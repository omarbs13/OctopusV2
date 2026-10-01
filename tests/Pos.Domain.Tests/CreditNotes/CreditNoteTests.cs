using Pos.Domain.Common;
using Pos.Domain.CreditNotes;

namespace Pos.Domain.Tests.CreditNotes;

/// <summary>013, research §5: el saldo se calcula y nunca es negativo (SC-005).</summary>
public sealed class CreditNoteTests
{
    [Fact]
    public void Issue_CreaElMovimientoDeEmisionPorElImporteInicial()
    {
        var note = CreditNote.Issue(7, Guid.NewGuid(), 5_000);
        var issue = note.RecordIssue(Guid.NewGuid());

        Assert.Equal("NC-000007", note.Folio);
        Assert.Equal(CreditNoteMovementType.Issue, issue.Type);
        Assert.Equal(5_000, issue.AmountCents);
        Assert.Equal(1, issue.Sequence);
        Assert.Throws<DomainException>(() => CreditNote.Issue(1, Guid.NewGuid(), 0));
    }

    [Fact]
    public void Redeem_ValidaSaldoSuficiente_YElSaldoQuedaEnCeroNuncaNegativo()
    {
        var note = CreditNote.Issue(1, Guid.NewGuid(), 5_000);

        var use = note.Redeem(5_000, currentBalanceCents: 5_000, nextSequence: 2, Guid.NewGuid());

        Assert.Equal(0, CreditNote.Balance(5_000, 0, use.AmountCents));
        Assert.Throws<DomainException>(() => note.Redeem(5_001, 5_000, 2, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => note.Redeem(0, 5_000, 2, Guid.NewGuid()));
    }

    [Fact]
    public void Restore_DevuelveSaldoYElSaldoSumaEmisionMasRestauracionMenosUsos()
    {
        var note = CreditNote.Issue(1, Guid.NewGuid(), 5_000);
        var restore = note.Restore(1_500, 3, Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(CreditNoteMovementType.Restore, restore.Type);
        Assert.Equal(5_000 + 1_500 - 2_000, CreditNote.Balance(5_000, 1_500, 2_000));
        Assert.True(CreditNoteFolio.TryParse("nc-12", out var number));
        Assert.Equal(12, number);
        Assert.False(CreditNoteFolio.TryParse("V-12", out _));
    }
}
