namespace Sheetly.Core;

/// <summary>
/// Encapsulates the fluent configuration for one entity type, mirroring EF Core's
/// <c>IEntityTypeConfiguration&lt;T&gt;</c>. Register with
/// <see cref="ModelBuilder.ApplyConfiguration{T}"/> or
/// <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>.
/// </summary>
public interface IEntityTypeConfiguration<T> where T : class
{
	void Configure(EntityTypeBuilder<T> builder);
}
