using Sheetly.Core.Migration;
using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Design;
using Sheetly.Core.Migrations.Operations;
using Sheetly.Core.Tests.Integration.Helpers;
using Sheetly.Google;

namespace Sheetly.Core.Tests;

/// <summary>
/// Phase 3: ModelDiffer structural-change detection, real Down() generation,
/// rename operations, and migration apply/rollback through a migration service.
/// </summary>
public class MigrationEngineTests
{
	private static MigrationSnapshot OneTable(string table, params ColumnSchema[] columns)
	{
		var snap = new MigrationSnapshot();
		snap.Entities[table] = new EntitySchema { TableName = table, Columns = columns.ToList() };
		return snap;
	}

	// 3.1 — turning a column into a primary key is detected as an AlterColumn
	[Fact]
	public void ModelDiffer_DetectsPrimaryKeyChange()
	{
		var previous = OneTable("Users",
			new ColumnSchema { Name = "Code", DataType = "String", IsPrimaryKey = false });
		var current = OneTable("Users",
			new ColumnSchema { Name = "Code", DataType = "String", IsPrimaryKey = true });

		var ops = new ModelDiffer().GetDifferences(previous, current);

		var alter = Assert.IsType<AlterColumnOperation>(Assert.Single(ops));
		Assert.True(alter.IsPrimaryKey);
	}

	// 3.1 — adding a foreign key is detected
	[Fact]
	public void ModelDiffer_DetectsForeignKeyChange()
	{
		var previous = OneTable("Products",
			new ColumnSchema { Name = "CategoryId", DataType = "Int32" });
		var current = OneTable("Products",
			new ColumnSchema { Name = "CategoryId", DataType = "Int32", IsForeignKey = true, ForeignKeyTable = "Categories" });

		var ops = new ModelDiffer().GetDifferences(previous, current);

		var alter = Assert.IsType<AlterColumnOperation>(Assert.Single(ops));
		Assert.True(alter.IsForeignKey);
		Assert.Equal("Categories", alter.ForeignKeyTable);
	}

	// 3.1 — generator emits the structural alter chain
	[Fact]
	public void Generator_AlterColumn_EmitsStructuralFlags()
	{
		var ops = new List<MigrationOperation>
		{
			new AlterColumnOperation { Table = "Users", Name = "Code", IsPrimaryKey = true, IsForeignKey = true, ForeignKeyTable = "Roles" }
		};

		var code = new CSharpMigrationGenerator().GenerateMigration("M", "id", "App.Migrations", ops);

		Assert.Contains(".IsPrimaryKey(true)", code);
		Assert.Contains(".IsForeignKey(\"Roles\")", code);
	}

	// 3.3 — explicit down operations produce a real Down() body
	[Fact]
	public void Generator_DownOperations_ProduceRealReverse()
	{
		var up = new List<MigrationOperation> { new DropColumnOperation { Table = "T", Name = "X" } };
		var down = new List<MigrationOperation> { new AddColumnOperation { Table = "T", Name = "X", ClrType = typeof(int) } };

		var code = new CSharpMigrationGenerator().GenerateMigration("M", "id", "App.Migrations", up, down);

		var downIdx = code.IndexOf("void Down(");
		Assert.Contains("builder.AddColumn<int>(\"T\", \"X\"", code[downIdx..]);
		Assert.DoesNotContain("// TODO", code);
	}

	// 3.2 — rename operations generate up + reversed down
	[Fact]
	public void Generator_RenameColumn_EmitsAndReverses()
	{
		var ops = new List<MigrationOperation> { new RenameColumnOperation { Table = "T", Name = "Old", NewName = "New" } };

		var code = new CSharpMigrationGenerator().GenerateMigration("M", "id", "App.Migrations", ops);

		Assert.Contains("builder.RenameColumn(\"T\", \"Old\", \"New\");", code);
		Assert.Contains("builder.RenameColumn(\"T\", \"New\", \"Old\");", code); // down
	}

	// 3.4 — apply then revert a migration through the service
	[Fact]
	public async Task MigrationService_ApplyThenRevert_CreateTable()
	{
		var provider = new InMemorySheetsProvider();
		var svc = new GoogleMigrationService(provider);

		var up = new MigrationBuilder();
		up.CreateTable("Products", t => t
			.Column<int>("Id", c => c.IsPrimaryKey())
			.Column<string>("Name"));
		await svc.ApplyMigrationAsync(up.GetOperations(), "20240101_Init");

		Assert.True(await provider.SheetExistsAsync("Products"));
		Assert.Contains("20240101_Init", await svc.GetAppliedMigrationsAsync());

		var down = new MigrationBuilder();
		down.DropTable("Products");
		await svc.RevertMigrationAsync(down.GetOperations(), "20240101_Init");

		Assert.False(await provider.SheetExistsAsync("Products"));
		Assert.Empty(await svc.GetAppliedMigrationsAsync());
	}

	// 3.2 — rename column is applied to the data sheet header
	[Fact]
	public async Task MigrationService_RenameColumn_UpdatesHeader()
	{
		var provider = new InMemorySheetsProvider();
		var svc = new GoogleMigrationService(provider);

		var up = new MigrationBuilder();
		up.CreateTable("Products", t => t
			.Column<int>("Id", c => c.IsPrimaryKey())
			.Column<string>("Name"));
		await svc.ApplyMigrationAsync(up.GetOperations(), "20240101_Init");

		var rename = new MigrationBuilder();
		rename.RenameColumn("Products", "Name", "Title");
		await svc.ApplyMigrationAsync(rename.GetOperations(), "20240102_Rename");

		var rows = await provider.GetAllRowsAsync("Products");
		var headers = rows[0].Select(h => h?.ToString()).ToList();
		Assert.Contains("Title", headers);
		Assert.DoesNotContain("Name", headers);
	}

	// #1 — SchemaReader reconstructs the model from the applied __SheetlySchema__
	[Fact]
	public async Task SchemaReader_RoundTripsAppliedSchema()
	{
		var provider = new InMemorySheetsProvider();
		var svc = new GoogleMigrationService(provider);

		var up = new MigrationBuilder();
		up.CreateTable("Products", t => t
			.Column<int>("Id", c => c.IsPrimaryKey())
			.Column<string>("Name", c => c.IsRequired().HasMaxLength(100))
			.Column<int>("CategoryId", c => c.IsForeignKey("Categories")));
		await svc.ApplyMigrationAsync(up.GetOperations(), "20240101_Init");

		var snap = await SchemaReader.ReadAsync(provider);

		Assert.True(snap.Entities.ContainsKey("Products"));
		var cols = snap.Entities["Products"].Columns;
		Assert.Contains(cols, c => c.Name == "Id" && c.IsPrimaryKey);
		Assert.Contains(cols, c => c.Name == "Name" && c.MaxLength == 100);
		Assert.Contains(cols, c => c.Name == "CategoryId" && c.IsForeignKey && c.ForeignKeyTable == "Categories");
	}

	// 3.2 — rename table renames the underlying sheet
	[Fact]
	public async Task MigrationService_RenameTable_RenamesSheet()
	{
		var provider = new InMemorySheetsProvider();
		var svc = new GoogleMigrationService(provider);

		var up = new MigrationBuilder();
		up.CreateTable("Products", t => t.Column<int>("Id", c => c.IsPrimaryKey()));
		await svc.ApplyMigrationAsync(up.GetOperations(), "20240101_Init");

		var rename = new MigrationBuilder();
		rename.RenameTable("Products", "Items");
		await svc.ApplyMigrationAsync(rename.GetOperations(), "20240102_Rename");

		Assert.True(await provider.SheetExistsAsync("Items"));
		Assert.False(await provider.SheetExistsAsync("Products"));
	}
}
