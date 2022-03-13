namespace ReBalanced.Domain.Providers;

public interface IEntityRepository<T>
{
    Task<T> Create(T entity);
    Task<T?> Get(Guid id);
    Task<ICollection<T>> Get(ICollection<Guid> ids);
    Task<T?> Update(Guid id, T entity);
    Task<bool> Delete(Guid id);
}