namespace Pos.Application.Purchases.VoidPurchase;

/// <summary>Anulación completa de una compra vigente con motivo obligatorio (FR-017a).</summary>
public sealed record VoidPurchaseCommand(Guid PurchaseId, int ExpectedVersion, string Reason);
