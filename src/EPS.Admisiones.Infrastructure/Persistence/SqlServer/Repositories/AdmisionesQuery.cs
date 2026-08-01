using System.Runtime.CompilerServices;
using EPS.Admisiones.Application.Contracts;
using EPS.Admisiones.Application.Ports;
using EPS.Admisiones.Domain.Admisiones;
using EPS.Admisiones.Domain.Pacientes;
using Microsoft.EntityFrameworkCore;

namespace EPS.Admisiones.Infrastructure.Persistence.SqlServer.Repositories;

/// <summary>
/// Lado LECTURA. Todas las consultas son AsNoTracking y proyectan a DTO:
/// nunca materializan entidades ni activan el change tracker.
/// </summary>
public sealed class AdmisionesQuery : IAdmisionesQuery
{
    private readonly AdmisionesDbContext _db;

    public AdmisionesQuery(AdmisionesDbContext db) => _db = db;

    public async Task<IReadOnlyList<AdmisionResumen>> ObtenerRecientesAsync(
        int cantidad,
        CancellationToken cancellationToken)
    {
        if (cantidad is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidad),
                "La cantidad debe estar entre 1 y 500.");
        }

        // El ToString() de los enums no es traducible a SQL, asi que se
        // proyecta el valor crudo y se formatea despues de materializar.
        var filas = await _db.Admisiones
            .AsNoTracking()
            .OrderByDescending(a => a.FechaAdmisionUtc)
            .Take(cantidad)
            .Select(a => new
            {
                a.Id,
                a.Documento.Tipo,
                a.Documento.Numero,
                a.Copago.Monto,
                a.Copago.Moneda,
                a.FechaAdmisionUtc,
                a.Estado
            })
            .ToListAsync(cancellationToken);

        return filas
            .Select(f => new AdmisionResumen(
                f.Id,
                f.Tipo.ToString(),
                f.Numero,
                f.Monto,
                f.Moneda,
                f.FechaAdmisionUtc,
                f.Estado.ToString()))
            .ToList();
    }

    public async Task<MetricasAdmisiones> ObtenerMetricasAsync(CancellationToken cancellationToken)
    {
        var inicioDelDia = DateTime.UtcNow.Date;

        // Cuatro agregaciones sobre indices, sin materializar ninguna entidad.
        // Se aceptan cuatro viajes a la base porque cada uno resuelve con un
        // seek + stream aggregate; unificarlos en una sola consulta con
        // subconsultas produciria un plan peor y codigo menos legible.
        var admisionesHoy = await _db.Admisiones
            .AsNoTracking()
            .CountAsync(a => a.FechaAdmisionUtc >= inicioDelDia, cancellationToken);

        // SumAsync sobre decimal? evita la excepcion cuando no hay filas:
        // SQL devuelve NULL para SUM() sin registros, no cero.
        var copagoHoy = await _db.Admisiones
            .AsNoTracking()
            .Where(a => a.FechaAdmisionUtc >= inicioDelDia)
            .SumAsync(a => (decimal?)a.Copago.Monto, cancellationToken) ?? 0m;

        var pendientes = await _db.Admisiones
            .AsNoTracking()
            .CountAsync(a => a.Estado == EstadoAdmision.PendienteSincronizacion, cancellationToken);

        var fallidas = await _db.Admisiones
            .AsNoTracking()
            .CountAsync(a => a.Estado == EstadoAdmision.FallidaSincronizacion, cancellationToken);

        return new MetricasAdmisiones(admisionesHoy, copagoHoy, pendientes, fallidas);
    }

    /// <summary>
    /// Version optimizada del endpoint de la Parte 4. Diferencias con el
    /// original: filtrado y agregacion en el servidor, proyeccion directa a DTO
    /// (sin Include ni entidades), sin change tracking y con streaming, de modo
    /// que la memoria del proceso no crece con el tamano del resultado.
    /// </summary>
    public async IAsyncEnumerable<ReporteAuditoriaItem> ObtenerReporteAuditoriaAsync(
        DateTime desdeUtc,
        DateTime hastaUtc,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (hastaUtc <= desdeUtc)
        {
            throw new ArgumentException(
                "El fin del rango debe ser posterior al inicio.",
                nameof(hastaUtc));
        }

        // El paso intermedio por un tipo anonimo no es decorativo: EF Core solo
        // puede resolver un OrderBy sobre un miembro de la proyeccion cuando el
        // NewExpression trae poblada su lista de Members. Los tipos anonimos la
        // pueblan; el constructor posicional de un record NO. Ordenar
        // directamente sobre ReporteAuditoriaItem.TotalAuditar lanza
        // InvalidOperationException en tiempo de ejecucion.
        var consulta = _db.Pacientes
            .AsNoTracking()
            .Where(p => p.Estado == EstadoPaciente.Activo
                && p.Atenciones.Any(a =>
                    a.RequiereAuditoria
                    && a.FechaUtc >= desdeUtc
                    && a.FechaUtc < hastaUtc))
            .Select(p => new
            {
                NombreCompleto = p.Nombre + " " + p.Apellido,
                TotalAuditar = p.Atenciones
                    .Where(a => a.RequiereAuditoria
                        && a.FechaUtc >= desdeUtc
                        && a.FechaUtc < hastaUtc)
                    .Sum(a => a.Valor.Monto)
            })
            .OrderByDescending(x => x.TotalAuditar)
            // Desempate estable: sin un orden total determinista, dos filas con
            // el mismo total pueden alternar entre peticiones, lo que rompe
            // cualquier paginacion posterior sobre este mismo reporte.
            .ThenBy(x => x.NombreCompleto)
            .Select(x => new ReporteAuditoriaItem(x.NombreCompleto, x.TotalAuditar))
            .AsAsyncEnumerable();

        await foreach (var item in consulta.WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}
