namespace Sheetly.Core.Migrations;

public abstract class Migration
{
	public abstract void Up(MigrationBuilder builder);

	public abstract void Down(MigrationBuilder builder);
}
