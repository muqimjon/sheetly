using Sheetly.Core.Tests.Integration.Helpers;
using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Verifies that user-assigned (non-auto-increment) primary keys work correctly:
///   - The user-provided value is stored as-is (not overwritten)
///   - Empty/null PK throws a validation error
///   - Duplicate PK in the same batch throws a validation error
/// </summary>
public class StringPkTests
{
	// UserAccount auto-derives table name "UserAccounts"
	private const string TableName = "UserAccounts";

	private static async Task<(StringPkDbContext ctx, InMemorySheetsProvider provider)> CreateAsync()
	{
		var provider = new InMemorySheetsProvider();
		await provider.CreateSheetAsync(TableName, ["Username", "Email"]);
		await provider.CreateSheetAsync("__SheetlySchema__", StringPkContextFactory.SchemaHeaders);
		await StringPkContextFactory.AppendSchemaRowAsync(provider, "UserAccount", TableName, "Username");

		var ctx = new StringPkDbContext();
		await ctx.InitializeAsync(provider);
		return (ctx, provider);
	}

	[Fact]
	public async Task Add_StringPk_ValueIsPreserved()
	{
		var (ctx, _) = await CreateAsync();

		var account = new UserAccount { Username = "johndoe", Email = "john@example.com" };
		ctx.Accounts.Add(account);
		await ctx.SaveChangesAsync();

		Assert.Equal("johndoe", account.Username);
	}

	[Fact]
	public async Task Add_StringPk_StoredCorrectlyInSheet()
	{
		var (ctx, provider) = await CreateAsync();

		ctx.Accounts.Add(new UserAccount { Username = "alice", Email = "alice@example.com" });
		await ctx.SaveChangesAsync();

		var rows = provider.GetSheetSnapshot(TableName);
		Assert.Equal(2, rows.Count); // header + 1 data row
		Assert.Equal("alice", rows[1][0]?.ToString());
	}

	[Fact]
	public async Task Add_EmptyStringPk_ThrowsValidationException()
	{
		var (ctx, _) = await CreateAsync();

		ctx.Accounts.Add(new UserAccount { Username = "", Email = "x@example.com" });

		await Assert.ThrowsAsync<Sheetly.Core.Validation.ValidationException>(
			() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task Add_DuplicateStringPk_InSameBatch_ThrowsValidationException()
	{
		var (ctx, _) = await CreateAsync();

		ctx.Accounts.Add(new UserAccount { Username = "bob", Email = "bob1@example.com" });
		ctx.Accounts.Add(new UserAccount { Username = "bob", Email = "bob2@example.com" });

		await Assert.ThrowsAsync<Sheetly.Core.Validation.ValidationException>(
			() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task Add_MultipleStringPk_AllPreserved()
	{
		var (ctx, _) = await CreateAsync();

		ctx.Accounts.Add(new UserAccount { Username = "user1", Email = "u1@example.com" });
		ctx.Accounts.Add(new UserAccount { Username = "user2", Email = "u2@example.com" });
		ctx.Accounts.Add(new UserAccount { Username = "user3", Email = "u3@example.com" });
		await ctx.SaveChangesAsync();

		var all = await ctx.Accounts.ToListAsync();
		var usernames = all.Select(a => a.Username).OrderBy(u => u).ToList();
		Assert.Equal(["user1", "user2", "user3"], usernames);
	}
}

public class StringPkDbContext : SheetsContext
{
	public SheetsSet<UserAccount> Accounts { get; set; } = default!;
}

public static class StringPkContextFactory
{
	public static readonly string[] SchemaHeaders = new string[30]
	{
		"ClassName", "TableName", "PropertyName", "ColumnName", "DataType",
		"IsNullable", "IsRequired", "IsPrimaryKey", "IsForeignKey", "ForeignKeyTable",
		"ForeignKeyColumn", "OnDelete", "OnUpdate", "IsUnique", "IndexName",
		"MaxLength", "MinLength", "Precision", "Scale", "MinValue",
		"MaxValue", "DefaultValue", "DefaultValueSql", "CheckConstraint", "IsComputed",
		"ComputedSql", "IsConcurrencyToken", "IsAutoIncrement", "CurrentIdValue", "Comment"
	};

	public static async Task AppendSchemaRowAsync(
		InMemorySheetsProvider provider,
		string className,
		string tableName,
		string pkPropertyName)
	{
		var row = new object[30];
		for (int i = 0; i < row.Length; i++) row[i] = string.Empty;

		row[0] = className;
		row[1] = tableName;
		row[2] = pkPropertyName;
		row[3] = pkPropertyName;
		row[4] = "String";
		row[6] = "True";   // IsRequired
		row[7] = "True";   // IsPrimaryKey
		row[27] = "False"; // IsAutoIncrement — user-assigned PK
		row[28] = "0";

		await provider.AppendRowAsync("__SheetlySchema__", row);
	}
}
