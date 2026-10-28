namespace AuthService.Repositories;

public interface IBaseRepository<TEntity> where TEntity : class
{
    Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    void Add(TEntity entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
