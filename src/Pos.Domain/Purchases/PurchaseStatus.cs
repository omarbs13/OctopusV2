using Pos.Domain.Common;

namespace Pos.Domain.Purchases;

/// <summary>Estado de una compra (020): <c>ACTIVE → VOIDED</c>, una sola vez.</summary>
public enum PurchaseStatus
{
    Active,
    Voided,
}

public static class PurchaseStatusExtensions
{
    public static string ToCode(this PurchaseStatus status) => status switch
    {
        PurchaseStatus.Active => "ACTIVE",
        PurchaseStatus.Voided => "VOIDED",
        _ => throw new DomainException("El estado de la compra no es válido."),
    };

    public static PurchaseStatus FromCode(string code) => code switch
    {
        "ACTIVE" => PurchaseStatus.Active,
        "VOIDED" => PurchaseStatus.Voided,
        _ => throw new DomainException("El estado de la compra no es válido."),
    };

    /// <summary>Texto en español: "Vigente" o "Anulada".</summary>
    public static string Describe(this PurchaseStatus status) => status switch
    {
        PurchaseStatus.Active => "Vigente",
        PurchaseStatus.Voided => "Anulada",
        _ => throw new DomainException("El estado de la compra no es válido."),
    };
}
