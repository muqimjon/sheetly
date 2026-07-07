using Sheetly.Core.Migration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Sheetly.Core.Migrations;

/// <summary>
/// Computes the model hash used to detect schema changes between migrations.
/// Hashes only structural fields — table/column names, data types, PK/FK relationships.
/// Validation-only constraints (MaxLength, IsUnique, IsRequired, etc.) don't change
/// the Sheets schema, so they don't trigger a new migration. Columns are sorted by
/// name so pure property reordering (which ModelDiffer ignores) doesn't drift the hash.
/// </summary>
public static class ModelHasher
{
	public static string Calculate(Dictionary<string, EntitySchema> entities)
	{
		var structural = entities
			.OrderBy(e => e.Key, StringComparer.Ordinal)
			.ToDictionary(
				e => e.Key,
				e => new
				{
					e.Value.TableName,
					Columns = e.Value.Columns.OrderBy(c => c.Name, StringComparer.Ordinal).Select(c => new
					{
						c.Name,
						c.DataType,
						c.IsPrimaryKey,
						c.IsAutoIncrement,
						c.IsForeignKey,
						c.ForeignKeyTable,
						c.ForeignKeyColumn
					}).ToList()
				});

		var json = JsonSerializer.Serialize(structural, new JsonSerializerOptions { WriteIndented = false });
		return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
	}
}
