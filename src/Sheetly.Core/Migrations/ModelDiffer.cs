using Sheetly.Core.Migration;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Core.Migrations;

public class ModelDiffer
{
	public List<MigrationOperation> GetDifferences(MigrationSnapshot? previous, MigrationSnapshot current)
	{
		var operations = new List<MigrationOperation>();

		var previousEntities = previous?.Entities ?? new Dictionary<string, EntitySchema>();
		var currentEntities = current.Entities;

		foreach (var (tableName, entity) in currentEntities)
		{
			if (!previousEntities.ContainsKey(tableName))
			{
				operations.Add(CreateTableOperation(entity));
			}
			else
			{
				var previousEntity = previousEntities[tableName];
				operations.AddRange(GetColumnDifferences(tableName, previousEntity, entity));
			}
		}

		foreach (var (tableName, _) in previousEntities)
		{
			if (!currentEntities.ContainsKey(tableName))
			{
				operations.Add(new DropTableOperation { Name = tableName });
			}
		}

		return operations;
	}

	private static CreateTableOperation CreateTableOperation(EntitySchema entity)
	{
		var operation = new CreateTableOperation
		{
			Name = entity.TableName,
			ClassName = entity.ClassName
		};

		foreach (var column in entity.Columns)
		{
			operation.Columns.Add(new AddColumnOperation
			{
				Table = entity.TableName,
				Name = column.Name,
				ClrType = GetClrType(column.DataType),
				IsNullable = column.IsNullable,
				IsPrimaryKey = column.IsPrimaryKey,
				IsUnique = column.IsPrimaryKey || column.IsUnique,
				IsAutoIncrement = column.IsAutoIncrement,
				MaxLength = column.MaxLength,
				DefaultValue = column.DefaultValue,
				ForeignKeyTable = column.IsForeignKey ? column.ForeignKeyTable : null
			});
		}

		return operation;
	}

	private static IEnumerable<MigrationOperation> GetColumnDifferences(
		string tableName,
		EntitySchema previous,
		EntitySchema current)
	{
		var operations = new List<MigrationOperation>();

		var previousColumns = previous.Columns.ToDictionary(c => c.Name);
		var currentColumns = current.Columns.ToDictionary(c => c.Name);

		foreach (var (columnName, column) in currentColumns)
		{
			if (!previousColumns.ContainsKey(columnName))
			{
				operations.Add(new AddColumnOperation
				{
					Table = tableName,
					Name = column.Name,
					ClrType = GetClrType(column.DataType),
					IsNullable = column.IsNullable,
					IsPrimaryKey = column.IsPrimaryKey,
					MaxLength = column.MaxLength,
					DefaultValue = column.DefaultValue,
					ForeignKeyTable = column.IsForeignKey ? column.ForeignKeyTable : null
				});
			}
			else
			{
				var prevCol = previousColumns[columnName];
				if (HasColumnChanged(prevCol, column))
				{
					operations.Add(new AlterColumnOperation
					{
						Table = tableName,
						Name = columnName,
						ClrType = column.DataType != prevCol.DataType ? GetClrType(column.DataType) : null,
						IsNullable = column.IsNullable != prevCol.IsNullable ? column.IsNullable : null,
						MaxLength = column.MaxLength != prevCol.MaxLength ? column.MaxLength : null,
						DefaultValue = !Equals(column.DefaultValue, prevCol.DefaultValue) ? column.DefaultValue : null
					});
				}
			}
		}

		foreach (var (columnName, _) in previousColumns)
		{
			if (!currentColumns.ContainsKey(columnName))
			{
				operations.Add(new DropColumnOperation
				{
					Table = tableName,
					Name = columnName
				});
			}
		}

		return operations;
	}

	private static bool HasColumnChanged(ColumnSchema previous, ColumnSchema current)
	{
		return previous.DataType != current.DataType ||
			   previous.IsNullable != current.IsNullable ||
			   previous.MaxLength != current.MaxLength ||
			   !Equals(previous.DefaultValue, current.DefaultValue);
	}

	private static Type GetClrType(string typeName)
	{
		return typeName switch
		{
			"Int32" => typeof(int),
			"Int64" => typeof(long),
			"String" => typeof(string),
			"Boolean" => typeof(bool),
			"Decimal" => typeof(decimal),
			"Double" => typeof(double),
			"Single" => typeof(float),
			"DateTime" => typeof(DateTime),
			"DateTimeOffset" => typeof(DateTimeOffset),
			"Guid" => typeof(Guid),
			"Byte" => typeof(byte),
			"Int16" => typeof(short),
			_ => typeof(string)
		};
	}
}
