namespace Pos.Application.Customers.GetCustomerCreditStatus;

public sealed record GetCustomerCreditStatusQuery(Guid CustomerId, long SaleTotalCents);
