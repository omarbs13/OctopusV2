using Microsoft.EntityFrameworkCore;
using Pos.Application.Discounts;
using Pos.Domain.Discounts;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Discounts;

public sealed class DiscountApprovalStore : IDiscountApprovalStore
{
    private readonly PosDbContext _context;

    public DiscountApprovalStore(PosDbContext context) => _context = context;

    public void Add(DiscountApproval approval) => _context.DiscountApprovals.Add(approval);

    public async Task<IReadOnlyList<DiscountApproval>> ListForDraftAsync(Guid draftId, Guid requestedBy, CancellationToken cancellationToken) =>
        await _context.DiscountApprovals.AsNoTracking()
            .Where(a => a.DraftId == draftId && a.RequestedBy == requestedBy)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _context.SaveChangesAsync(cancellationToken);
}
