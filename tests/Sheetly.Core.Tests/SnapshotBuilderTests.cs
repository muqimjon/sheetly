using Sheetly.Core.Migrations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sheetly.Core.Tests;

public class SnapshotBuilderTests
{
	[Fact]
	public void BuildFromContext_ShouldDetectEntities()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContext));

		Assert.NotEmpty(snapshot.Entities);
		Assert.Contains("TestUsers", snapshot.Entities.Keys);
	}

	[Fact]
	public void BuildFromContext_ShouldDetectPrimaryKey()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContext));
		var userSchema = snapshot.Entities["TestUsers"];

		var pkColumn = userSchema.Columns.FirstOrDefault(c => c.IsPrimaryKey);
		Assert.NotNull(pkColumn);
		Assert.Equal("Id", pkColumn.PropertyName);
	}

	[Fact]
	public void BuildFromContext_ShouldDetectForeignKey()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContextWithFk));

		Assert.Contains("TestOrders", snapshot.Entities.Keys);
		var orderSchema = snapshot.Entities["TestOrders"];

		var fkColumn = orderSchema.Columns.FirstOrDefault(c => c.IsForeignKey);
		Assert.NotNull(fkColumn);
		Assert.Equal("UserId", fkColumn.PropertyName);
		Assert.Equal("TestUsers", fkColumn.ForeignKeyTable);
	}

	[Fact]
	public void BuildFromContext_ShouldRespectFluentApiTableName()
	{
		var modelMetadata = new Dictionary<Type, EntityMetadata>();
		var builder = new EntityTypeBuilder<TestUser>();
		builder.HasSheetName("CustomUsers");
		modelMetadata[typeof(TestUser)] = builder;

		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContext), modelMetadata);

		Assert.Contains("CustomUsers", snapshot.Entities.Keys);
		Assert.DoesNotContain("TestUsers", snapshot.Entities.Keys);
	}

	[Fact]
	public void BuildFromContext_ShouldRespectFluentApiRequired()
	{
		var modelMetadata = new Dictionary<Type, EntityMetadata>();
		var builder = new EntityTypeBuilder<TestUser>();
		builder.Property(u => u.Name).IsRequired();
		modelMetadata[typeof(TestUser)] = builder;

		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContext), modelMetadata);
		var schema = snapshot.Entities.Values.First(e => e.ClassName == "TestUser");

		var nameCol = schema.Columns.First(c => c.PropertyName == "Name");
		Assert.False(nameCol.IsNullable);
		Assert.True(nameCol.IsRequired);
	}

	[Fact]
	public void BuildFromContext_ShouldRespectAttributeTableName()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContextWithAttr));

		// Table attribute "custom_products" should override default pluralization
		Assert.Contains("custom_products", snapshot.Entities.Keys);
	}

	[Fact]
	public void BuildFromContext_ShouldSkipNavigationProperties()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContextWithFk));
		var orderSchema = snapshot.Entities["TestOrders"];

		// Should not have a column for the navigation property "User"
		Assert.DoesNotContain(orderSchema.Columns, c => c.PropertyName == "User");
	}

	[Fact]
	public void BuildFromContext_ShouldSetAutoIncrementForPK()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContext));
		var userSchema = snapshot.Entities["TestUsers"];
		var pkColumn = userSchema.Columns.First(c => c.IsPrimaryKey);

		Assert.True(pkColumn.IsAutoIncrement);
	}

	[Fact]
	public void BuildFromContext_NumericPK_IsRequired()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContext));
		var pkColumn = snapshot.Entities["TestUsers"].Columns.First(c => c.IsPrimaryKey);

		Assert.True(pkColumn.IsRequired);
		Assert.False(pkColumn.IsNullable);
	}

	[Fact]
	public void BuildFromContext_StringPK_IsNotAutoIncrement()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContextWithStringPk));
		var pkColumn = snapshot.Entities["TestAccounts"].Columns.First(c => c.IsPrimaryKey);

		Assert.False(pkColumn.IsAutoIncrement);
	}

	[Fact]
	public void BuildFromContext_StringPK_IsRequired()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContextWithStringPk));
		var pkColumn = snapshot.Entities["TestAccounts"].Columns.First(c => c.IsPrimaryKey);

		Assert.True(pkColumn.IsRequired);
	}

	[Fact]
	public void BuildFromContext_StringPK_IsNotNullable()
	{
		var snapshot = SnapshotBuilder.BuildFromContext(typeof(TestContextWithStringPk));
		var pkColumn = snapshot.Entities["TestAccounts"].Columns.First(c => c.IsPrimaryKey);

		Assert.False(pkColumn.IsNullable);
	}

	// Test context classes
	private class TestContext : SheetsContext
	{
		public SheetsSet<TestUser> Users { get; set; } = default!;
	}

	private class TestContextWithFk : SheetsContext
	{
		public SheetsSet<TestUser> Users { get; set; } = default!;
		public SheetsSet<TestOrder> Orders { get; set; } = default!;
	}

	private class TestContextWithAttr : SheetsContext
	{
		public SheetsSet<TestProduct> Products { get; set; } = default!;
	}

	private class TestContextWithStringPk : SheetsContext
	{
		public SheetsSet<TestAccount> Accounts { get; set; } = default!;
	}

	private class TestUser
	{
		public int Id { get; set; }
		public string Name { get; set; } = "";
		public int? Age { get; set; }
	}

	private class TestOrder
	{
		public int Id { get; set; }
		public int UserId { get; set; }
		public TestUser? User { get; set; }
	}

	[Table("custom_products")]
	private class TestProduct
	{
		public int Id { get; set; }
		public string Name { get; set; } = "";
	}

	private class TestAccount
	{
		[System.ComponentModel.DataAnnotations.Key]
		public string Username { get; set; } = "";
		public string Email { get; set; } = "";
	}
}
