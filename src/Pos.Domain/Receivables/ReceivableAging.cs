namespace Pos.Domain.Receivables;

/// <summary>
/// Atraso de una cuenta por cobrar en días calendario locales (014, FR-018, research §9). El
/// vencimiento no se guarda: se calcula con el plazo actual.
/// </summary>
public static class ReceivableAging
{
    /// <summary><c>max(0, hoy − (venta + plazo))</c>.</summary>
    public static int DaysOverdue(DateOnly saleLocalDate, DateOnly todayLocal, int termDays) =>
        Math.Max(0, todayLocal.DayNumber - (saleLocalDate.DayNumber + termDays));

    public static bool IsOverdue(int daysOverdue, long balanceCents) => daysOverdue > 0 && balanceCents > 0;
}
