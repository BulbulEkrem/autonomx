namespace AutoNomX.Domain.Interfaces;

/// <summary>Unit of work for transactional persistence operations.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
