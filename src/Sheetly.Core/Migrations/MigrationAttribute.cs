namespace Sheetly.Core.Migrations;

/// <summary>
/// Attribute to mark a class as a migration with a unique ID.
/// Similar to Entity Framework's Migration attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MigrationAttribute : Attribute
{
	/// <summary>
	/// Gets the unique identifier for this migration.
	/// Format: yyyyMMddHHmmss_MigrationName
	/// </summary>
	public string Id { get; }

	/// <summary>
	/// Creates a new migration attribute with the specified ID.
	/// </summary>
	/// <param name="id">The unique migration identifier.</param>
	public MigrationAttribute(string id)
	{
		Id = id ?? throw new ArgumentNullException(nameof(id));
	}
}
