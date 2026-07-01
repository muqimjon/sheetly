using Sheetly.Core.Migration;
using Sheetly.Core.Migrations.Design;

namespace Sheetly.Core.Tests;

public class ModelSnapshotGeneratorTests
{
	private readonly ModelSnapshotGenerator _generator = new();

	private static MigrationSnapshot SimpleSnapshot(string tableName = "Products", string className = "Product") =>
		new()
		{
			ModelHash = "abc123",
			Version = "1.1.1",
			LastUpdated = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
			Entities = new Dictionary<string, EntitySchema>
			{
				[tableName] = new EntitySchema
				{
					TableName = tableName,
					ClassName = className,
					Namespace = "App.Models",
					Columns =
					[
						new ColumnSchema { Name = "Id", PropertyName = "Id", DataType = "Int64", IsPrimaryKey = true, IsAutoIncrement = true },
						new ColumnSchema { Name = "Name", PropertyName = "Name", DataType = "String", IsRequired = true, MaxLength = 100 }
					],
					Relationships = []
				}
			}
		};

	[Fact]
	public void GenerateModelSnapshot_NamespaceAppearsCorrectly()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "test.Migrations", "AppDb");

		Assert.Contains("namespace test.Migrations;", code);
	}

	[Fact]
	public void GenerateModelSnapshot_Namespace_NeverStartsWithDot()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "test.Migrations", "AppDb");

		Assert.DoesNotContain("namespace .Migrations;", code);
	}

	[Fact]
	public void GenerateModelSnapshot_ClassNameIncludesContextName()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "App.Migrations", "AppDb");

		Assert.Contains("class AppDbModelSnapshot : MigrationSnapshot", code);
	}

	[Fact]
	public void GenerateModelSnapshot_NoInlineEntityComments()
	{
		var snapshot = SimpleSnapshot("Products", "Product");
		var code = _generator.GenerateModelSnapshot(snapshot, "App.Migrations", "AppDb");

		Assert.DoesNotContain("// Product", code);
	}

	[Fact]
	public void GenerateModelSnapshot_TableNamePreserved()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot("Orders", "Order"), "App.Migrations", "AppDb");

		Assert.Contains("TableName = \"Orders\"", code);
	}

	[Fact]
	public void GenerateModelSnapshot_ClassNamePreserved()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot("Orders", "Order"), "App.Migrations", "AppDb");

		Assert.Contains("ClassName = \"Order\"", code);
	}

	[Fact]
	public void GenerateModelSnapshot_ColumnNamePreserved()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "App.Migrations", "AppDb");

		Assert.Contains("Name = \"Id\"", code);
		Assert.Contains("Name = \"Name\"", code);
	}

	[Fact]
	public void GenerateModelSnapshot_PrimaryKeyFlagPreserved()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "App.Migrations", "AppDb");

		Assert.Contains("IsPrimaryKey = true", code);
	}

	[Fact]
	public void GenerateModelSnapshot_IsRequiredPreserved()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "App.Migrations", "AppDb");

		Assert.Contains("IsRequired = true", code);
	}

	[Fact]
	public void GenerateModelSnapshot_MaxLengthPreserved()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "App.Migrations", "AppDb");

		Assert.Contains("MaxLength = 100", code);
	}

	[Fact]
	public void GenerateModelSnapshot_ModelHashPreserved()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "App.Migrations", "AppDb");

		Assert.Contains("ModelHash = \"abc123\"", code);
	}

	[Fact]
	public void GenerateModelSnapshot_VersionPreserved()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "App.Migrations", "AppDb");

		Assert.Contains("Version = \"1.1.1\"", code);
	}

	[Fact]
	public void GenerateModelSnapshot_ForeignKeyDataPreserved()
	{
		var snapshot = new MigrationSnapshot
		{
			Entities = new Dictionary<string, EntitySchema>
			{
				["Orders"] = new EntitySchema
				{
					TableName = "Orders",
					ClassName = "Order",
					Columns =
					[
						new ColumnSchema { Name = "Id", PropertyName = "Id", DataType = "Int64", IsPrimaryKey = true },
						new ColumnSchema
						{
							Name = "CategoryId", PropertyName = "CategoryId", DataType = "Int64",
							IsForeignKey = true, ForeignKeyTable = "Categories", ForeignKeyColumn = "Id"
						}
					],
					Relationships = []
				}
			}
		};

		var code = _generator.GenerateModelSnapshot(snapshot, "App.Migrations", "AppDb");

		Assert.Contains("IsForeignKey = true", code);
		Assert.Contains("ForeignKeyTable = \"Categories\"", code);
		Assert.Contains("ForeignKeyColumn = \"Id\"", code);
	}

	[Fact]
	public void GenerateModelSnapshot_MultipleEntitiesAllAppear()
	{
		var snapshot = new MigrationSnapshot
		{
			Entities = new Dictionary<string, EntitySchema>
			{
				["Products"] = new EntitySchema { TableName = "Products", ClassName = "Product", Columns = [], Relationships = [] },
				["Categories"] = new EntitySchema { TableName = "Categories", ClassName = "Category", Columns = [], Relationships = [] }
			}
		};

		var code = _generator.GenerateModelSnapshot(snapshot, "App.Migrations", "AppDb");

		Assert.Contains("TableName = \"Products\"", code);
		Assert.Contains("TableName = \"Categories\"", code);
	}

	[Fact]
	public void GenerateModelSnapshot_EmptyEntities_DoesNotThrow()
	{
		var snapshot = new MigrationSnapshot { Entities = [] };
		var ex = Record.Exception(() => _generator.GenerateModelSnapshot(snapshot, "App.Migrations", "AppDb"));

		Assert.Null(ex);
	}

	[Fact]
	public void GenerateModelSnapshot_CheckConstraintEscaped()
	{
		var snapshot = new MigrationSnapshot
		{
			Entities = new Dictionary<string, EntitySchema>
			{
				["T"] = new EntitySchema
				{
					TableName = "T",
					ClassName = "T",
					Columns =
					[
						new ColumnSchema { Name = "N", PropertyName = "N", DataType = "String", CheckConstraint = "N != \"bad\"" }
					],
					Relationships = []
				}
			}
		};

		var code = _generator.GenerateModelSnapshot(snapshot, "App.Migrations", "AppDb");

		Assert.DoesNotContain("CheckConstraint = \"N != \"bad\"\"", code);
		Assert.Contains("CheckConstraint = \"N != \\\"bad\\\"\"", code);
	}

	[Fact]
	public void GenerateModelSnapshot_IsAutoIncrementPreserved()
	{
		var code = _generator.GenerateModelSnapshot(SimpleSnapshot(), "App.Migrations", "AppDb");

		Assert.Contains("IsAutoIncrement = true", code);
	}
}
