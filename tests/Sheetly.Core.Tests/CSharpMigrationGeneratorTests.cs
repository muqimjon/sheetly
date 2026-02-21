using Sheetly.Core.Migrations.Design;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Core.Tests;

public class CSharpMigrationGeneratorTests
{
	[Fact]
	public void GenerateMigration_ShouldGenerateValidClass()
	{
		// Arrange
		var generator = new CSharpMigrationGenerator();
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

		// Act
		var code = generator.GenerateMigration("InitialCreate", "20240101000000_InitialCreate", "MyProject.Migrations", operations);

		// Assert
		Assert.Contains("class InitialCreate : Migration", code);
		Assert.Contains("builder.CreateTable(\"Products\"", code);
		Assert.Contains(".Column<int>(\"Id\", c => c.IsPrimaryKey())", code);
		Assert.Contains(".Column<string>(\"Name\", c => c.IsRequired().HasMaxLength(100))", code);
	}
}
