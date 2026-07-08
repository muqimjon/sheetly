using Sheetly.Core.Migration;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Core.Migrations;

public class ModelDiffer
{
	public List<MigrationOperation> GetDifferences(MigrationSnapshot? previous, MigrationSnapshot current, bool detectRenames = true)
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
				operations.AddRange(GetColumnDifferences(tableName, previousEntity, entity, detectRenames));
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
		EntitySchema current,
		bool detectRenames)
	{
		var renames = new List<MigrationOperation>();
		var alters = new List<MigrationOperation>();

		var previousColumns = previous.Columns.ToDictionary(c => c.Name);
		var currentColumns = current.Columns.ToDictionary(c => c.Name);

		var adds = new List<ColumnSchema>();
		foreach (var (columnName, column) in currentColumns)
		{
			if (!previousColumns.ContainsKey(columnName))
				adds.Add(column);
			else if (HasColumnChanged(previousColumns[columnName], column))
				alters.Add(AlterColumn(tableName, previousColumns[columnName], column));
		}

		var drops = previousColumns.Values.Where(c => !currentColumns.ContainsKey(c.Name)).ToList();

		if (detectRenames)
		{
			// An unmatched drop and add of the same type on a table is a rename — but only when it's
			// unambiguous (exactly one of each for that type); otherwise leave them as Drop+Add.
			foreach (var dataType in drops.Select(d => d.DataType).Distinct().ToList())
			{
				var typedDrops = drops.Where(d => d.DataType == dataType).ToList();
				var typedAdds = adds.Where(a => a.DataType == dataType).ToList();
				if (typedDrops.Count != 1 || typedAdds.Count != 1) continue;

				var drop = typedDrops[0];
				var add = typedAdds[0];
				renames.Add(new RenameColumnOperation { Table = tableName, Name = drop.Name, NewName = add.Name });
				if (HasColumnChanged(drop, add))
					alters.Add(AlterColumn(tableName, drop, add));
				drops.Remove(drop);
				adds.Remove(add);
			}
		}

		var operations = new List<MigrationOperation>();
		operations.AddRange(renames);
		operations.AddRange(alters);
		operations.AddRange(adds.Select(c => AddColumn(tableName, c)));
		operations.AddRange(drops.Select(c => new DropColumnOperation { Table = tableName, Name = c.Name }));
		return operations;
	}

	private static AddColumnOperation AddColumn(string tableName, ColumnSchema column) => new()
	{
		Table = tableName,
		Name = column.Name,
		ClrType = GetClrType(column.DataType),
		IsNullable = column.IsNullable,
		IsPrimaryKey = column.IsPrimaryKey,
		MaxLength = column.MaxLength,
		DefaultValue = column.DefaultValue,
		ForeignKeyTable = column.IsForeignKey ? column.ForeignKeyTable : null
	};

	private static AlterColumnOperation AlterColumn(string tableName, ColumnSchema prevCol, ColumnSchema column) => new()
	{
		Table = tableName,
		Name = column.Name,
		ClrType = column.DataType != prevCol.DataType ? GetClrType(column.DataType) : null,
		IsNullable = column.IsNullable != prevCol.IsNullable ? column.IsNullable : null,
		MaxLength = column.MaxLength != prevCol.MaxLength ? column.MaxLength : null,
		DefaultValue = !Equals(column.DefaultValue, prevCol.DefaultValue) ? column.DefaultValue : null,
		IsPrimaryKey = column.IsPrimaryKey != prevCol.IsPrimaryKey ? column.IsPrimaryKey : null,
		IsAutoIncrement = column.IsAutoIncrement != prevCol.IsAutoIncrement ? column.IsAutoIncrement : null,
		IsForeignKey = column.IsForeignKey != prevCol.IsForeignKey ? column.IsForeignKey : null,
		ForeignKeyTable = column.ForeignKeyTable != prevCol.ForeignKeyTable ? column.ForeignKeyTable : null
	};

	private static bool HasColumnChanged(ColumnSchema previous, ColumnSchema current)
	{
		return previous.DataType != current.DataType ||
			   previous.IsNullable != current.IsNullable ||
			   previous.MaxLength != current.MaxLength ||
			   !Equals(previous.DefaultValue, current.DefaultValue) ||
			   previous.IsPrimaryKey != current.IsPrimaryKey ||
			   previous.IsAutoIncrement != current.IsAutoIncrement ||
			   previous.IsForeignKey != current.IsForeignKey ||
			   previous.ForeignKeyTable != current.ForeignKeyTable;
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
