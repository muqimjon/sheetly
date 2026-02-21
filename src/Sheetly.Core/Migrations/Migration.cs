namespace Sheetly.Core.Migrations;

/// <summary>
/// Base class for all migrations. Similar to Entity Framework's Migration class.
/// </summary>
public abstract class Migration
{
	/// <summary>
	/// Builds the operations that will upgrade the database schema.
	/// </summary>
	/// <param name="builder">The migration builder to use for creating operations.</param>
	public abstract void Up(MigrationBuilder builder);

	/// <summary>
	/// Builds the operations that will downgrade the database schema.
	/// </summary>
	/// <param name="builder">The migration builder to use for creating operations.</param>
	public abstract void Down(MigrationBuilder builder);
}
