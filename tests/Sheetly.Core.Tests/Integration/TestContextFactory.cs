using Sheetly.Core.Tests.Integration.Helpers;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Creates fully-initialised TestDbContext instances backed by a fresh
/// InMemorySheetsProvider.  Each call returns an isolated context so
/// tests are independent of each other.
/// </summary>
public static class TestContextFactory
{
	// Column headers for __SheetlySchema__ — must match GoogleMigrationService.SchemaTableHeaders
	// (indices 0-29; index 28 = "CurrentIdValue")
	private static readonly string[] SchemaHeaders = new string[30]
	{
		"ClassName", "TableName", "PropertyName", "ColumnName", "DataType",
		"IsNullable", "IsRequired", "IsPrimaryKey", "IsForeignKey", "ForeignKeyTable",
		"ForeignKeyColumn", "OnDelete", "OnUpdate", "IsUnique", "IndexName",
		"MaxLength", "MinLength", "Precision", "Scale", "MinValue",
		"MaxValue", "DefaultValue", "DefaultValueSql", "CheckConstraint", "IsComputed",
		"ComputedSql", "IsConcurrencyToken", "IsAutoIncrement", "CurrentIdValue", "Comment"
	};

	/// <summary>
	/// Creates a fresh in-memory provider pre-seeded with the expected sheets
	/// and then returns an initialised context that is ready for CRUD usage.
	/// </summary>
	public static async Task<(TestDbContext ctx, InMemorySheetsProvider provider)> CreateAsync()
	{
		var provider = new InMemorySheetsProvider();
		await SeedSheetsAsync(provider);

		var ctx = new TestDbContext();
		await ctx.InitializeAsync(provider);
		return (ctx, provider);
	}

	// ── Internal setup ────────────────────────────────────────────────────────

	private static async Task SeedSheetsAsync(InMemorySheetsProvider provider)
	{
		// Create entity sheets with appropriate column headers
		await provider.CreateSheetAsync("Categories", new[] { "Id", "Name" });
		await provider.CreateSheetAsync("Products",
			new[] { "Id", "Title", "Price", "Description", "Stock", "CategoryId" });

		// Create __SheetlySchema__ with a PK-tracking row for each entity
		await provider.CreateSheetAsync("__SheetlySchema__", SchemaHeaders);
		await AppendSchemaRowAsync(provider, "Category", "Categories", "Id");
		await AppendSchemaRowAsync(provider, "Product", "Products", "Id");
	}

	/// <summary>
	/// Appends a minimal schema row that satisfies GetAndIncrementIdFromCentralSchema.
	/// The important columns are [1]=TableName, [2]=PropertyName, [28]=CurrentIdValue.
	/// </summary>
	private static async Task AppendSchemaRowAsync(
		InMemorySheetsProvider provider,
		string className,
		string tableName,
		string pkPropertyName)
	{
		var row = new object[30];
		for (int i = 0; i < row.Length; i++) row[i] = string.Empty;

		row[0] = className;        // ClassName
		row[1] = tableName;        // TableName  ← used by GetAndIncrementIdFromCentralSchema
		row[2] = pkPropertyName;   // PropertyName (PK) ← used by GetAndIncrementIdFromCentralSchema
		row[3] = pkPropertyName;   // ColumnName
		row[4] = "Int32";          // DataType
		row[7] = "True";           // IsPrimaryKey
		row[27] = "True";          // IsAutoIncrement
		row[28] = "0";             // CurrentIdValue ← seed at 0; incremented on first insert

		await provider.AppendRowAsync("__SheetlySchema__", row);
	}
}
