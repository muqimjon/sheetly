using Sheetly.Core.Migration;
using Sheetly.Core.Validation;
using Sheetly.Core.Validation.Rules;

namespace Sheetly.Core.Tests;

public class ConstraintValidatorTests
{
	private readonly EntitySchema _userSchema;
	private readonly MigrationSnapshot _snapshot;

	public ConstraintValidatorTests()
	{
		_userSchema = new EntitySchema
		{
			TableName = "Users",
			Columns =
			{
				new ColumnSchema { Name = "Id", PropertyName = "Id", DataType = "Int32", IsPrimaryKey = true },
				new ColumnSchema { Name = "Username", PropertyName = "Username", DataType = "String", IsNullable = false, MaxLength = 50 },
				new ColumnSchema { Name = "Age", PropertyName = "Age", DataType = "Int32", IsNullable = true }
			}
		};

		_snapshot = new MigrationSnapshot();
		_snapshot.Entities["Users"] = _userSchema;
	}

	[Fact]
	public void Validate_ShouldPass_ForValidUser()
	{
		// Arrange
		var validator = new ConstraintValidator(_snapshot);
		var user = new User { Id = 1, Username = "testuser", Age = 25 };
		var context = CreateContext(user);

		// Act
		var result = validator.Validate(user, context);

		// Assert
		Assert.True(result.IsValid);
	}

	[Fact]
	public void Validate_ShouldFail_WhenRequiredFieldIsNull()
	{
		// Arrange
		var validator = new ConstraintValidator(_snapshot);
		var user = new User { Id = 1, Username = null!, Age = 25 }; // Null required field
		var context = CreateContext(user);

		// Act
		var result = validator.Validate(user, context);

		// Assert
		Assert.False(result.IsValid);
		Assert.Contains(result.Errors, e => e.PropertyName == "Username");
	}

	[Fact]
	public void Validate_ShouldFail_WhenMaxLengthExceeded()
	{
		// Arrange
		var validator = new ConstraintValidator(_snapshot);
		var user = new User { Id = 1, Username = new string('a', 51), Age = 25 }; // 51 chars > 50
		var context = CreateContext(user);

		// Act
		var result = validator.Validate(user, context);

		// Assert
		Assert.False(result.IsValid);
		Assert.Contains(result.Errors, e => e.PropertyName == "Username" && e.Message.Contains("must not exceed"));
	}

	private ValidationContext CreateContext(object user)
	{
		return new ValidationContext
		{
			TrackedEntities = new[] { user },
			Schema = _userSchema,
			EntityType = typeof(User)
		};
	}

	private class User
	{
		public int Id { get; set; }
		public string Username { get; set; } = string.Empty;
		public int? Age { get; set; }
	}
}
