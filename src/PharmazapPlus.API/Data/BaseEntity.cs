namespace PharmazapPlus.API.Data;

// Classe base para entidades com timestamps
public abstract class BaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
