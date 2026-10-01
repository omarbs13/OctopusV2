namespace Pos.Application.Customers.SetCustomerActive;

public sealed record SetCustomerActiveCommand(Guid Id, int ExpectedVersion, bool Active);
