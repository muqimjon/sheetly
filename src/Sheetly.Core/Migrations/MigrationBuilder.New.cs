using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Core.Migrations;

/// <summary>
/// Fluent API for building migration operations.
/// Similar to Entity Framework's MigrationBuilder.
/// </summary>
public class MigrationBuilder
{
	private readonly List<MigrationOperation> _operations = new();

	/// <summary>
	/// Creates a new table with the specified name.
	/// </summary>
	/// <param name="name">The name of the table to create.</param>
	/// <param name="columns">Action to configure the table columns.</param>
	/// <returns>The migration builder for method chaining.</returns>
	public MigrationBuilder CreateTable(string name, Action<TableBuilder> columns)
	{
		var operation = new CreateTableOperation { Name = name };
		var tableBuilder = new TableBuilder(name, operation.Columns);
		columns(tableBuilder);
		_operations.Add(operation);
		return this;
	}

	/// <summary>
	/// Drops an existing table.
	/// </summary>
	/// <param name="name">The name of the table to drop.</param>
	/// <returns>The migration builder for method chaining.</returns>
	public MigrationBuilder DropTable(string name)
	{
		_operations.Add(new DropTableOperation { Name = name });
		return this;
	}

	/// <summary>
	/// Adds a column to an existing table.
	/// </summary>
	/// <typeparam name="T">The CLR type of the column.</typeparam>
	/// <param name="table">The table name.</param>
	/// <param name="name">The column name.</param>
	/// <param name="configure">Optional column configuration.</param>
	/// <returns>The migration builder for method chaining.</returns>
	public MigrationBuilder AddColumn<T>(string table, string name, Action<ColumnBuilder>? configure = null)
	{
		var operation = new AddColumnOperation
		{
			Table = table,
			Name = name,
			ClrType = typeof(T),
			IsNullable = IsNullableType(typeof(T))
		};

		if (configure != null)
		{
			var columnBuilder = new ColumnBuilder(operation);
			configure(columnBuilder);
		}

		_operations.Add(operation);
		return this;
	}

	/// <summary>
	/// Drops a column from an existing table.
	/// </summary>
	/// <param name="table">The table name.</param>
	/// <param name="name">The column name.</param>
	/// <returns>The migration builder for method chaining.</returns>
	public MigrationBuilder DropColumn(string table, string name)
	{
		_operations.Add(new DropColumnOperation { Table = table, Name = name });
		return this;
	}

	/// <summary>
	/// Alters an existing column.
	/// </summary>
	/// <param name="table">The table name.</param>
	/// <param name="name">The column name.</param>
	/// <param name="configure">Column alteration configuration.</param>
	/// <returns>The migration builder for method chaining.</returns>
	public MigrationBuilder AlterColumn(string table, string name, Action<AlterColumnBuilder> configure)
	{
		var operation = new AlterColumnOperation { Table = table, Name = name };
		var builder = new AlterColumnBuilder(operation);
		configure(builder);
		_operations.Add(operation);
		return this;
	}

	/// <summary>
	/// Creates an index on a table.
	/// </summary>
	/// <param name="name">The name of the index.</param>
	/// <param name="table">The table name.</param>
	/// <param name="columns">The column names to include in the index.</param>
	/// <param name="configure">Optional index configuration.</param>
	/// <returns>The migration builder for method chaining.</returns>
	public MigrationBuilder CreateIndex(string name, string table, string[] columns, Action<IndexBuilder>? configure = null)
	{
		var operation = new CreateIndexOperation
		{
			Name = name,
			Table = table,
			Columns = new List<string>(columns)
		};

		if (configure != null)
		{
			var builder = new IndexBuilder(operation);
			configure(builder);
		}

		_operations.Add(operation);
		return this;
	}

	/// <summary>
	/// Drops an index from a table.
	/// </summary>
	/// <param name="name">The name of the index.</param>
	/// <param name="table">The table name.</param>
	/// <returns>The migration builder for method chaining.</returns>
	public MigrationBuilder DropIndex(string name, string table)
	{
		_operations.Add(new DropIndexOperation { Name = name, Table = table });
		return this;
	}

	/// <summary>
	/// Adds a check constraint to a table.
	/// </summary>
	/// <param name="name">The name of the constraint.</param>
	/// <param name="table">The table name.</param>
	/// <param name="sql">The SQL expression for the check constraint.</param>
	/// <returns>The migration builder for method chaining.</returns>
	public MigrationBuilder AddCheckConstraint(string name, string table, string sql)
	{
		_operations.Add(new AddCheckConstraintOperation { Name = name, Table = table, Sql = sql });
		return this;
	}

	/// <summary>
	/// Drops a check constraint from a table.
	/// </summary>
	/// <param name="name">The name of the constraint.</param>
	/// <param name="table">The table name.</param>
	/// <returns>The migration builder for method chaining.</returns>
	public MigrationBuilder DropCheckConstraint(string name, string table)
	{
		_operations.Add(new DropCheckConstraintOperation { Name = name, Table = table });
		return this;
	}

	/// <summary>
	/// Gets all operations that have been added to this builder.
	/// </summary>
	public List<MigrationOperation> GetOperations() => _operations;

	private static bool IsNullableType(Type type)
	{
		return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
	}
}

/// <summary>
/// Builder for configuring a table's columns.
/// </summary>
public class TableBuilder
{
	private readonly string _tableName;
	private readonly List<AddColumnOperation> _columns;

	internal TableBuilder(string tableName, List<AddColumnOperation> columns)
	{
		_tableName = tableName;
		_columns = columns;
	}

	/// <summary>
	/// Adds a column to the table.
	/// </summary>
	/// <typeparam name="T">The CLR type of the column.</typeparam>
	/// <param name="name">The column name.</param>
	/// <param name="configure">Optional column configuration.</param>
	/// <returns>The table builder for method chaining.</returns>
	public TableBuilder Column<T>(string name, Action<ColumnBuilder>? configure = null)
	{
		var operation = new AddColumnOperation
		{
			Table = _tableName,
			Name = name,
			ClrType = typeof(T),
			IsNullable = IsNullableType(typeof(T))
		};

		if (configure != null)
		{
			var columnBuilder = new ColumnBuilder(operation);
			configure(columnBuilder);
		}

		_columns.Add(operation);
		return this;
	}

	private static bool IsNullableType(Type type)
	{
		return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
	}
}

/// <summary>
/// Builder for configuring a column.
/// </summary>
public class ColumnBuilder
{
	private readonly AddColumnOperation _operation;

	internal ColumnBuilder(AddColumnOperation operation)
	{
		_operation = operation;
	}

	/// <summary>
	/// Marks the column as required (not nullable).
	/// </summary>
	public ColumnBuilder IsRequired()
	{
		_operation.IsNullable = false;
		return this;
	}

	/// <summary>
	/// Marks the column as the primary key.
	/// </summary>
	public ColumnBuilder IsPrimaryKey()
	{
		_operation.IsPrimaryKey = true;
		_operation.IsNullable = false;
		return this;
	}

	/// <summary>
	/// Sets the maximum length for string columns.
	/// </summary>
	public ColumnBuilder HasMaxLength(int length)
	{
		_operation.MaxLength = length;
		return this;
	}

	/// <summary>
	/// Sets a default value for the column.
	/// </summary>
	public ColumnBuilder HasDefaultValue(object value)
	{
		_operation.DefaultValue = value;
		return this;
	}

	/// <summary>
	/// Marks the column as a foreign key.
	/// </summary>
	/// <param name="table">The related table name.</param>
	/// <param name="column">The related column name (default: "Id").</param>
	public ColumnBuilder IsForeignKey(string table, string column = "Id")
	{
		_operation.ForeignKeyTable = table;
		_operation.ForeignKeyColumn = column;
		return this;
	}

	/// <summary>
	/// Marks the column as unique.
	/// </summary>
	public ColumnBuilder IsUnique()
	{
		_operation.IsUnique = true;
		return this;
	}

	/// <summary>
	/// Sets a check constraint for the column.
	/// </summary>
	public ColumnBuilder HasCheckConstraint(string expression)
	{
		_operation.CheckConstraint = expression;
		return this;
	}

	/// <summary>
	/// Sets the precision and scale for decimal columns.
	/// </summary>
	public ColumnBuilder HasPrecision(int precision, int scale = 0)
	{
		_operation.Precision = precision;
		_operation.Scale = scale;
		return this;
	}

	/// <summary>
	/// Marks the column as a computed column.
	/// </summary>
	public ColumnBuilder HasComputedColumnSql(string sql, bool? stored = null)
	{
		_operation.IsComputed = true;
		_operation.ComputedColumnSql = sql;
		_operation.IsStored = stored;
		return this;
	}

	/// <summary>
	/// Marks the column as a concurrency token.
	/// </summary>
	public ColumnBuilder IsConcurrencyToken()
	{
		_operation.IsConcurrencyToken = true;
		return this;
	}

	/// <summary>
	/// Adds a comment to the column.
	/// </summary>
	public ColumnBuilder HasComment(string comment)
	{
		_operation.Comment = comment;
		return this;
	}
}

/// <summary>
/// Builder for altering a column.
/// </summary>
public class AlterColumnBuilder
{
	private readonly AlterColumnOperation _operation;

	internal AlterColumnBuilder(AlterColumnOperation operation)
	{
		_operation = operation;
	}

	/// <summary>
	/// Changes the column type.
	/// </summary>
	public AlterColumnBuilder HasType<T>()
	{
		_operation.ClrType = typeof(T);
		return this;
	}

	/// <summary>
	/// Changes the nullable setting.
	/// </summary>
	public AlterColumnBuilder IsNullable(bool nullable = true)
	{
		_operation.IsNullable = nullable;
		return this;
	}

	/// <summary>
	/// Changes the max length.
	/// </summary>
	public AlterColumnBuilder HasMaxLength(int length)
	{
		_operation.MaxLength = length;
		return this;
	}

	/// <summary>
	/// Changes the default value.
	/// </summary>
	public AlterColumnBuilder HasDefaultValue(object value)
	{
		_operation.DefaultValue = value;
		return this;
	}
}

/// <summary>
/// Builder for configuring an index.
/// </summary>
public class IndexBuilder
{
	private readonly CreateIndexOperation _operation;

	internal IndexBuilder(CreateIndexOperation operation)
	{
		_operation = operation;
	}

	/// <summary>
	/// Marks the index as unique.
	/// </summary>
	public IndexBuilder IsUnique()
	{
		_operation.IsUnique = true;
		return this;
	}

	/// <summary>
	/// Marks the index as clustered.
	/// </summary>
	public IndexBuilder IsClustered()
	{
		_operation.IsClustered = true;
		return this;
	}

	/// <summary>
	/// Adds a filter to the index.
	/// </summary>
	public IndexBuilder HasFilter(string filter)
	{
		_operation.Filter = filter;
		return this;
	}
}
