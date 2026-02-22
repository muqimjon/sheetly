using Sheetly.Core.Migration;
using Sheetly.Core.Validation.Rules;

namespace Sheetly.Core.Tests;

public class ForeignKeyValidatorTests
{
	[Fact]
	public void Validate_ShouldPass_WhenFkValueIsNull_AndColumnIsNullable()
	{
		var orderSchema = CreateOrderSchema(isNullable: true);
		var allSchemas = CreateAllSchemas(orderSchema);
		var validator = new ForeignKeyValidator();

		var order = new Order { Id = 1, ProductId = null };
		var context = new ValidationContext
		{
			TrackedEntities = new object[] { order },
			Schema = orderSchema,
			EntityType = typeof(Order),
			AllSchemas = allSchemas
		};

		var result = validator.Validate(order, context);
		Assert.True(result.IsValid);
	}

	[Fact]
	public void Validate_ShouldFail_WhenFkValueIsNull_AndColumnIsNotNullable()
	{
		var orderSchema = CreateOrderSchema(isNullable: false);
		var allSchemas = CreateAllSchemas(orderSchema);
		var validator = new ForeignKeyValidator();

		var order = new OrderRequired { Id = 1, ProductId = null };
		var context = new ValidationContext
		{
			TrackedEntities = new object[] { order },
			Schema = orderSchema,
			EntityType = typeof(OrderRequired),
			AllSchemas = allSchemas
		};

		var result = validator.Validate(order, context);
		Assert.False(result.IsValid);
		Assert.Contains(result.Errors, e => e.PropertyName == "ProductId");
	}

	[Fact]
	public void Validate_ShouldPass_WhenFkReferencesTrackedEntity()
	{
		var orderSchema = CreateOrderSchema(isNullable: false);
		var allSchemas = CreateAllSchemas(orderSchema);
		var validator = new ForeignKeyValidator();

		var product = new Product { Id = 5, Name = "Test" };
		var order = new OrderRequired { Id = 1, ProductId = 5 };
		var context = new ValidationContext
		{
			TrackedEntities = new object[] { order, product },
			Schema = orderSchema,
			EntityType = typeof(OrderRequired),
			AllSchemas = allSchemas
		};

		var result = validator.Validate(order, context);
		Assert.True(result.IsValid);
	}

	[Fact]
	public void Validate_ShouldFail_WhenFkReferencesNonExistentTrackedEntity()
	{
		var orderSchema = CreateOrderSchema(isNullable: false);
		var allSchemas = CreateAllSchemas(orderSchema);
		var validator = new ForeignKeyValidator();

		var product = new Product { Id = 10, Name = "Other" };
		var order = new OrderRequired { Id = 1, ProductId = 999 };
		var context = new ValidationContext
		{
			TrackedEntities = new object[] { order, product },
			Schema = orderSchema,
			EntityType = typeof(OrderRequired),
			AllSchemas = allSchemas
		};

		var result = validator.Validate(order, context);
		Assert.False(result.IsValid);
		Assert.Contains(result.Errors, e => e.Message.Contains("Foreign key"));
	}

	[Fact]
	public void Validate_ShouldSkip_WhenFkValueIsDefaultZero()
	{
		var orderSchema = CreateOrderSchema(isNullable: false);
		var allSchemas = CreateAllSchemas(orderSchema);
		var validator = new ForeignKeyValidator();

		// FK = 0 means it will be set on save (auto-increment)
		var order = new OrderWithIntFk { Id = 1, ProductId = 0 };
		var context = new ValidationContext
		{
			TrackedEntities = new object[] { order },
			Schema = orderSchema,
			EntityType = typeof(OrderWithIntFk),
			AllSchemas = allSchemas
		};

		var result = validator.Validate(order, context);
		Assert.True(result.IsValid);
	}

	private static EntitySchema CreateOrderSchema(bool isNullable)
	{
		return new EntitySchema
		{
			TableName = "Orders",
			ClassName = "Order",
			Columns =
			{
				new ColumnSchema { Name = "Id", PropertyName = "Id", DataType = "Int32", IsPrimaryKey = true },
				new ColumnSchema
				{
					Name = "ProductId", PropertyName = "ProductId", DataType = "Int32",
					IsForeignKey = true, ForeignKeyTable = "Products",
					IsNullable = isNullable
				}
			}
		};
	}

	private static Dictionary<string, EntitySchema> CreateAllSchemas(EntitySchema orderSchema)
	{
		var productSchema = new EntitySchema
		{
			TableName = "Products",
			ClassName = "Product",
			Columns =
			{
				new ColumnSchema { Name = "Id", PropertyName = "Id", DataType = "Int32", IsPrimaryKey = true },
				new ColumnSchema { Name = "Name", PropertyName = "Name", DataType = "String" }
			}
		};

		return new Dictionary<string, EntitySchema>
		{
			["Orders"] = orderSchema,
			["Products"] = productSchema
		};
	}

	private class Product
	{
		public int Id { get; set; }
		public string Name { get; set; } = "";
	}

	private class Order
	{
		public int Id { get; set; }
		public int? ProductId { get; set; }
	}

	private class OrderRequired
	{
		public int Id { get; set; }
		public int? ProductId { get; set; }
	}

	private class OrderWithIntFk
	{
		public int Id { get; set; }
		public int ProductId { get; set; }
	}
}
