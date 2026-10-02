namespace Pos.Application.Suppliers.SetSupplierActive;

public sealed record SetSupplierActiveCommand(Guid Id, int ExpectedVersion, bool Active);
