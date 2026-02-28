using Sheetly.Core.Migration;
using System.Text;

namespace Sheetly.Core.Migrations.Design;

/// <summary>
/// Generates ModelSnapshot.cs file - similar to Entity Framework Core.
/// This represents the current state of the model.
/// </summary>
public class ModelSnapshotGenerator
{
	private const string Indent = "    ";

	/// <summary>
	/// Generates a ModelSnapshot C# class from the migration snapshot.
	/// </summary>
	public string GenerateModelSnapshot(
		MigrationSnapshot snapshot,
		string targetNamespace,
		string contextName)
	{
		var sb = new StringBuilder();

		sb.AppendLine("using System;");
		sb.AppendLine("using Sheetly.Core.Migration;");
		sb.AppendLine();

		sb.AppendLine($"namespace {targetNamespace};");
		sb.AppendLine();

		sb.AppendLine($"public partial class {contextName}ModelSnapshot : MigrationSnapshot");
		sb.AppendLine("{");

		sb.AppendLine($"{Indent}public {contextName}ModelSnapshot()");
		sb.AppendLine($"{Indent}{{");
		sb.AppendLine($"{Indent}{Indent}var snapshot = BuildModel();");
		sb.AppendLine($"{Indent}{Indent}this.Entities = snapshot.Entities;");
		sb.AppendLine($"{Indent}{Indent}this.ModelHash = snapshot.ModelHash;");
		sb.AppendLine($"{Indent}{Indent}this.Version = snapshot.Version;");
		sb.AppendLine($"{Indent}{Indent}this.LastUpdated = snapshot.LastUpdated;");
		sb.AppendLine($"{Indent}}}");
		sb.AppendLine();

		sb.AppendLine($"{Indent}public static MigrationSnapshot BuildModel()");
		sb.AppendLine($"{Indent}{{");
		sb.AppendLine($"{Indent}{Indent}var snapshot = new MigrationSnapshot");
		sb.AppendLine($"{Indent}{Indent}{{");
		sb.AppendLine($"{Indent}{Indent}{Indent}ModelHash = \"{snapshot.ModelHash}\",");
		sb.AppendLine($"{Indent}{Indent}{Indent}Version = \"{snapshot.Version}\",");
		sb.AppendLine($"{Indent}{Indent}{Indent}LastUpdated = DateTime.Parse(\"{snapshot.LastUpdated:O}\")");
		sb.AppendLine($"{Indent}{Indent}}};");
		sb.AppendLine();

		foreach (var entity in snapshot.Entities.OrderBy(e => e.Key))
		{
			GenerateEntity(sb, entity.Value, Indent + Indent);
		}

		sb.AppendLine($"{Indent}{Indent}return snapshot;");
		sb.AppendLine($"{Indent}}}");
		sb.AppendLine("}");

		return sb.ToString();
	}

	private void GenerateEntity(StringBuilder sb, EntitySchema entity, string indent)
	{
		sb.AppendLine($"{indent}// {entity.ClassName}");
		sb.AppendLine($"{indent}snapshot.Entities[\"{entity.TableName}\"] = new EntitySchema");
		sb.AppendLine($"{indent}{{");
		sb.AppendLine($"{indent}{Indent}TableName = \"{entity.TableName}\",");
		sb.AppendLine($"{indent}{Indent}ClassName = \"{entity.ClassName}\",");
		sb.AppendLine($"{indent}{Indent}Namespace = \"{entity.Namespace}\",");
		sb.AppendLine($"{indent}{Indent}Columns = new List<ColumnSchema>");
		sb.AppendLine($"{indent}{Indent}{{");

		for (int i = 0; i < entity.Columns.Count; i++)
		{
			var column = entity.Columns[i];
			var isLast = i == entity.Columns.Count - 1;
			GenerateColumn(sb, column, indent + Indent + Indent, isLast);
		}

		sb.AppendLine($"{indent}{Indent}}},");

		if (entity.Relationships.Count > 0)
		{
			sb.AppendLine($"{indent}{Indent}Relationships = new List<RelationshipSchema>");
			sb.AppendLine($"{indent}{Indent}{{");

			for (int i = 0; i < entity.Relationships.Count; i++)
			{
				var rel = entity.Relationships[i];
				var comma = i < entity.Relationships.Count - 1 ? "," : "";
				sb.AppendLine($"{indent}{Indent}{Indent}new RelationshipSchema {{ FromProperty = \"{rel.FromProperty}\", ToTable = \"{rel.ToTable}\", Type = RelationshipType.{rel.Type} }}{comma}");
			}

			sb.AppendLine($"{indent}{Indent}}}");
		}
		else
		{
			sb.AppendLine($"{indent}{Indent}Relationships = new List<RelationshipSchema>()");
		}

		sb.AppendLine($"{indent}}};");
		sb.AppendLine();
	}

	private void GenerateColumn(StringBuilder sb, ColumnSchema column, string indent, bool isLast)
	{
		var comma = isLast ? "" : ",";

		sb.AppendLine($"{indent}new ColumnSchema");
		sb.AppendLine($"{indent}{{");
		sb.AppendLine($"{indent}{Indent}Name = \"{column.Name}\",");
		sb.AppendLine($"{indent}{Indent}PropertyName = \"{column.PropertyName}\",");
		sb.AppendLine($"{indent}{Indent}DataType = \"{column.DataType}\",");
		sb.AppendLine($"{indent}{Indent}IsPrimaryKey = {column.IsPrimaryKey.ToString().ToLower()},");
		sb.AppendLine($"{indent}{Indent}IsAutoIncrement = {column.IsAutoIncrement.ToString().ToLower()},");
		sb.AppendLine($"{indent}{Indent}IsForeignKey = {column.IsForeignKey.ToString().ToLower()},");

		if (!string.IsNullOrEmpty(column.ForeignKeyTable))
			sb.AppendLine($"{indent}{Indent}ForeignKeyTable = \"{column.ForeignKeyTable}\",");

		if (!string.IsNullOrEmpty(column.ForeignKeyColumn))
			sb.AppendLine($"{indent}{Indent}ForeignKeyColumn = \"{column.ForeignKeyColumn}\",");

		sb.AppendLine($"{indent}{Indent}IsNullable = {column.IsNullable.ToString().ToLower()},");
		sb.AppendLine($"{indent}{Indent}IsRequired = {column.IsRequired.ToString().ToLower()},");

		if (column.IsUnique)
			sb.AppendLine($"{indent}{Indent}IsUnique = true,");

		if (column.MaxLength.HasValue)
			sb.AppendLine($"{indent}{Indent}MaxLength = {column.MaxLength.Value},");

		if (column.MinLength.HasValue)
			sb.AppendLine($"{indent}{Indent}MinLength = {column.MinLength.Value},");

		if (column.Precision.HasValue)
			sb.AppendLine($"{indent}{Indent}Precision = {column.Precision.Value},");

		if (column.Scale.HasValue)
			sb.AppendLine($"{indent}{Indent}Scale = {column.Scale.Value},");

		if (column.MinValue.HasValue)
			sb.AppendLine($"{indent}{Indent}MinValue = {column.MinValue.Value}m,");

		if (column.MaxValue.HasValue)
			sb.AppendLine($"{indent}{Indent}MaxValue = {column.MaxValue.Value}m,");

		if (column.DefaultValue is not null)
			sb.AppendLine($"{indent}{Indent}DefaultValue = {FormatValue(column.DefaultValue)},");

		if (!string.IsNullOrEmpty(column.CheckConstraint))
			sb.AppendLine($"{indent}{Indent}CheckConstraint = \"{EscapeString(column.CheckConstraint)}\",");

		if (column.IsComputed)
			sb.AppendLine($"{indent}{Indent}IsComputed = true,");

		if (!string.IsNullOrEmpty(column.ComputedColumnSql))
			sb.AppendLine($"{indent}{Indent}ComputedColumnSql = \"{EscapeString(column.ComputedColumnSql)}\",");

		if (column.IsConcurrencyToken)
			sb.AppendLine($"{indent}{Indent}IsConcurrencyToken = true,");

		if (!string.IsNullOrEmpty(column.Comment))
			sb.AppendLine($"{indent}{Indent}Comment = \"{EscapeString(column.Comment)}\",");

		var lastLine = sb.ToString().TrimEnd();
		if (lastLine.EndsWith(","))
		{
			sb.Length -= (Environment.NewLine.Length + 1);
			sb.AppendLine();
		}

		sb.AppendLine($"{indent}}}{comma}");
	}

	private static string FormatValue(object value)
	{
		return value switch
		{
			string s => $"\"{EscapeString(s)}\"",
			bool b => b.ToString().ToLower(),
			decimal d => $"{d}m",
			float f => $"{f}f",
			double db => $"{db}d",
			_ => value.ToString() ?? "null"
		};
	}

	private static string EscapeString(string value)
	{
		return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
	}
}
