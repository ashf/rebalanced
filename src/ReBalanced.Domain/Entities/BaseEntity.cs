namespace ReBalanced.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public bool IsDeleted { get; set; } = false;
}