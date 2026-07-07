using Sheetly.Core.Internal;
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
		List<MigrationOperation> operations,
		List<MigrationOperation>? downOperations = null)
	{
		var sb = new StringBuilder();

		sb.AppendLine("using Sheetly.Core.Migrations;");
		sb.AppendLine("using Sheetly.Core.Migrations.Operations;");
		sb.AppendLine();

		sb.AppendLine($"namespace {targetNamespace};");
		sb.AppendLine();

		sb.AppendLine($"[Migration(\"{EscapeString(migrationId)}\")]");
		sb.AppendLine($"public partial class {SanitizeClassName(migrationName)} : Migration");
		sb.AppendLine("{");

		sb.AppendLine($"{Indent}public override void Up(MigrationBuilder builder)");
		sb.AppendLine($"{Indent}{{");
		GenerateOperations(sb, operations, Indent + Indent);
		sb.AppendLine($"{Indent}}}");
		sb.AppendLine();

		sb.AppendLine($"{Indent}public override void Down(MigrationBuilder builder)");
		sb.AppendLine($"{Indent}{{");
		if (downOperations is not null)
			GenerateOperations(sb, downOperations, Indent + Indent);
		else
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
					sb.AppendLine($"{indent}builder.DropTable(\"{EscapeString(dropTable.Name)}\");");
					sb.AppendLine();
					break;
				case AddColumnOperation addColumn:
					GenerateAddColumn(sb, addColumn, indent);
					break;
				case DropColumnOperation dropColumn:
					sb.AppendLine($"{indent}builder.DropColumn(\"{EscapeString(dropColumn.Table)}\", \"{EscapeString(dropColumn.Name)}\");");
					sb.AppendLine();
					break;
				case RenameColumnOperation renameColumn:
					sb.AppendLine($"{indent}builder.RenameColumn(\"{EscapeString(renameColumn.Table)}\", \"{EscapeString(renameColumn.Name)}\", \"{EscapeString(renameColumn.NewName)}\");");
					sb.AppendLine();
					break;
				case RenameTableOperation renameTable:
					sb.AppendLine($"{indent}builder.RenameTable(\"{EscapeString(renameTable.Name)}\", \"{EscapeString(renameTable.NewName)}\");");
					sb.AppendLine();
					break;
				case AlterColumnOperation alterColumn:
					GenerateAlterColumn(sb, alterColumn, indent);
					break;
				case CreateIndexOperation createIndex:
					GenerateCreateIndex(sb, createIndex, indent);
					break;
				case DropIndexOperation dropIndex:
					sb.AppendLine($"{indent}builder.DropIndex(\"{EscapeString(dropIndex.Name)}\", \"{EscapeString(dropIndex.Table)}\");");
					sb.AppendLine();
					break;
				case AddCheckConstraintOperation addCheck:
					sb.AppendLine($"{indent}builder.AddCheckConstraint(\"{EscapeString(addCheck.Name)}\", \"{EscapeString(addCheck.Table)}\", \"{EscapeString(addCheck.Sql)}\");");
					sb.AppendLine();
					break;
				case DropCheckConstraintOperation dropCheck:
					sb.AppendLine($"{indent}builder.DropCheckConstraint(\"{EscapeString(dropCheck.Name)}\", \"{EscapeString(dropCheck.Table)}\");");
					sb.AppendLine();
					break;
			}
		}
	}

	private void GenerateCreateTable(StringBuilder sb, CreateTableOperation operation, string indent)
	{
		sb.AppendLine($"{indent}builder.CreateTable(\"{EscapeString(operation.Name)}\", table => table");

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

		if (column.DefaultValue is not null)
			chain.Add($".HasDefaultValue({FormatValue(column.DefaultValue)})");

		if (!string.IsNullOrEmpty(column.CheckConstraint))
			chain.Add($".HasCheckConstraint(\"{EscapeString(column.CheckConstraint)}\")");

		if (!string.IsNullOrEmpty(column.ForeignKeyTable))
			chain.Add($".IsForeignKey(\"{EscapeString(column.ForeignKeyTable)}\")");

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
		var name = EscapeString(column.Name);

		if (chain.Count > 0)
			sb.AppendLine($"{indent}.Column<{typeName}>(\"{name}\", c => c{chainStr})");
		else
			sb.AppendLine($"{indent}.Column<{typeName}>(\"{name}\")");
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

		if (column.DefaultValue is not null)
			chain.Add($".HasDefaultValue({FormatValue(column.DefaultValue)})");

		if (!string.IsNullOrEmpty(column.CheckConstraint))
			chain.Add($".HasCheckConstraint(\"{EscapeString(column.CheckConstraint)}\")");

		if (!string.IsNullOrEmpty(column.ForeignKeyTable))
			chain.Add($".IsForeignKey(\"{EscapeString(column.ForeignKeyTable)}\")");

		if (column.IsConcurrencyToken)
			chain.Add(".IsConcurrencyToken()");

		if (column.IsComputed && !string.IsNullOrEmpty(column.ComputedColumnSql))
		{
			var storedParam = column.IsStored.HasValue ? $", {column.IsStored.Value.ToString().ToLower()}" : "";
			chain.Add($".HasComputedColumnSql(\"{EscapeString(column.ComputedColumnSql)}\"{storedParam})");
		}

		if (!string.IsNullOrEmpty(column.Comment))
			chain.Add($".HasComment(\"{EscapeString(column.Comment)}\")");

		var table = EscapeString(column.Table);
		var name = EscapeString(column.Name);

		if (chain.Count > 0)
			sb.AppendLine($"{indent}builder.AddColumn<{typeName}>(\"{table}\", \"{name}\", c => c{string.Join("", chain)});");
		else
			sb.AppendLine($"{indent}builder.AddColumn<{typeName}>(\"{table}\", \"{name}\");");
		sb.AppendLine();
	}

	private void GenerateAlterColumn(StringBuilder sb, AlterColumnOperation operation, string indent)
	{
		var chain = new List<string>();

		if (operation.ClrType is not null)
			chain.Add($".HasType<{GetTypeName(operation.ClrType)}>()");

		if (operation.IsNullable.HasValue)
			chain.Add($".IsNullable({operation.IsNullable.Value.ToString().ToLower()})");

		if (operation.MaxLength.HasValue)
			chain.Add($".HasMaxLength({operation.MaxLength.Value})");

		if (operation.DefaultValue is not null)
			chain.Add($".HasDefaultValue({FormatValue(operation.DefaultValue)})");

		if (operation.IsPrimaryKey.HasValue)
			chain.Add($".IsPrimaryKey({operation.IsPrimaryKey.Value.ToString().ToLower()})");

		if (operation.IsAutoIncrement.HasValue)
			chain.Add($".IsAutoIncrement({operation.IsAutoIncrement.Value.ToString().ToLower()})");

		if (operation.IsUnique.HasValue)
			chain.Add($".IsUnique({operation.IsUnique.Value.ToString().ToLower()})");

		if (operation.IsForeignKey == true && !string.IsNullOrEmpty(operation.ForeignKeyTable))
			chain.Add($".IsForeignKey(\"{EscapeString(operation.ForeignKeyTable)}\")");
		else if (operation.IsForeignKey == false)
			chain.Add(".DropForeignKey()");

		sb.AppendLine($"{indent}builder.AlterColumn(\"{EscapeString(operation.Table)}\", \"{EscapeString(operation.Name)}\", c => c{string.Join("", chain)});");
		sb.AppendLine();
	}

	private void GenerateCreateIndex(StringBuilder sb, CreateIndexOperation operation, string indent)
	{
		var columnsArray = string.Join(", ", operation.Columns.Select(c => $"\"{EscapeString(c)}\""));
		sb.Append($"{indent}builder.CreateIndex(\"{EscapeString(operation.Name)}\", \"{EscapeString(operation.Table)}\", [{columnsArray}]");

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
				case RenameColumnOperation renameColumn:
					sb.AppendLine($"{indent}builder.RenameColumn(\"{renameColumn.Table}\", \"{renameColumn.NewName}\", \"{renameColumn.Name}\");");
					break;
				case RenameTableOperation renameTable:
					sb.AppendLine($"{indent}builder.RenameTable(\"{renameTable.NewName}\", \"{renameTable.Name}\");");
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
		if (underlying is not null)
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

	private static string FormatValue(object value) => CSharpHelper.FormatLiteral(value);

	private static string SanitizeClassName(string name)
	{
		var result = new StringBuilder();
		foreach (var c in name)
		{
			if (char.IsLetterOrDigit(c) || c == '_')
				result.Append(c);
		}

		if (result.Length > 0 && !char.IsLetter(result[0]))
			result.Insert(0, '_');

		var sanitized = result.ToString();
		return CSharpHelper.IsKeyword(sanitized) ? "@" + sanitized : sanitized;
	}

	private static string EscapeString(string value) => CSharpHelper.EscapeStringLiteral(value);
}
