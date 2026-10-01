namespace Pos.Application.Receivables.ListCustomerReceivables;

public sealed record ListCustomerReceivablesQuery(Guid CustomerId, bool OnlyPending = false, int Page = 1);
