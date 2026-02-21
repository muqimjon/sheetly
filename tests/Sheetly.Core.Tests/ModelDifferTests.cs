using Sheetly.Core.Migration;
using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Core.Tests;

public class ModelDifferTests
{
	[Fact]
	public void GetDifferences_ShouldReturnCreateTable_WhenTableIsNew()
	{
		// Arrange
		var differ = new ModelDiffer();
		var current = new MigrationSnapshot();
		var entity = new EntitySchema
		{
			TableName = "Users",
			Columns = { new ColumnSchema { Name = "Id", DataType = "Int32", IsPrimaryKey = true } }
		};
		current.Entities["Users"] = entity;

		// Act
		var operations = differ.GetDifferences(null, current);

		// Assert
		Assert.Single(operations);
		var createTable = Assert.IsType<CreateTableOperation>(operations[0]);
		Assert.Equal("Users", createTable.Name);
		Assert.Single(createTable.Columns);
		Assert.Equal("Id", createTable.Columns[0].Name);
	}

	[Fact]
	public void GetDifferences_ShouldReturnAddColumn_WhenColumnIsAdded()
	{
		// Arrange
		var differ = new ModelDiffer();

		var previous = new MigrationSnapshot();
		previous.Entities["Users"] = new EntitySchema
		{
			TableName = "Users",
			Columns = { new ColumnSchema { Name = "Id", DataType = "Int32", IsPrimaryKey = true } }
		};

		var current = new MigrationSnapshot();
		current.Entities["Users"] = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Id", DataType = "Int32", IsPrimaryKey = true },
				new ColumnSchema { Name = "Email", DataType = "String" }
			}
		};

		// Act
		var operations = differ.GetDifferences(previous, current);

		// Assert
		Assert.Single(operations);
		var addColumn = Assert.IsType<AddColumnOperation>(operations[0]);
		Assert.Equal("Users", addColumn.Table);
		Assert.Equal("Email", addColumn.Name);
		Assert.Equal(typeof(string), addColumn.ClrType);
	}

	[Fact]
	public void GetDifferences_ShouldReturnDropTable_WhenTableIsRemoved()
	{
		// Arrange
		var differ = new ModelDiffer();

		var previous = new MigrationSnapshot();
		previous.Entities["Users"] = new EntitySchema { TableName = "Users" };

		var current = new MigrationSnapshot(); // No entities

		// Act
		var operations = differ.GetDifferences(previous, current);

		// Assert
		Assert.Single(operations);
		var dropTable = Assert.IsType<DropTableOperation>(operations[0]);
		Assert.Equal("Users", dropTable.Name);
	}
}
