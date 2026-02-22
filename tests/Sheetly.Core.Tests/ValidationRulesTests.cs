using Sheetly.Core.Migration;
using Sheetly.Core.Validation.Rules;

namespace Sheetly.Core.Tests;

public class ValidationRulesTests
{
	#region RangeValidator Tests

	[Fact]
	public void RangeValidator_ShouldFail_WhenBelowMinimum()
	{
		var schema = new EntitySchema
		{
			TableName = "Products",
			Columns =
			{
				new ColumnSchema { Name = "Price", PropertyName = "Price", DataType = "Decimal", MinValue = 0m }
			}
		};

		var validator = new RangeValidator();
		var entity = new PriceEntity { Price = -5m };
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(PriceEntity),
			TrackedEntities = new[] { entity }
		};

		var result = validator.Validate(entity, context);
		Assert.False(result.IsValid);
		Assert.Contains(result.Errors, e => e.PropertyName == "Price");
	}

	[Fact]
	public void RangeValidator_ShouldFail_WhenAboveMaximum()
	{
		var schema = new EntitySchema
		{
			TableName = "Products",
			Columns =
			{
				new ColumnSchema { Name = "Price", PropertyName = "Price", DataType = "Decimal", MaxValue = 1000m }
			}
		};

		var validator = new RangeValidator();
		var entity = new PriceEntity { Price = 1500m };
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(PriceEntity),
			TrackedEntities = new[] { entity }
		};

		var result = validator.Validate(entity, context);
		Assert.False(result.IsValid);
	}

	[Fact]
	public void RangeValidator_ShouldPass_WhenInRange()
	{
		var schema = new EntitySchema
		{
			TableName = "Products",
			Columns =
			{
				new ColumnSchema { Name = "Price", PropertyName = "Price", DataType = "Decimal", MinValue = 0m, MaxValue = 1000m }
			}
		};

		var validator = new RangeValidator();
		var entity = new PriceEntity { Price = 500m };
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(PriceEntity),
			TrackedEntities = new[] { entity }
		};

		var result = validator.Validate(entity, context);
		Assert.True(result.IsValid);
	}

	#endregion

	#region UniqueValidator Tests

	[Fact]
	public void UniqueValidator_ShouldFail_WhenDuplicateValueExists()
	{
		var schema = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Email", PropertyName = "Email", DataType = "String", IsUnique = true }
			}
		};

		var validator = new UniqueValidator();
		var user1 = new EmailEntity { Email = "test@test.com" };
		var user2 = new EmailEntity { Email = "test@test.com" };
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(EmailEntity),
			TrackedEntities = new object[] { user1, user2 }
		};

		var result = validator.Validate(user1, context);
		Assert.False(result.IsValid);
		Assert.Contains(result.Errors, e => e.Message.Contains("Duplicate"));
	}

	[Fact]
	public void UniqueValidator_ShouldPass_WhenAllValuesAreUnique()
	{
		var schema = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Email", PropertyName = "Email", DataType = "String", IsUnique = true }
			}
		};

		var validator = new UniqueValidator();
		var user1 = new EmailEntity { Email = "user1@test.com" };
		var user2 = new EmailEntity { Email = "user2@test.com" };
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(EmailEntity),
			TrackedEntities = new object[] { user1, user2 }
		};

		var result = validator.Validate(user1, context);
		Assert.True(result.IsValid);
	}

	#endregion

	#region CheckConstraintValidator Tests

	[Fact]
	public void CheckConstraint_ShouldFail_WhenViolated()
	{
		var schema = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Age", PropertyName = "Age", DataType = "Int32", CheckConstraint = "Age >= 18" }
			}
		};

		var validator = new CheckConstraintValidator();
		var entity = new AgeEntity { Age = 15 };
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(AgeEntity),
			TrackedEntities = new[] { entity }
		};

		var result = validator.Validate(entity, context);
		Assert.False(result.IsValid);
	}

	[Fact]
	public void CheckConstraint_ShouldPass_WhenSatisfied()
	{
		var schema = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Age", PropertyName = "Age", DataType = "Int32", CheckConstraint = "Age >= 18" }
			}
		};

		var validator = new CheckConstraintValidator();
		var entity = new AgeEntity { Age = 25 };
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(AgeEntity),
			TrackedEntities = new[] { entity }
		};

		var result = validator.Validate(entity, context);
		Assert.True(result.IsValid);
	}

	#endregion

	#region DataTypeValidator Tests

	[Fact]
	public void DataTypeValidator_ShouldPass_ForCompatibleNumericTypes()
	{
		var schema = new EntitySchema
		{
			TableName = "Data",
			Columns =
			{
				new ColumnSchema { Name = "Value", PropertyName = "Value", DataType = "Int32" }
			}
		};

		var validator = new DataTypeValidator();
		var entity = new ValueEntity { Value = 42 };
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(ValueEntity),
			TrackedEntities = new[] { entity }
		};

		var result = validator.Validate(entity, context);
		Assert.True(result.IsValid);
	}

	#endregion

	#region PrimaryKeyValidator Tests

	[Fact]
	public void PrimaryKeyValidator_ShouldPass_ForDefaultValue_NewEntity()
	{
		var schema = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Id", PropertyName = "Id", DataType = "Int32", IsPrimaryKey = true }
			}
		};

		var validator = new PrimaryKeyValidator();
		var entity = new IdEntity { Id = 0 }; // Default - new entity
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(IdEntity),
			TrackedEntities = new[] { entity }
		};

		var result = validator.Validate(entity, context);
		Assert.True(result.IsValid);
	}

	[Fact]
	public void PrimaryKeyValidator_ShouldFail_ForDuplicateAmongTracked()
	{
		var schema = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Id", PropertyName = "Id", DataType = "Int32", IsPrimaryKey = true }
			}
		};

		var validator = new PrimaryKeyValidator();
		var entity1 = new IdEntity { Id = 5 };
		var entity2 = new IdEntity { Id = 5 };
		var context = new ValidationContext
		{
			Schema = schema,
			EntityType = typeof(IdEntity),
			TrackedEntities = new object[] { entity1, entity2 }
		};

		var result = validator.Validate(entity1, context);
		Assert.False(result.IsValid);
	}

	#endregion

	private class PriceEntity { public decimal Price { get; set; } }
	private class EmailEntity { public string Email { get; set; } = ""; }
	private class AgeEntity { public int Age { get; set; } }
	private class ValueEntity { public int Value { get; set; } }
	private class IdEntity { public int Id { get; set; } }
}
