using Sheetly.Core.Migration;
using Sheetly.Core.Migrations;

namespace Sheetly.Core.Tests;

/// <summary>
/// M2b — the model hash is order-insensitive, so reordering properties (which the
/// name-keyed ModelDiffer treats as "no change") never drifts the hash and strands the context.
/// </summary>
public class ModelHasherTests
{
	private static ColumnSchema Col(string name, string type = "String") =>
		new() { Name = name, PropertyName = name, DataType = type };

	private static Dictionary<string, EntitySchema> Model(params ColumnSchema[] columns) =>
		new() { ["Products"] = new EntitySchema { TableName = "Products", ClassName = "Product", Columns = columns.ToList() } };

	[Fact]
	public void Calculate_IsInsensitiveToColumnOrder()
	{
		var a = ModelHasher.Calculate(Model(Col("Id", "Int32"), Col("Title"), Col("Price", "Decimal")));
		var b = ModelHasher.Calculate(Model(Col("Title"), Col("Price", "Decimal"), Col("Id", "Int32")));

		Assert.Equal(a, b);
	}

	[Fact]
	public void Calculate_ChangesWhenColumnAddedOrRetyped()
	{
		var baseline = ModelHasher.Calculate(Model(Col("Id", "Int32"), Col("Title")));
		var added = ModelHasher.Calculate(Model(Col("Id", "Int32"), Col("Title"), Col("Sku")));
		var retyped = ModelHasher.Calculate(Model(Col("Id", "Int64"), Col("Title")));

		Assert.NotEqual(baseline, added);
		Assert.NotEqual(baseline, retyped);
	}
}
