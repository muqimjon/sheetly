namespace Sheetly.Core.Migrations;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class MigrationAttribute(string id) : Attribute
{
	/// <summary>Format: yyyyMMddHHmmss_MigrationName</summary>
	public string Id { get; } = id ?? throw new ArgumentNullException(nameof(id));
}
