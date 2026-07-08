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
	public void GetDifferences_ShouldDetectRename_WhenSingleColumnRenamedSameType()
	{
		var differ = new ModelDiffer();
		var previous = new MigrationSnapshot();
		previous.Entities["Users"] = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Id", DataType = "Int32", IsPrimaryKey = true },
				new ColumnSchema { Name = "Titel", DataType = "String" }
			}
		};
		var current = new MigrationSnapshot();
		current.Entities["Users"] = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Id", DataType = "Int32", IsPrimaryKey = true },
				new ColumnSchema { Name = "Title", DataType = "String" }
			}
		};

		var operations = differ.GetDifferences(previous, current);

		var rename = Assert.IsType<RenameColumnOperation>(Assert.Single(operations));
		Assert.Equal("Users", rename.Table);
		Assert.Equal("Titel", rename.Name);
		Assert.Equal("Title", rename.NewName);
	}

	[Fact]
	public void GetDifferences_ShouldNotDetectRename_WhenDisabled()
	{
		var differ = new ModelDiffer();
		var previous = new MigrationSnapshot();
		previous.Entities["Users"] = new EntitySchema
		{
			TableName = "Users",
			Columns = { new ColumnSchema { Name = "Titel", DataType = "String" } }
		};
		var current = new MigrationSnapshot();
		current.Entities["Users"] = new EntitySchema
		{
			TableName = "Users",
			Columns = { new ColumnSchema { Name = "Title", DataType = "String" } }
		};

		var operations = differ.GetDifferences(previous, current, detectRenames: false);

		Assert.Contains(operations, o => o is AddColumnOperation);
		Assert.Contains(operations, o => o is DropColumnOperation);
		Assert.DoesNotContain(operations, o => o is RenameColumnOperation);
	}

	[Fact]
	public void GetDifferences_ShouldNotDetectRename_WhenAmbiguous()
	{
		var differ = new ModelDiffer();
		var previous = new MigrationSnapshot();
		previous.Entities["Users"] = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "A", DataType = "String" },
				new ColumnSchema { Name = "B", DataType = "String" }
			}
		};
		var current = new MigrationSnapshot();
		current.Entities["Users"] = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "C", DataType = "String" },
				new ColumnSchema { Name = "D", DataType = "String" }
			}
		};

		var operations = differ.GetDifferences(previous, current);

		Assert.DoesNotContain(operations, o => o is RenameColumnOperation);
		Assert.Equal(2, operations.Count(o => o is AddColumnOperation));
		Assert.Equal(2, operations.Count(o => o is DropColumnOperation));
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
