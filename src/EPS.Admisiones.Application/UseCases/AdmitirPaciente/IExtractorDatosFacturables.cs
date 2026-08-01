using System.Text.Json;
using EPS.Admisiones.Domain.Admisiones.ValueObjects;

namespace EPS.Admisiones.Application.UseCases.AdmitirPaciente;

/// <summary>
/// Traduce el documento clinico semi-estructurado a los pocos datos que el
/// modelo relacional necesita. Es una interfaz porque el mapeo depende del
/// perfil de implementacion FHIR de cada prestador y cambia con el tiempo.
/// </summary>
public interface IExtractorDatosFacturables
{
    DatosFacturables Extraer(JsonElement historiaClinica);
}

/// <summary>Datos minimos que se proyectan al almacen transaccional.</summary>
public sealed record DatosFacturables(
    DocumentoPaciente Documento,
    ValorCopago Copago,
    string Nombre,
    string Apellido,
    bool RequiereAuditoria,
    string RecursoFhir);
