namespace Pos.Application.Reports;

/// <summary>Mensajes de los reportes, en español.</summary>
public static class ReportMessages
{
    public const string EndBeforeStart = "La fecha final no puede ser anterior a la inicial.";
    public const string PeriodTooLong = "El período no puede superar 366 días.";
    public const string InvalidDate = "La fecha no es válida.";
    public const string NoData = "Sin datos en este período";
    public const string ThresholdRange = "El umbral debe estar entre 0.01 % y 100 %.";
    public const string MissingBusiness = "Faltan los datos del negocio.";
}
