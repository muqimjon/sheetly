namespace Sheetly.Core.Migrations;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MigrationAttribute : Attribute
{
	/// <summary>Format: yyyyMMddHHmmss_MigrationName</summary>
	public string Id { get; }

	public MigrationAttribute(string id)
	{
		Id = id ?? throw new ArgumentNullException(nameof(id));
	}
}
