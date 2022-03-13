namespace ReBalanced.Application.Services.Interfaces;

public interface IBaseEntityService<T>
{
    public Task<T?> Get(Guid id);
    public Task<bool> Delete(Guid id);
    public Task<T?> Update(Guid id, T item);
}