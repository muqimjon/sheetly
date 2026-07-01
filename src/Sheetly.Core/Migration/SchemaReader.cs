using Sheetly.Core.Abstractions;
using Sheetly.Core.Migrations;
using System.Globalization;

namespace Sheetly.Core.Migration;

/// <summary>
/// Reconstructs a <see cref="MigrationSnapshot"/> from the remote <c>__SheetlySchema__</c> sheet.
/// This is the inverse of the migration services' AddColumnToSchemaAsync 30-column layout and
/// enables database-first scenarios (e.g. scaffolding models from an existing spreadsheet)
/// and runtime inspection of the applied schema.
/// </summary>
public static class SchemaReader
{
	private const string SchemaTable = "__SheetlySchema__";

	public static async Task<MigrationSnapshot> ReadAsync(ISheetsProvider provider)
	{
		var snapshot = new MigrationSnapshot();
		if (!await provider.SheetExistsAsync(SchemaTable))
			return snapshot;

		var rows = await provider.GetAllRowsAsync(SchemaTable);
		for (int i = 1; i < rows.Count; i++)
		{
			var r = rows[i];
			string S(int idx) => idx < r.Count ? r[idx]?.ToString() ?? string.Empty : string.Empty;

			var tableName = S(1);
			if (string.IsNullOrEmpty(tableName)) continue;

			if (!snapshot.Entities.TryGetValue(tableName, out var entity))
			{
				entity = new EntitySchema { TableName = tableName, ClassName = S(0) };
				snapshot.Entities[tableName] = entity;
			}

			bool B(int idx) => bool.TryParse(S(idx), out var b) && b;
			int? I(int idx) => int.TryParse(S(idx), out var v) ? v : null;
			decimal? D(int idx) => decimal.TryParse(S(idx), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
			static string? N(string s) => string.IsNullOrEmpty(s) ? null : s;
			static ForeignKeyAction Fk(string s) => Enum.TryParse<ForeignKeyAction>(s, out var a) ? a : ForeignKeyAction.NoAction;

			entity.Columns.Add(new ColumnSchema
			{
				Name = S(3),
				PropertyName = S(2),
				DataType = S(4),
				IsNullable = B(5),
				IsRequired = B(6),
				IsPrimaryKey = B(7),
				IsForeignKey = B(8),
				ForeignKeyTable = N(S(9)),
				ForeignKeyColumn = N(S(10)),
				OnDelete = Fk(S(11)),
				OnUpdate = Fk(S(12)),
				IsUnique = B(13),
				IndexName = N(S(14)),
				MaxLength = I(15),
				MinLength = I(16),
				Precision = I(17),
				Scale = I(18),
				MinValue = D(19),
				MaxValue = D(20),
				DefaultValue = N(S(21)),
				DefaultValueSql = N(S(22)),
				CheckConstraint = N(S(23)),
				IsComputed = B(24),
				ComputedColumnSql = N(S(25)),
				IsConcurrencyToken = B(26),
				IsAutoIncrement = B(27),
				Comment = N(S(29))
			});
		}

		snapshot.ModelHash = ModelHasher.Calculate(snapshot.Entities);
		return snapshot;
	}
}
