using Sheetly.Core.Mapping;
using Sheetly.Core.Migration;

namespace Sheetly.Core.Tests;

/// <summary>
/// B4 — the property cache resolves exactly one PropertyInfo per name and picks the most-derived
/// declaration, so an entity that new-shadows a base-class property maps instead of throwing
/// "an item with the same key has already been added".
/// </summary>
public class EntityMapperTests
{
	private class AuditedBase
	{
		public int Id { get; set; }
		public object Code { get; set; } = string.Empty;
	}

	private sealed class ShadowedEntity : AuditedBase
	{
		public new string Code { get; set; } = string.Empty;
	}

	private static EntitySchema Schema() => new()
	{
		TableName = "ShadowedEntities",
		ClassName = nameof(ShadowedEntity),
		Columns =
		[
			new ColumnSchema { Name = "Id", PropertyName = "Id", DataType = "Int32", IsPrimaryKey = true },
			new ColumnSchema { Name = "Code", PropertyName = "Code", DataType = "String" }
		]
	};

	[Fact]
	public void MapFromRow_ShadowedProperty_UsesMostDerivedDeclaration()
	{
		var entity = EntityMapper.MapFromRow<ShadowedEntity>([1, "X-1"], ["Id", "Code"], Schema());

		Assert.Equal(1, entity.Id);
		Assert.Equal("X-1", entity.Code);
	}

	[Fact]
	public void MapToRow_ShadowedProperty_WritesMostDerivedValue()
	{
		var entity = new ShadowedEntity { Id = 7, Code = "X-7" };
		((AuditedBase)entity).Code = "stale";

		var row = EntityMapper.MapToRow(entity, Schema(), ["Id", "Code"]);

		Assert.Equal(7, row[0]);
		Assert.Equal("X-7", row[1]);
	}
}
