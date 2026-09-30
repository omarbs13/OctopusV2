using Microsoft.EntityFrameworkCore;
using Pos.Application.Business;
using Pos.Application.Products;
using Pos.Domain.Business;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Business;

public sealed class BusinessProfileRepository : IBusinessProfileRepository
{
    private readonly PosDbContext _context;

    public BusinessProfileRepository(PosDbContext context) => _context = context;

    public Task<BusinessProfile?> GetAsync(CancellationToken cancellationToken) =>
        _context.BusinessProfiles.Where(p => p.DeletedAt == null).OrderBy(p => p.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    public void Add(BusinessProfile profile) => _context.BusinessProfiles.Add(profile);

    public async Task<SaveOutcome> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            return SaveOutcome.Conflict;
        }
    }
}
