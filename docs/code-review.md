# Parte 4 — Code review y optimización

## Código original

```csharp
[HttpGet("reporte-mensual")]
public async Task<IActionResult> GenerarReporteMensual()
{
    var pacientes = _dbContext.Pacientes
        .Include(p => p.Atenciones)
        .ToList();

    var resultado = new List<ReporteDto>();

    foreach (var p in pacientes)
    {
        if (p.Estado == "Activo" && p.Atenciones.Any(a => a.RequiereAuditoria))
        {
            resultado.Add(new ReporteDto
            {
                NombreCompleto = p.Nombre + " " + p.Apellido,
                TotalAuditar = p.Atenciones.Where(a => a.RequiereAuditoria).Sum(a => a.Valor)
            });
        }
    }

    return Ok(resultado);
}
```

---

## Problemas identificados

### 1. Se materializa la base de datos entera en memoria (crítico)

`.Include(p => p.Atenciones).ToList()` trae **todos** los pacientes y **todas** sus atenciones
antes de aplicar un solo filtro. El `WHERE` está en C#, no en SQL.

Con una EPS real —cientos de miles de afiliados y millones de atenciones— esto reserva cientos de
megabytes por petición. Dos o tres llamadas concurrentes y el proceso muere por `OutOfMemoryException`.
Es la causa directa del síntoma reportado.

Agravante: `Include` genera un `JOIN`, así que las columnas del paciente se **repiten en cada fila
de atención**. Un paciente con 200 atenciones viaja 200 veces por la red. EF luego deduplica en
memoria, pero el ancho de banda y las asignaciones ya se pagaron.

### 2. `ToList()` síncrono dentro de un método `async` (crítico en concurrencia)

El método se declara `async` pero llama a `ToList()`, que es bloqueante. El hilo del thread pool
queda retenido durante toda la consulta en lugar de liberarse.

Con 5.000 usuarios concurrentes esto produce **thread pool starvation**: ASP.NET Core inyecta
hilos nuevos a razón de uno o dos por segundo, las peticiones se encolan y la latencia se dispara
en todo el servidor —no solo en este endpoint—. Es un fallo que se propaga.

### 3. Change tracking activo sobre entidades de solo lectura

No hay `AsNoTracking()`. EF Core crea una entrada de seguimiento por cada entidad materializada y
guarda una copia de sus valores originales para detectar cambios.

En un reporte que nunca escribe, eso es **aproximadamente el doble de memoria** y un costo de CPU
en la construcción del grafo, a cambio de cero beneficio.

### 4. El reporte "mensual" no filtra por mes

No hay ningún predicado sobre fechas: el resultado crece de forma monótona con la antigüedad del
sistema. Un endpoint cuyo costo aumenta cada día es una bomba de tiempo, aunque hoy responda.

### 5. Doble enumeración de la colección por cada paciente

`p.Atenciones.Any(...)` recorre las atenciones y `p.Atenciones.Where(...).Sum(...)` las vuelve a
recorrer. Es trabajo O(2n) en cliente que el motor de base de datos resolvería en una sola pasada.

### 6. Se acumula el resultado completo antes de responder

`List<ReporteDto>` retiene todas las filas en RAM hasta que termina el `foreach`, y luego el
serializador construye el JSON completo. Nada se envía al cliente hasta que todo está listo:
pico de memoria máximo y *time to first byte* pésimo.

### 7. El estado se compara como cadena

`p.Estado == "Activo"` es frágil (un `"activo"` en minúscula rompe el reporte en silencio) y, al
evaluarse en cliente, no puede aprovechar ningún índice. Se corrige modelándolo como `enum`
persistido como `int`.

### 8. Sin `CancellationToken`

Si el usuario cierra la pestaña, el servidor sigue ejecutando la consulta completa. Bajo carga,
esos trabajos fantasma compiten por los mismos recursos que las peticiones vivas.

---

## Versión optimizada

Implementada en
[`AdmisionesQuery.ObtenerReporteAuditoriaAsync`](../src/EPS.Admisiones.Infrastructure/Persistence/SqlServer/Repositories/AdmisionesQuery.cs)
y expuesta por
[`ReportesController`](../src/EPS.Admisiones.Web/Controllers/ReportesController.cs).

```csharp
public async IAsyncEnumerable<ReporteAuditoriaItem> ObtenerReporteAuditoriaAsync(
    DateTime desdeUtc,
    DateTime hastaUtc,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    if (hastaUtc <= desdeUtc)
    {
        throw new ArgumentException("El fin del rango debe ser posterior al inicio.", nameof(hastaUtc));
    }

    var consulta = _db.Pacientes
        .AsNoTracking()                                   // (3) sin change tracking
        .Where(p => p.Estado == EstadoPaciente.Activo     // (1)(7) filtro en servidor, enum
            && p.Atenciones.Any(a =>
                a.RequiereAuditoria
                && a.FechaUtc >= desdeUtc                 // (4) rango obligatorio
                && a.FechaUtc < hastaUtc))
        .Select(p => new                                  // (1) proyección: sin Include
        {
            NombreCompleto = p.Nombre + " " + p.Apellido,
            TotalAuditar = p.Atenciones                   // (5) SUM ejecutado por SQL Server
                .Where(a => a.RequiereAuditoria
                    && a.FechaUtc >= desdeUtc
                    && a.FechaUtc < hastaUtc)
                .Sum(a => a.Valor.Monto)
        })
        .OrderByDescending(x => x.TotalAuditar)
        .ThenBy(x => x.NombreCompleto)                    // (9) orden total determinista
        .Select(x => new ReporteAuditoriaItem(x.NombreCompleto, x.TotalAuditar))
        .AsAsyncEnumerable();                             // (2)(6) asíncrono y en streaming

    await foreach (var item in consulta.WithCancellation(cancellationToken))  // (8)
    {
        yield return item;
    }
}
```

### Por qué el paso por un tipo anónimo

No es adorno. La versión directa —proyectar al `record` y ordenar después— compila
sin una sola advertencia y **falla en tiempo de ejecución**:

```csharp
.Select(p => new ReporteAuditoriaItem(p.Nombre + " " + p.Apellido, /* ... */))
.OrderByDescending(r => r.TotalAuditar)   // InvalidOperationException
```

> `The LINQ expression (...) could not be translated.`

EF Core solo puede resolver un `OrderBy` sobre un miembro de la proyección cuando
el `NewExpression` del árbol de sintaxis trae poblada su colección `Members`. Los
tipos anónimos la pueblan; el constructor posicional de un `record` no, porque sus
argumentos son parámetros del constructor y no enlaces a miembros. EF pierde el
rastro entre `TotalAuditar` y el `SUM` que lo produce, y aborta la traducción.

El orden es entonces: proyectar a un tipo anónimo → ordenar → proyectar al DTO
final. La última proyección sí puede usar el constructor del `record`, porque
después de ella no hay ningún acceso a miembros que traducir.

Es una trampa que no aparece en pruebas unitarias con el proveedor InMemory
—ese sí evalúa en cliente— y solo se manifiesta contra SQL Server real. Por eso
la cobertura de este endpoint es una prueba de integración y no unitaria.

### Qué cambia en la práctica

| Aspecto | Antes | Después |
|---|---|---|
| Filas transferidas | Todos los pacientes × sus atenciones | Solo los pacientes del reporte |
| Columnas por fila | Entidad completa + JOIN repetido | Dos escalares |
| Memoria del proceso | Proporcional al tamaño de la tabla | Constante (una fila en vuelo) |
| Hilos bloqueados | Uno por petición | Ninguno |
| Agregación | En C#, tras traer todo | `SUM` en SQL Server |
| Cancelación | No soportada | Propagada hasta el motor |

`Select` a un tipo con constructor posicional permite a EF Core traducir la proyección y devolver
exactamente dos columnas por fila. `AsAsyncEnumerable` mantiene abierto el `DataReader` y ASP.NET
Core serializa elemento a elemento: la memoria deja de escalar con el tamaño del resultado.

### SQL generado (aproximado)

```sql
SELECT  p.[Nombre] + N' ' + p.[Apellido] AS NombreCompleto,
        (SELECT COALESCE(SUM(a0.[Valor]), 0.0)
         FROM   [admisiones].[Atenciones] AS a0
         WHERE  p.[Id] = a0.[PacienteId]
                AND a0.[RequiereAuditoria] = CAST(1 AS bit)
                AND a0.[FechaUtc] >= @desde
                AND a0.[FechaUtc] <  @hasta) AS TotalAuditar
FROM    [admisiones].[Pacientes] AS p
WHERE   p.[Estado] = 1
        AND EXISTS (SELECT 1
                    FROM   [admisiones].[Atenciones] AS a
                    WHERE  p.[Id] = a.[PacienteId]
                           AND a.[RequiereAuditoria] = CAST(1 AS bit)
                           AND a.[FechaUtc] >= @desde
                           AND a.[FechaUtc] <  @hasta)
ORDER BY TotalAuditar DESC;
```

El índice de cobertura definido en `AtencionConfiguration` está pensado para este plan:

```csharp
builder.HasIndex(a => new { a.RequiereAuditoria, a.FechaUtc })
       .IncludeProperties(a => a.PacienteId);
```

### Nota sobre EF Core vs. Dapper

La versión de arriba resuelve el problema **dentro de EF Core**, que es lo que pedía el enunciado.
Si este reporte creciera hasta convertirse en un cuello de botella medido —por ejemplo, si el
optimizador eligiera un plan distinto al esperado o hiciera falta un `OPTION (RECOMPILE)`—, el
siguiente paso sería reescribirlo con **Dapper** y SQL explícito.

El criterio que aplicaría: EF Core para escrituras transaccionales, donde el change tracking y el
`SaveChanges` transaccional aportan valor real; Dapper para lecturas analíticas donde ese mismo
tracking es puro sobrecosto y el control fino del plan importa. En una PoC de 72 horas, introducir
Dapper para un solo endpoint agrega una dependencia y un patrón de conexión sin beneficio medible,
así que se documenta la decisión en lugar de implementarla.
