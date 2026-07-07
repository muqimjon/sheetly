using Sheetly.Core.Migrations.Design;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Core.Tests;

public class CSharpMigrationGeneratorTests
{
	private readonly CSharpMigrationGenerator _generator = new();

	[Fact]
	public void GenerateMigration_ShouldGenerateValidClass()
	{
		var operations = new List<MigrationOperation>
		{
			new CreateTableOperation
			{
				Name = "Products",
				Columns =
				{
					new AddColumnOperation { Name = "Id", ClrType = typeof(int), IsPrimaryKey = true },
					new AddColumnOperation { Name = "Name", ClrType = typeof(string), IsNullable = false, MaxLength = 100 }
				}
			}
		};

		var code = _generator.GenerateMigration("InitialCreate", "20240101000000_InitialCreate", "MyProject.Migrations", operations);

		Assert.Contains("class InitialCreate : Migration", code);
		Assert.Contains("builder.CreateTable(\"Products\"", code);
		Assert.Contains(".Column<int>(\"Id\", c => c.IsPrimaryKey())", code);
		Assert.Contains(".Column<string>(\"Name\", c => c.IsRequired().HasMaxLength(100))", code);
	}

	[Fact]
	public void GenerateMigration_Namespace_AppearsVerbatim()
	{
		var code = _generator.GenerateMigration("Init", "20260101_Init", "test.Migrations", []);

		Assert.Contains("namespace test.Migrations;", code);
	}

	[Fact]
	public void GenerateMigration_Namespace_NeverStartsWithDot()
	{
		var code = _generator.GenerateMigration("Init", "20260101_Init", "test.Migrations", []);

		Assert.DoesNotContain("namespace .Migrations;", code);
	}

	[Fact]
	public void GenerateMigration_MigrationId_AppearsInAttribute()
	{
		var code = _generator.GenerateMigration("Init", "20260101000000_Init", "App.Migrations", []);

		Assert.Contains("[Migration(\"20260101000000_Init\")]", code);
	}

	[Fact]
	public void GenerateMigration_ClassName_SanitizesSpaces()
	{
		var code = _generator.GenerateMigration("Add Users Table", "20260101_AddUsersTable", "App.Migrations", []);

		Assert.Contains("class AddUsersTable : Migration", code);
	}

	[Fact]
	public void GenerateMigration_ClassName_PrefixesUnderscoreWhenStartsWithDigit()
	{
		var code = _generator.GenerateMigration("123Migration", "20260101_123Migration", "App.Migrations", []);

		Assert.Contains("class _123Migration : Migration", code);
	}

	[Fact]
	public void GenerateMigration_ClassName_RemovesSpecialChars()
	{
		var code = _generator.GenerateMigration("Add-Column@Table", "20260101_AddColumnTable", "App.Migrations", []);

		Assert.Contains("class AddColumnTable : Migration", code);
	}

	[Fact]
	public void GenerateMigration_TypeMappings_IntLongStringBoolDecimal()
	{
		var ops = new List<MigrationOperation>
		{
			new CreateTableOperation
			{
				Name = "T",
				Columns =
				{
					new AddColumnOperation { Name = "A", ClrType = typeof(int) },
					new AddColumnOperation { Name = "B", ClrType = typeof(long) },
					new AddColumnOperation { Name = "C", ClrType = typeof(string) },
					new AddColumnOperation { Name = "D", ClrType = typeof(bool) },
					new AddColumnOperation { Name = "E", ClrType = typeof(decimal) },
				}
			}
		};

		var code = _generator.GenerateMigration("Init", "id", "App.Migrations", ops);

		Assert.Contains(".Column<int>(", code);
		Assert.Contains(".Column<long>(", code);
		Assert.Contains(".Column<string>(", code);
		Assert.Contains(".Column<bool>(", code);
		Assert.Contains(".Column<decimal>(", code);
	}

	[Fact]
	public void GenerateMigration_TypeMappings_NullableTypes()
	{
		var ops = new List<MigrationOperation>
		{
			new CreateTableOperation
			{
				Name = "T",
				Columns =
				{
					new AddColumnOperation { Name = "A", ClrType = typeof(int?) },
					new AddColumnOperation { Name = "B", ClrType = typeof(Guid?) },
					new AddColumnOperation { Name = "C", ClrType = typeof(DateTime?) },
				}
			}
		};

		var code = _generator.GenerateMigration("Init", "id", "App.Migrations", ops);

		Assert.Contains(".Column<int?>(", code);
		Assert.Contains(".Column<Guid?>(", code);
		Assert.Contains(".Column<DateTime?>(", code);
	}

	[Fact]
	public void GenerateMigration_Column_AllModifiers()
	{
		var ops = new List<MigrationOperation>
		{
			new CreateTableOperation
			{
				Name = "T",
				Columns =
				{
					new AddColumnOperation
					{
						Name = "Code",
						ClrType = typeof(string),
						IsUnique = true,
						MaxLength = 50,
						ForeignKeyTable = "Categories",
						CheckConstraint = "LEN(Code)>0",
						Comment = "My comment"
					}
				}
			}
		};

		var code = _generator.GenerateMigration("Init", "id", "App.Migrations", ops);

		Assert.Contains(".IsUnique()", code);
		Assert.Contains(".HasMaxLength(50)", code);
		Assert.Contains(".IsForeignKey(\"Categories\")", code);
		Assert.Contains(".HasCheckConstraint(", code);
		Assert.Contains(".HasComment(\"My comment\")", code);
	}

	[Fact]
	public void GenerateMigration_Column_DefaultValueFormats()
	{
		var ops = new List<MigrationOperation>
		{
			new CreateTableOperation
			{
				Name = "T",
				Columns =
				{
					new AddColumnOperation { Name = "Price", ClrType = typeof(decimal), DefaultValue = 0m },
					new AddColumnOperation { Name = "Active", ClrType = typeof(bool), DefaultValue = true },
					new AddColumnOperation { Name = "Label", ClrType = typeof(string), DefaultValue = "N/A" },
				}
			}
		};

		var code = _generator.GenerateMigration("Init", "id", "App.Migrations", ops);

		Assert.Contains(".HasDefaultValue(0m)", code);
		Assert.Contains(".HasDefaultValue(true)", code);
		Assert.Contains(".HasDefaultValue(\"N/A\")", code);
	}

	[Fact]
	public void GenerateMigration_Down_ReversesCreateTableToDropTable()
	{
		var ops = new List<MigrationOperation>
		{
			new CreateTableOperation { Name = "Products", Columns = { new AddColumnOperation { Name = "Id", ClrType = typeof(int) } } }
		};

		var code = _generator.GenerateMigration("Init", "id", "App.Migrations", ops);

		Assert.Contains("void Down(MigrationBuilder builder)", code);
		Assert.Contains("builder.DropTable(\"Products\");", code);
	}

	[Fact]
	public void GenerateMigration_Down_ReversesAddColumnToDropColumn()
	{
		var ops = new List<MigrationOperation>
		{
			new AddColumnOperation { Table = "Products", Name = "Stock", ClrType = typeof(int) }
		};

		var code = _generator.GenerateMigration("AddStock", "id", "App.Migrations", ops);

		Assert.Contains("builder.DropColumn(\"Products\", \"Stock\");", code);
	}

	[Fact]
	public void GenerateMigration_DropTable_GeneratesCorrectUp()
	{
		var ops = new List<MigrationOperation>
		{
			new DropTableOperation { Name = "OldTable" }
		};

		var code = _generator.GenerateMigration("DropOld", "id", "App.Migrations", ops);

		Assert.Contains("builder.DropTable(\"OldTable\");", code);
	}

	[Fact]
	public void GenerateMigration_AddColumn_GeneratesStandaloneAddColumn()
	{
		var ops = new List<MigrationOperation>
		{
			new AddColumnOperation { Table = "Products", Name = "Stock", ClrType = typeof(int), IsNullable = false }
		};

		var code = _generator.GenerateMigration("AddStock", "id", "App.Migrations", ops);

		Assert.Contains("builder.AddColumn<int>(\"Products\", \"Stock\", c => c.IsRequired());", code);
	}

	[Fact]
	public void GenerateMigration_AlterColumn_GeneratesAlterColumn()
	{
		var ops = new List<MigrationOperation>
		{
			new AlterColumnOperation { Table = "Products", Name = "Name", ClrType = typeof(string), MaxLength = 200 }
		};

		var code = _generator.GenerateMigration("AlterName", "id", "App.Migrations", ops);

		Assert.Contains("builder.AlterColumn(\"Products\", \"Name\",", code);
		Assert.Contains(".HasMaxLength(200)", code);
	}

	[Fact]
	public void GenerateMigration_CreateIndex_GeneratesWithOptions()
	{
		var ops = new List<MigrationOperation>
		{
			new CreateIndexOperation { Name = "IX_Products_Name", Table = "Products", Columns = ["Name"], IsUnique = true }
		};

		var code = _generator.GenerateMigration("AddIdx", "id", "App.Migrations", ops);

		Assert.Contains("builder.CreateIndex(\"IX_Products_Name\", \"Products\", [\"Name\"]", code);
		Assert.Contains(".IsUnique()", code);
	}

	[Fact]
	public void GenerateMigration_ContainsBothUpAndDownMethods()
	{
		var code = _generator.GenerateMigration("Init", "id", "App.Migrations", []);

		Assert.Contains("public override void Up(MigrationBuilder builder)", code);
		Assert.Contains("public override void Down(MigrationBuilder builder)", code);
	}

	[Fact]
	public void GenerateMigration_EscapesSpecialCharsInCheckConstraint()
	{
		var ops = new List<MigrationOperation>
		{
			new CreateTableOperation
			{
				Name = "T",
				Columns =
				{
					new AddColumnOperation { Name = "N", ClrType = typeof(string), CheckConstraint = "N != \"bad\"" }
				}
			}
		};

		var code = _generator.GenerateMigration("Init", "id", "App.Migrations", ops);

		Assert.DoesNotContain(".HasCheckConstraint(\"N != \"bad\"\")", code);
		Assert.Contains(".HasCheckConstraint(\"N != \\\"bad\\\"\")", code);
	}

	// M7 — string default values with quotes/newlines/backslashes are escaped
	[Fact]
	public void GenerateMigration_EscapesNewlinesAndQuotesInDefaultValue()
	{
		var ops = new List<MigrationOperation>
		{
			new CreateTableOperation
			{
				Name = "T",
				Columns =
				{
					new AddColumnOperation { Name = "N", ClrType = typeof(string), DefaultValue = "line1\nO\"Brien\\x" }
				}
			}
		};

		var code = _generator.GenerateMigration("Init", "id", "App.Migrations", ops);

		Assert.DoesNotContain("O\"Brien", code);
		Assert.Contains("\\n", code);
		Assert.Contains("\\\"Brien", code);
		Assert.Contains("\\\\x", code);
	}

	// M7 — decimal defaults are emitted with an invariant separator regardless of culture
	[Fact]
	public void GenerateMigration_DecimalDefault_UsesInvariantSeparator()
	{
		var prev = System.Threading.Thread.CurrentThread.CurrentCulture;
		System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
		try
		{
			var ops = new List<MigrationOperation>
			{
				new CreateTableOperation
				{
					Name = "T",
					Columns = { new AddColumnOperation { Name = "Price", ClrType = typeof(decimal), DefaultValue = 1.5m } }
				}
			};

			var code = _generator.GenerateMigration("Init", "id", "App.Migrations", ops);

			Assert.Contains(".HasDefaultValue(1.5m)", code);
			Assert.DoesNotContain("1,5m", code);
		}
		finally
		{
			System.Threading.Thread.CurrentThread.CurrentCulture = prev;
		}
	}

	// M7 — a migration named after a C# keyword produces a compilable class name
	[Fact]
	public void GenerateMigration_KeywordName_GetsAtPrefix()
	{
		var code = _generator.GenerateMigration("class", "20260101_class", "App.Migrations", []);

		Assert.Contains("public partial class @class : Migration", code);
	}
}
