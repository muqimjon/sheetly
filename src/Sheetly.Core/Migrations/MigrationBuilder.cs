using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Core.Migrations;

public class MigrationBuilder
{
	private readonly List<MigrationOperation> _operations = new();

	public MigrationBuilder CreateTable(string name, Action<TableBuilder> columns)
	{
		var operation = new CreateTableOperation { Name = name };
		var tableBuilder = new TableBuilder(name, operation.Columns);
		columns(tableBuilder);
		_operations.Add(operation);
		return this;
	}

	public MigrationBuilder DropTable(string name)
	{
		_operations.Add(new DropTableOperation { Name = name });
		return this;
	}

	public MigrationBuilder AddColumn<T>(string table, string name, Action<ColumnBuilder>? configure = null)
	{
		var operation = new AddColumnOperation
		{
			Table = table,
			Name = name,
			ClrType = typeof(T),
			IsNullable = IsNullableType(typeof(T))
		};

		if (configure is not null)
		{
			var columnBuilder = new ColumnBuilder(operation);
			configure(columnBuilder);
		}

		_operations.Add(operation);
		return this;
	}

	public MigrationBuilder DropColumn(string table, string name)
	{
		_operations.Add(new DropColumnOperation { Table = table, Name = name });
		return this;
	}

	public MigrationBuilder AlterColumn(string table, string name, Action<AlterColumnBuilder> configure)
	{
		var operation = new AlterColumnOperation { Table = table, Name = name };
		var builder = new AlterColumnBuilder(operation);
		configure(builder);
		_operations.Add(operation);
		return this;
	}

	public MigrationBuilder CreateIndex(string name, string table, string[] columns, Action<IndexBuilder>? configure = null)
	{
		var operation = new CreateIndexOperation
		{
			Name = name,
			Table = table,
			Columns = new List<string>(columns)
		};

		if (configure is not null)
		{
			var builder = new IndexBuilder(operation);
			configure(builder);
		}

		_operations.Add(operation);
		return this;
	}

	public MigrationBuilder DropIndex(string name, string table)
	{
		_operations.Add(new DropIndexOperation { Name = name, Table = table });
		return this;
	}

	public MigrationBuilder AddCheckConstraint(string name, string table, string sql)
	{
		_operations.Add(new AddCheckConstraintOperation { Name = name, Table = table, Sql = sql });
		return this;
	}

	public MigrationBuilder DropCheckConstraint(string name, string table)
	{
		_operations.Add(new DropCheckConstraintOperation { Name = name, Table = table });
		return this;
	}

	public List<MigrationOperation> GetOperations() => _operations;

	private static bool IsNullableType(Type type)
	{
		return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
	}
}

public class TableBuilder
{
	private readonly string _tableName;
	private readonly List<AddColumnOperation> _columns;

	internal TableBuilder(string tableName, List<AddColumnOperation> columns)
	{
		_tableName = tableName;
		_columns = columns;
	}

	public TableBuilder Column<T>(string name, Action<ColumnBuilder>? configure = null)
	{
		var operation = new AddColumnOperation
		{
			Table = _tableName,
			Name = name,
			ClrType = typeof(T),
			IsNullable = IsNullableType(typeof(T))
		};

		if (configure is not null)
		{
			var columnBuilder = new ColumnBuilder(operation);
			configure(columnBuilder);
		}

		_columns.Add(operation);
		return this;
	}

	private static bool IsNullableType(Type type)
	{
		return !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
	}
}

public class ColumnBuilder
{
	private readonly AddColumnOperation _operation;

	internal ColumnBuilder(AddColumnOperation operation)
	{
		_operation = operation;
	}

	public ColumnBuilder IsRequired()
	{
		_operation.IsNullable = false;
		return this;
	}

	public ColumnBuilder IsPrimaryKey()
	{
		_operation.IsPrimaryKey = true;
		_operation.IsNullable = false;
		return this;
	}

	public ColumnBuilder HasMaxLength(int length)
	{
		_operation.MaxLength = length;
		return this;
	}

	public ColumnBuilder HasDefaultValue(object value)
	{
		_operation.DefaultValue = value;
		return this;
	}

	public ColumnBuilder IsForeignKey(string table, string column = "Id")
	{
		_operation.ForeignKeyTable = table;
		_operation.ForeignKeyColumn = column;
		return this;
	}

	public ColumnBuilder IsUnique()
	{
		_operation.IsUnique = true;
		return this;
	}

	public ColumnBuilder HasCheckConstraint(string expression)
	{
		_operation.CheckConstraint = expression;
		return this;
	}

	public ColumnBuilder HasPrecision(int precision, int scale = 0)
	{
		_operation.Precision = precision;
		_operation.Scale = scale;
		return this;
	}

	public ColumnBuilder HasComputedColumnSql(string sql, bool? stored = null)
	{
		_operation.IsComputed = true;
		_operation.ComputedColumnSql = sql;
		_operation.IsStored = stored;
		return this;
	}

	public ColumnBuilder IsConcurrencyToken()
	{
		_operation.IsConcurrencyToken = true;
		return this;
	}

	public ColumnBuilder HasComment(string comment)
	{
		_operation.Comment = comment;
		return this;
	}
}

public class AlterColumnBuilder
{
	private readonly AlterColumnOperation _operation;

	internal AlterColumnBuilder(AlterColumnOperation operation)
	{
		_operation = operation;
	}

	public AlterColumnBuilder HasType<T>()
	{
		_operation.ClrType = typeof(T);
		return this;
	}

	public AlterColumnBuilder IsNullable(bool nullable = true)
	{
		_operation.IsNullable = nullable;
		return this;
	}

	public AlterColumnBuilder HasMaxLength(int length)
	{
		_operation.MaxLength = length;
		return this;
	}

	public AlterColumnBuilder HasDefaultValue(object value)
	{
		_operation.DefaultValue = value;
		return this;
	}
}

public class IndexBuilder
{
	private readonly CreateIndexOperation _operation;

	internal IndexBuilder(CreateIndexOperation operation)
	{
		_operation = operation;
	}

	public IndexBuilder IsUnique()
	{
		_operation.IsUnique = true;
		return this;
	}

	public IndexBuilder IsClustered()
	{
		_operation.IsClustered = true;
		return this;
	}

	public IndexBuilder HasFilter(string filter)
	{
		_operation.Filter = filter;
		return this;
	}
}
