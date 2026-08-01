# Módulo de admisiones EPS — .NET 8

Prueba de concepto de un módulo de admisiones con **persistencia políglota** (SQL Server + MongoDB),
consistencia garantizada mediante **patrón Outbox** y un dashboard en **tiempo real sobre Blazor Server**.

> Prueba técnica — Ingeniero de Desarrollo Senior (Cloud / .NET 8 / Híbrido).
> Las respuestas escritas están en [`docs/arquitectura-azure.md`](docs/arquitectura-azure.md) (Parte 1)
> y [`docs/code-review.md`](docs/code-review.md) (Parte 4).

---

## Índice

- [Arranque rápido](#arranque-rápido)
- [Arquitectura](#arquitectura)
- [La decisión central: cómo se resuelve el dual write](#la-decisión-central-cómo-se-resuelve-el-dual-write)
- [Mapa de la solución](#mapa-de-la-solución)
- [Cómo probarlo](#cómo-probarlo)
- [Modelo de datos](#modelo-de-datos)
- [Pruebas](#pruebas)
- [Decisiones y desviaciones del enunciado](#decisiones-y-desviaciones-del-enunciado)
- [Qué falta para producción](#qué-falta-para-producción)

---

## Arranque rápido

**Requisitos:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) y Docker Desktop.

```bash
# 1. Bases de datos (SQL Server 2022 + MongoDB 7)
docker compose up -d

# 2. Esperar a que SQL Server esté listo (~30 s la primera vez)
docker compose ps

# 3. Ejecutar la aplicación — aplica migraciones automáticamente en Development
dotnet run --project src/EPS.Admisiones.Web
```

| Recurso | URL |
|---|---|
| Dashboard en tiempo real | <http://localhost:5080/dashboard> |
| Swagger | <http://localhost:5080/swagger> |
| Health check | <http://localhost:5080/health> |
| Mongo Express (opcional) | <http://localhost:8081> |

Para levantar Mongo Express: `docker compose --profile herramientas up -d`.

Para detener y limpiar: `docker compose down -v`.

---

## Arquitectura

Arquitectura hexagonal (puertos y adaptadores), que es la forma de Clean Architecture que mejor
encaja cuando hay **dos almacenes de datos heterogéneos**: cada uno es un adaptador detrás de su
propio puerto, y el caso de uso no sabe cuál es cuál.

```
                        ┌──────────────────────────────┐
   HTTP  ──────────────▶│      Adaptadores entrada     │
   Blazor ─────────────▶│  Controllers · Componentes   │
                        └──────────────┬───────────────┘
                                       │ IAdmitirPacienteUseCase
                        ┌──────────────▼───────────────┐
                        │        Application           │
                        │  Casos de uso · PUERTOS      │
                        └──────────────┬───────────────┘
                                       │ depende solo de ↓
                        ┌──────────────▼───────────────┐
                        │           Domain             │
                        │ Agregados · VOs · Invariantes│
                        │   (cero dependencias)        │
                        └──────────────────────────────┘
                                       ▲
                        ┌──────────────┴───────────────┐
                        │     Adaptadores salida       │
                        │ EF Core · Mongo · Outbox     │
                        └──────────────────────────────┘
```

**Regla de dependencias:** las flechas apuntan siempre hacia adentro. `Domain` no referencia ningún
proyecto ni paquete de infraestructura; `Application` solo referencia `Domain`; `Infrastructure`
implementa las interfaces de `Application`; `Web` solo compone.

### Por qué hexagonal aquí y no una arquitectura por capas clásica

No es por ceremonia. Los tres beneficios concretos en este proyecto:

1. **Dos persistencias intercambiables.** `IHistoriaClinicaRepository` está implementado sobre
   MongoDB, pero pasar a Cosmos DB SQL API o a un blob no toca ni una línea del caso de uso.
2. **Tests sin bases de datos.** `AdmitirPacienteUseCaseTests` verifica la lógica completa —incluido
   el orden de las operaciones del Outbox— con dobles de prueba, en milisegundos.
3. **El Outbox es invisible para el dominio.** `OutboxMessage` vive en `Infrastructure`; el dominio
   solo emite eventos. Cambiar el transporte a Azure Service Bus es un cambio local.

Contrapeso honesto: hexagonal cuesta indirección. La disciplina aquí fue **no crear un puerto sin una
segunda implementación plausible o una necesidad real de test**. Por eso hay 8 puertos y no 20.

---

## La decisión central: cómo se resuelve el dual write

El enunciado plantea el problema así: *"si la escritura en MongoDB es exitosa pero la de SQL Server
falla, el sistema no puede quedar en estado inconsistente"*.

### El enfoque descartado

Escribir en Mongo y luego en SQL, con compensación si SQL falla:

```
1. Mongo.Insert(historia)        ✔
2. SQL.Insert(copago)            ✘  ← falla
3. Mongo.Delete(historia)        ← compensación... ¿y si también falla?
```

El paso 3 tiene el mismo problema que el paso 2. Además, si el proceso muere entre 1 y 2 —despliegue,
OOM, reinicio de la instancia— **no queda rastro** de que había algo que compensar. Se reduce la
probabilidad del fallo, no se elimina.

### El enfoque implementado: Outbox transaccional

Se invierte el orden y se convierten dos escrituras remotas en **una transacción local + propagación
asíncrona garantizada**:

```
┌─ Transacción única en SQL Server ──────────────────┐
│  INSERT Pacientes / Atenciones                     │
│  INSERT Admisiones      (copago bloqueado)         │
│  INSERT OutboxMessages  (payload FHIR completo)    │
└────────────────────────────────────────────────────┘
                        │ commit atómico
                        ▼
        OutboxDispatcher (BackgroundService)
                        │  Polly: timeout + retry + circuit breaker
                        ▼
        MongoDB — upsert idempotente por _id
                        │
                        ▼
        Admision.ConfirmarSincronizacion()  →  notifica al dashboard
```

**El escenario del enunciado deja de ser posible.** Ya no hay dos escrituras que puedan divergir:

| Situación | Resultado |
|---|---|
| SQL falla | Nada se guardó. El cliente recibe error y puede reintentar. Mongo ni se tocó |
| SQL confirma, Mongo cae | La admisión **existe** y el copago está bloqueado. El Outbox reintenta hasta que Mongo vuelva |
| Mongo sigue caído tras N intentos | `EstadoAdmision.FallidaSincronizacion`: queda para conciliación manual, **visible y medible**. La admisión nunca se pierde |
| El proceso muere a mitad | El mensaje sigue en la tabla Outbox. Al reiniciar, se despacha |
| El cliente reintenta por timeout | `Idempotency-Key` devuelve la admisión original. No se cobra dos veces |

**Garantía de entrega:** al menos una vez. Es seguro porque el `_id` del documento en Mongo se deriva
del identificador de la admisión (`admisionId.ToString("N")`), no lo genera Mongo. Reprocesar el mismo
mensaje hace un upsert sobre el mismo documento: idempotente por construcción.

Sobre transacciones distribuidas (MSDTC): descartadas. **Azure SQL no las soporta** entre motores
heterogéneos y, aunque lo hiciera, un 2PC acopla la disponibilidad de los dos almacenes —la caída de
cualquiera tumbaría las admisiones—.

---

## Mapa de la solución

```
src/
├── EPS.Admisiones.Domain/            Cero dependencias externas
│   ├── Admisiones/                   Admision (raíz), HistoriaClinica, EstadoAdmision
│   │   ├── ValueObjects/             DocumentoPaciente, ValorCopago
│   │   └── Events/                   PacienteAdmitido
│   ├── Pacientes/                    Paciente (raíz), Atencion, EstadoPaciente
│   └── Common/                       AggregateRoot, ValueObject, IDomainEvent
│
├── EPS.Admisiones.Application/       Casos de uso y puertos
│   ├── Ports/                        8 interfaces = frontera del hexágono
│   ├── UseCases/AdmitirPaciente/     AdmitirPacienteUseCase + extractor FHIR
│   └── Contracts/                    DTOs de entrada/salida
│
├── EPS.Admisiones.Infrastructure/    Adaptadores
│   ├── Persistence/SqlServer/        DbContext, configuraciones, repos, queries
│   ├── Persistence/MongoDb/          Repositorio documental con upsert
│   ├── Outbox/                       OutboxMessage, Writer, Dispatcher
│   ├── Messaging/                    AdmisionEventBus (tiempo real)
│   └── Resilience/                   Políticas de Polly v8
│
└── EPS.Admisiones.Web/               Host único
    ├── Controllers/                  API REST
    ├── Components/Pages/             DashboardAdmisiones.razor
    └── Utilities/                    DomainExceptionHandler → ProblemDetails

tests/EPS.Admisiones.Tests/           Sin bases de datos
db/01-schema.sql                      Modelo relacional (equivalente a las migraciones)
docs/                                 Respuestas de las Partes 1 y 4
```

### Los ocho puertos

| Puerto | Dirección | Adaptador |
|---|---|---|
| `IAdmitirPacienteUseCase` | Entrada | `AdmisionesController`, dashboard |
| `IAdmisionRepository` | Salida | EF Core / SQL Server |
| `IPacienteRepository` | Salida | EF Core / SQL Server |
| `IAdmisionesQuery` | Salida (lectura) | EF Core con proyecciones |
| `IUnitOfWork` | Salida | `SaveChangesAsync` |
| `IOutboxWriter` | Salida | Tabla `OutboxMessages` |
| `IHistoriaClinicaRepository` | Salida | MongoDB |
| `IAdmisionNotifier` | Salida | `AdmisionEventBus` en memoria |
| `IRelojSistema` | Salida | `DateTime.UtcNow` / reloj fijo en tests |

---

## Cómo probarlo

### Formato del payload

El enunciado permite simular la historia clínica. Perfil simplificado usado —los campos que consume
`ExtractorDatosFacturables` son `paciente`, `copago` y `encuentro.requiereAuditoria`; el resto del
documento viaja íntegro a MongoDB—:

```json
{
  "resourceType": "Bundle",
  "paciente": {
    "tipoDocumento": "CC",
    "numeroDocumento": "1098765432",
    "nombre": "Ana Maria",
    "apellido": "Portilla"
  },
  "copago": { "valor": 25000.00, "moneda": "COP" },
  "encuentro": {
    "clase": "ambulatorio",
    "requiereAuditoria": true,
    "diagnosticos": [
      { "codigo": "J06.9", "descripcion": "Infeccion aguda de vias respiratorias" }
    ],
    "signosVitales": { "presionArterial": "120/80", "frecuenciaCardiaca": 78 }
  }
}
```

### Flujo completo en tiempo real

1. Abrir <http://localhost:5080/dashboard>.
2. Con el dashboard visible, enviar una admisión:

```bash
curl -X POST http://localhost:5080/api/admisiones \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: 3f1c9a52-7d4b-4d2e-9a11-6c0f2b8e4d10" \
  -d @docs/ejemplo-admision.json
```

3. La fila aparece en el dashboard **sin recargar**, un par de segundos después: ese retardo es el
   ciclo del despachador del Outbox, y confirma que la fila solo se muestra cuando el dato ya es
   consistente en ambos almacenes.

El archivo [`EPS.Admisiones.Web.http`](src/EPS.Admisiones.Web/EPS.Admisiones.Web.http) tiene todas las
peticiones listas para VS Code / Visual Studio / Rider.

### Verificar la consistencia en ambos almacenes

```bash
# SQL Server — registro transaccional del copago
docker exec -it eps-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Local_Dev_2026!' -C -d EpsAdmisiones \
  -Q "SELECT NumeroDocumento, ValorCopago, Estado FROM admisiones.Admisiones"

# MongoDB — historia clínica completa
docker exec -it eps-mongo mongosh eps_admisiones \
  --eval "db.historias_clinicas.find().pretty()"

# Outbox — debe quedar sin pendientes
docker exec -it eps-sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'Local_Dev_2026!' -C -d EpsAdmisiones \
  -Q "SELECT COUNT(*) AS Pendientes FROM admisiones.OutboxMessages WHERE ProcesadoEnUtc IS NULL"
```

### Provocar el fallo que plantea el enunciado

La forma más directa de comprobar que el diseño aguanta:

```bash
# 1. Tumbar MongoDB
docker compose stop mongo

# 2. Admitir un paciente → responde 201. La admisión SÍ se registró.
curl -X POST http://localhost:5080/api/admisiones -H "Content-Type: application/json" -d @docs/ejemplo-admision.json

# 3. Verificar: el copago está bloqueado en SQL y el mensaje espera en el Outbox.
#    El dashboard todavía no la muestra: aún no es consistente.

# 4. Levantar MongoDB
docker compose start mongo

# 5. En pocos segundos el despachador converge, el documento aparece en Mongo,
#    el estado pasa a Sincronizada y la fila surge en el dashboard.
```

Ninguna admisión se perdió y en ningún momento hubo un estado inconsistente observable.

---

## Modelo de datos

### SQL Server — esquema `admisiones`

| Tabla | Propósito |
|---|---|
| `Pacientes` | Afiliados. Índice único por `(TipoDocumento, NumeroDocumento)` |
| `Atenciones` | Atenciones facturables. Base del reporte de auditoría (Parte 4) |
| `Admisiones` | Registro transaccional con el copago bloqueado. `ROWVERSION` para concurrencia optimista |
| `OutboxMessages` | Mensajes pendientes de propagar. Índice **filtrado** por `ProcesadoEnUtc IS NULL` |

Script completo en [`db/01-schema.sql`](db/01-schema.sql). En desarrollo se aplica automáticamente
vía migraciones al arrancar.

### MongoDB — colección `historias_clinicas`

```javascript
{
  "_id": "3f1c9a527d4b4d2e9a116c0f2b8e4d10",  // derivado del AdmisionId → upsert idempotente
  "tipoDocumento": "CC",
  "numeroDocumento": "1098765432",
  "recursoFhir": "Bundle",
  "capturadaEnUtc": ISODate("2026-07-31T14:30:00Z"),
  "contenido": { /* payload FHIR parseado: consultable e indexable */ },
  "contenidoOriginal": "{ ... }"  // texto literal recibido
}
```

Se guardan las dos formas a propósito: `BsonDocument.Parse` convierte los números JSON a `double`,
así que conservar el texto original es lo que garantiza **fidelidad clínica y trazabilidad ante
auditoría**. El campo parseado es el que se indexa y consulta.

---

## Pruebas

```bash
dotnet test
```

Los tests cubren el caso de uso completo **sin levantar ninguna base de datos**, usando dobles sobre
los puertos. Lo más relevante:

| Test | Qué protege |
|---|---|
| `Encola_el_mensaje_de_Outbox_ANTES_del_commit` | La invariante que sostiene toda la estrategia: si el commit fuese primero, existiría una ventana sin garantía |
| `Reintentar_con_la_misma_clave_de_idempotencia_no_duplica_la_admision` | No cobrar dos veces el copago |
| `Si_SQL_falla_no_queda_nada_a_medias` | Atomicidad del commit |
| `Un_payload_invalido_no_llega_a_tocar_la_base_de_datos` | Validación antes de la persistencia |
| `Tras_agotar_los_reintentos_queda_marcada_para_conciliacion_manual` | La admisión nunca se pierde en silencio |

---

## Decisiones y desviaciones del enunciado

El enunciado invita a modificar los requerimientos explicando los cambios. Las desviaciones:

### 1. Se invirtió el orden del dual write

**Enunciado:** guardar primero en MongoDB, extraer y guardar después en SQL Server.
**Implementado:** SQL Server primero (transaccional) y MongoDB por Outbox.

**Razón:** el orden original produce, inevitablemente, la inconsistencia que el propio enunciado pide
evitar en el punto 4. Invertirlo la elimina por construcción en lugar de mitigarla. El payload íntegro
sigue llegando completo a MongoDB, que es el requisito funcional real.

### 2. Se añadieron `Paciente` y `Atencion` al modelo

El reporte de la Parte 4 opera sobre `Pacientes` y `Atenciones`. En lugar de crear tablas ficticias
solo para el ejemplo, el caso de uso de admisión las alimenta: **el reporte corre sobre datos reales
generados por la Parte 2**, y la versión optimizada es ejecutable, no teórica.

### 3. `Estado` como enum en vez de string

El código original comparaba `p.Estado == "Activo"`. Se modeló como `enum` persistido como `int`:
indexable y a prueba de typos. Es uno de los hallazgos del code review, aplicado.

### 4. EF Core en lugar de Dapper

EF Core para todo. La justificación del criterio —y cuándo saltaría a Dapper— está en
[`docs/code-review.md`](docs/code-review.md#nota-sobre-ef-core-vs-dapper).

### 5. Bus en memoria para el tiempo real

`AdmisionEventBus` es un singleton en memoria: alcanza solo a los circuitos de **esta** instancia.
Es suficiente para una PoC de una instancia y está aislado detrás de `IAdmisionNotifier`, así que
migrar a Azure SignalR o Redis backplane no toca el resto del sistema. La limitación está documentada
en la propia clase, no escondida.

### 6. Nomenclatura mixta español/inglés

El dominio usa el lenguaje ubicuo del enunciado (`AdmitirPacienteUseCase`, `DocumentoPaciente`,
`ValorCopago`); la infraestructura y los términos técnicos, inglés (`Repository`, `UnitOfWork`,
`Outbox`). Los comentarios explican **por qué**, no qué hace el código.

---

## Qué falta para producción

Alcance deliberadamente excluido de una PoC de 72 horas, listado para que no se confunda con un
descuido:

- **Autenticación y autorización** (Entra ID + RBAC por rol clínico) y auditoría de accesos a datos
  de salud.
- **Reclamo de lote en el Outbox** con `UPDATE ... WITH (READPAST, UPDLOCK) OUTPUT` o un despachador
  en un worker único; hoy dos instancias pueden duplicar trabajo —sin corromper datos, gracias a la
  idempotencia—.
- **Backplane real para SignalR** (Azure SignalR Service).
- **Validación FHIR estricta** con `Hl7.Fhir.R4` en lugar del extractor simplificado.
- **Tests de integración** con Testcontainers y pruebas de carga sobre el dashboard.
- **Observabilidad**: OpenTelemetry con trazas correlacionadas API → Outbox → Mongo, y alertas sobre
  admisiones pendientes de sincronización.
- **Cifrado a nivel de columna** (Always Encrypted) sobre el número de documento.
