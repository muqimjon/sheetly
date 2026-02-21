using Sheetly.Core.Migrations.Operations;
using System.Text;

namespace Sheetly.Core.Migrations.Design;

/// <summary>
/// Generates C# migration files from migration operations.
/// Similar to Entity Framework's migration scaffolding.
/// </summary>
public class CSharpMigrationGenerator
{
	private const string Indent = "    ";

	/// <summary>
	/// Generates a C# migration file from the specified operations.
	/// </summary>
	/// <param name="migrationName">The name of the migration.</param>
	/// <param name="migrationId">The unique migration ID (timestamp_name format).</param>
	/// <param name="targetNamespace">The namespace for the migration class.</param>
	/// <param name="operations">The operations to include in the migration.</param>
	/// <returns>The generated C# code as a string.</returns>
	public string GenerateMigration(
		string migrationName,
		string migrationId,
		string targetNamespace,
		List<MigrationOperation> operations)
	{
		var sb = new StringBuilder();

		// Using statements
		sb.AppendLine("using Sheetly.Core.Migrations;");
		sb.AppendLine("using Sheetly.Core.Migrations.Operations;");
		sb.AppendLine();

		// Namespace
		sb.AppendLine($"namespace {targetNamespace};");
		sb.AppendLine();

		// Migration attribute
		sb.AppendLine($"[Migration(\"{migrationId}\")]");
		sb.AppendLine($"public partial class {SanitizeClassName(migrationName)} : Migration");
		sb.AppendLine("{");

		// Up method
		sb.AppendLine($"{Indent}public override void Up(MigrationBuilder builder)");
		sb.AppendLine($"{Indent}{{");
		GenerateOperations(sb, operations, Indent + Indent);
		sb.AppendLine($"{Indent}}}");
		sb.AppendLine();

		// Down method
		sb.AppendLine($"{Indent}public override void Down(MigrationBuilder builder)");
		sb.AppendLine($"{Indent}{{");
		GenerateReverseOperations(sb, operations, Indent + Indent);
		sb.AppendLine($"{Indent}}}");

		sb.AppendLine("}");

		return sb.ToString();
	}

	private void GenerateOperations(StringBuilder sb, List<MigrationOperation> operations, string indent)
	{
		foreach (var operation in operations)
		{
			switch (operation)
			{
				case CreateTableOperation createTable:
					GenerateCreateTable(sb, createTable, indent);
					break;
				case DropTableOperation dropTable:
					sb.AppendLine($"{indent}builder.DropTable(\"{dropTable.Name}\");");
					sb.AppendLine();
					break;
				case AddColumnOperation addColumn:
					GenerateAddColumn(sb, addColumn, indent);
					break;
				case DropColumnOperation dropColumn:
					sb.AppendLine($"{indent}builder.DropColumn(\"{dropColumn.Table}\", \"{dropColumn.Name}\");");
					sb.AppendLine();
					break;
				case AlterColumnOperation alterColumn:
					GenerateAlterColumn(sb, alterColumn, indent);
					break;
				case CreateIndexOperation createIndex:
					GenerateCreateIndex(sb, createIndex, indent);
					break;
				case DropIndexOperation dropIndex:
					sb.AppendLine($"{indent}builder.DropIndex(\"{dropIndex.Name}\", \"{dropIndex.Table}\");");
					sb.AppendLine();
					break;
				case AddCheckConstraintOperation addCheck:
					sb.AppendLine($"{indent}builder.AddCheckConstraint(\"{addCheck.Name}\", \"{addCheck.Table}\", \"{addCheck.Sql}\");");
					sb.AppendLine();
					break;
				case DropCheckConstraintOperation dropCheck:
					sb.AppendLine($"{indent}builder.DropCheckConstraint(\"{dropCheck.Name}\", \"{dropCheck.Table}\");");
					sb.AppendLine();
					break;
			}
		}
	}

	private void GenerateCreateTable(StringBuilder sb, CreateTableOperation operation, string indent)
	{
		// Add ClassName as comment for scaffolding support (Sheetly-specific)
		if (!string.IsNullOrEmpty(operation.ClassName))
		{
			sb.AppendLine($"{indent}// ClassName: {operation.ClassName}");
		}

		sb.AppendLine($"{indent}builder.CreateTable(\"{operation.Name}\", table => table");

		for (int i = 0; i < operation.Columns.Count; i++)
		{
			var column = operation.Columns[i];
			var isLast = i == operation.Columns.Count - 1;
			GenerateColumn(sb, column, indent + Indent, isLast);
		}

		sb.AppendLine($"{indent});");
		sb.AppendLine();
	}

	private void GenerateColumn(StringBuilder sb, AddColumnOperation column, string indent, bool isLast)
	{
		var typeName = GetTypeName(column.ClrType);
		var chain = new List<string>();

		// Build fluent chain - order matters for readability
		if (column.IsPrimaryKey)
			chain.Add(".IsPrimaryKey()");
		else if (!column.IsNullable)
			chain.Add(".IsRequired()");

		if (column.IsUnique)
			chain.Add(".IsUnique()");

		if (column.MaxLength.HasValue)
			chain.Add($".HasMaxLength({column.MaxLength.Value})");

		if (column.Precision.HasValue)
		{
			if (column.Scale.HasValue)
				chain.Add($".HasPrecision({column.Precision.Value}, {column.Scale.Value})");
			else
				chain.Add($".HasPrecision({column.Precision.Value})");
		}

		if (column.DefaultValue != null)
			chain.Add($".HasDefaultValue({FormatValue(column.DefaultValue)})");

		if (!string.IsNullOrEmpty(column.CheckConstraint))
			chain.Add($".HasCheckConstraint(\"{EscapeString(column.CheckConstraint)}\")");

		if (!string.IsNullOrEmpty(column.ForeignKeyTable))
			chain.Add($".IsForeignKey(\"{column.ForeignKeyTable}\")");

		if (column.IsConcurrencyToken)
			chain.Add(".IsConcurrencyToken()");

		if (column.IsComputed && !string.IsNullOrEmpty(column.ComputedColumnSql))
		{
			var storedParam = column.IsStored.HasValue ? $", {column.IsStored.Value.ToString().ToLower()}" : "";
			chain.Add($".HasComputedColumnSql(\"{EscapeString(column.ComputedColumnSql)}\"{storedParam})");
		}

		if (!string.IsNullOrEmpty(column.Comment))
			chain.Add($".HasComment(\"{EscapeString(column.Comment)}\")");

		var chainStr = string.Join("", chain);

		if (chain.Count > 0)
		{
			if (isLast)
				sb.AppendLine($"{indent}.Column<{typeName}>(\"{column.Name}\", c => c{chainStr})");
			else
				sb.AppendLine($"{indent}.Column<{typeName}>(\"{column.Name}\", c => c{chainStr})");
		}
		else
		{
			if (isLast)
				sb.AppendLine($"{indent}.Column<{typeName}>(\"{column.Name}\")");
			else
				sb.AppendLine($"{indent}.Column<{typeName}>(\"{column.Name}\")");
		}
	}

	private void GenerateAddColumn(StringBuilder sb, AddColumnOperation column, string indent)
	{
		var typeName = GetTypeName(column.ClrType);
		var chain = new List<string>();

		if (column.IsPrimaryKey)
			chain.Add(".IsPrimaryKey()");
		else if (!column.IsNullable)
			chain.Add(".IsRequired()");

		if (column.IsUnique)
			chain.Add(".IsUnique()");

		if (column.MaxLength.HasValue)
			chain.Add($".HasMaxLength({column.MaxLength.Value})");

		if (column.Precision.HasValue)
		{
			if (column.Scale.HasValue)
				chain.Add($".HasPrecision({column.Precision.Value}, {column.Scale.Value})");
			else
				chain.Add($".HasPrecision({column.Precision.Value})");
		}

		if (column.DefaultValue != null)
			chain.Add($".HasDefaultValue({FormatValue(column.DefaultValue)})");

		if (!string.IsNullOrEmpty(column.CheckConstraint))
			chain.Add($".HasCheckConstraint(\"{EscapeString(column.CheckConstraint)}\")");

		if (!string.IsNullOrEmpty(column.ForeignKeyTable))
			chain.Add($".IsForeignKey(\"{column.ForeignKeyTable}\")");

		if (column.IsConcurrencyToken)
			chain.Add(".IsConcurrencyToken()");

		if (column.IsComputed && !string.IsNullOrEmpty(column.ComputedColumnSql))
		{
			var storedParam = column.IsStored.HasValue ? $", {column.IsStored.Value.ToString().ToLower()}" : "";
			chain.Add($".HasComputedColumnSql(\"{EscapeString(column.ComputedColumnSql)}\"{storedParam})");
		}

		if (!string.IsNullOrEmpty(column.Comment))
			chain.Add($".HasComment(\"{EscapeString(column.Comment)}\")");

		if (chain.Count > 0)
		{
			sb.AppendLine($"{indent}builder.AddColumn<{typeName}>(\"{column.Table}\", \"{column.Name}\", c => c{string.Join("", chain)});");
		}
		else
		{
			sb.AppendLine($"{indent}builder.AddColumn<{typeName}>(\"{column.Table}\", \"{column.Name}\");");
		}
		sb.AppendLine();
	}

	private void GenerateAlterColumn(StringBuilder sb, AlterColumnOperation operation, string indent)
	{
		var chain = new List<string>();

		if (operation.ClrType != null)
			chain.Add($".HasType<{GetTypeName(operation.ClrType)}>()");

		if (operation.IsNullable.HasValue)
			chain.Add($".IsNullable({operation.IsNullable.Value.ToString().ToLower()})");

		if (operation.MaxLength.HasValue)
			chain.Add($".HasMaxLength({operation.MaxLength.Value})");

		if (operation.DefaultValue != null)
			chain.Add($".HasDefaultValue({FormatValue(operation.DefaultValue)})");

		sb.AppendLine($"{indent}builder.AlterColumn(\"{operation.Table}\", \"{operation.Name}\", c => c{string.Join("", chain)});");
		sb.AppendLine();
	}

	private void GenerateCreateIndex(StringBuilder sb, CreateIndexOperation operation, string indent)
	{
		var columnsArray = string.Join(", ", operation.Columns.Select(c => $"\"{c}\""));
		sb.Append($"{indent}builder.CreateIndex(\"{operation.Name}\", \"{operation.Table}\", [{columnsArray}]");

		if (operation.IsUnique || operation.IsClustered || !string.IsNullOrEmpty(operation.Filter))
		{
			sb.Append(", idx => idx");
			if (operation.IsUnique)
				sb.Append(".IsUnique()");
			if (operation.IsClustered)
				sb.Append(".IsClustered()");
			if (!string.IsNullOrEmpty(operation.Filter))
				sb.Append($".HasFilter(\"{EscapeString(operation.Filter)}\")");
		}

		sb.AppendLine(");");
		sb.AppendLine();
	}

	private void GenerateReverseOperations(StringBuilder sb, List<MigrationOperation> operations, string indent)
	{
		// Generate reverse operations in reverse order
		var reversed = new List<MigrationOperation>(operations);
		reversed.Reverse();

		foreach (var operation in reversed)
		{
			switch (operation)
			{
				case CreateTableOperation createTable:
					sb.AppendLine($"{indent}builder.DropTable(\"{createTable.Name}\");");
					break;
				case DropTableOperation dropTable:
					sb.AppendLine($"{indent}// TODO: Recreate table \"{dropTable.Name}\"");
					break;
				case AddColumnOperation addColumn:
					sb.AppendLine($"{indent}builder.DropColumn(\"{addColumn.Table}\", \"{addColumn.Name}\");");
					break;
				case DropColumnOperation dropColumn:
					sb.AppendLine($"{indent}// TODO: Recreate column \"{dropColumn.Table}.{dropColumn.Name}\"");
					break;
				case AlterColumnOperation alterColumn:
					sb.AppendLine($"{indent}// TODO: Revert column \"{alterColumn.Table}.{alterColumn.Name}\"");
					break;
				case CreateIndexOperation createIndex:
					sb.AppendLine($"{indent}builder.DropIndex(\"{createIndex.Name}\", \"{createIndex.Table}\");");
					break;
				case DropIndexOperation dropIndex:
					sb.AppendLine($"{indent}// TODO: Recreate index \"{dropIndex.Name}\" on \"{dropIndex.Table}\"");
					break;
				case AddCheckConstraintOperation addCheck:
					sb.AppendLine($"{indent}builder.DropCheckConstraint(\"{addCheck.Name}\", \"{addCheck.Table}\");");
					break;
				case DropCheckConstraintOperation dropCheck:
					sb.AppendLine($"{indent}// TODO: Recreate check constraint \"{dropCheck.Name}\" on \"{dropCheck.Table}\"");
					break;
			}
		}
	}

	private static string GetTypeName(Type type)
	{
		var underlying = Nullable.GetUnderlyingType(type);
		if (underlying != null)
			return GetTypeName(underlying) + "?";

		return type.Name switch
		{
			"Int32" => "int",
			"Int64" => "long",
			"Int16" => "short",
			"String" => "string",
			"Boolean" => "bool",
			"Decimal" => "decimal",
			"Double" => "double",
			"Single" => "float",
			"Byte" => "byte",
			_ => type.Name
		};
	}

	private static string FormatValue(object value)
	{
		return value switch
		{
			string s => $"\"{s}\"",
			bool b => b.ToString().ToLower(),
			decimal d => $"{d}m",
			float f => $"{f}f",
			double db => $"{db}d",
			_ => value.ToString() ?? "null"
		};
	}

	private static string SanitizeClassName(string name)
	{
		// Remove invalid characters for C# class names
		var result = new StringBuilder();
		foreach (var c in name)
		{
			if (char.IsLetterOrDigit(c) || c == '_')
				result.Append(c);
		}

		// Ensure it starts with a letter
		if (result.Length > 0 && !char.IsLetter(result[0]))
			result.Insert(0, '_');

		return result.ToString();
	}

	private static string EscapeString(string value)
	{
		// Escape quotes and backslashes for C# string literals
		return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}
}
