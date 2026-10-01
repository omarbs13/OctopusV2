namespace Pos.Application.Receivables.ListCustomerPayments;

public sealed record ListCustomerPaymentsQuery(Guid CustomerId, int Page = 1);
