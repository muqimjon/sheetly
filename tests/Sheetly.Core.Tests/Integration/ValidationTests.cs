using Sheetly.Core.Tests.Integration.Models;
using Sheetly.Core.Validation;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Tests that model-level validation rules (IsRequired, MaxLength, MinLength,
/// HasRange) are enforced during SaveChangesAsync — before any data reaches
/// the backing store.
/// </summary>
public class ValidationTests
{
	// ── IsRequired ────────────────────────────────────────────────────────────

	[Fact]
	public async Task Save_CategoryWithNullName_ThrowsValidationException()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = null! });

		var ex = await Assert.ThrowsAsync<ValidationException>(
			() => ctx.SaveChangesAsync());

		Assert.Contains(ex.ValidationResult.Errors, e => e.PropertyName == "Name");
	}

	[Fact]
	public async Task Save_CategoryWithEmptyName_ThrowsValidationException()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = string.Empty });

		await Assert.ThrowsAsync<ValidationException>(() => ctx.SaveChangesAsync());
	}

	// ── MaxLength ─────────────────────────────────────────────────────────────

	[Fact]
	public async Task Save_CategoryNameTooLong_ThrowsValidationException()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = new string('X', 101) }); // MaxLength = 100

		var ex = await Assert.ThrowsAsync<ValidationException>(
			() => ctx.SaveChangesAsync());

		Assert.Contains(ex.ValidationResult.Errors,
			e => e.PropertyName == "Name" && e.Message.Contains("must not exceed"));
	}

	[Fact]
	public async Task Save_CategoryNameAtExactMaxLength_Succeeds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = new string('A', 100) }; // exactly 100
		ctx.Categories.Add(category);

		await ctx.SaveChangesAsync(); // must not throw

		Assert.True(category.Id > 0);
	}

	[Fact]
	public async Task Save_ProductTitleTooLong_ThrowsValidationException()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Tech" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product
		{
			Title = new string('T', 201), // MaxLength = 200
			Price = 10m,
			CategoryId = cat.Id
		});

		var ex = await Assert.ThrowsAsync<ValidationException>(
			() => ctx.SaveChangesAsync());

		Assert.Contains(ex.ValidationResult.Errors, e => e.PropertyName == "Title");
	}

	[Fact]
	public async Task Save_ProductDescriptionAtMaxLength_Succeeds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Tech" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product
		{
			Title = "Widget",
			Price = 5m,
			Description = new string('D', 500), // exactly 500 = MaxLength
			CategoryId = cat.Id
		});

		await ctx.SaveChangesAsync(); // must not throw
	}

	// ── MinLength ─────────────────────────────────────────────────────────────

	[Fact]
	public async Task Save_CategoryNameTooShort_ThrowsValidationException()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Categories.Add(new Category { Name = "A" }); // MinLength = 2

		var ex = await Assert.ThrowsAsync<ValidationException>(
			() => ctx.SaveChangesAsync());

		Assert.Contains(ex.ValidationResult.Errors,
			e => e.PropertyName == "Name" && e.Message.Contains("at least"));
	}

	[Fact]
	public async Task Save_CategoryNameAtMinLength_Succeeds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var category = new Category { Name = "AB" }; // exactly 2 = MinLength
		ctx.Categories.Add(category);

		await ctx.SaveChangesAsync();

		Assert.True(category.Id > 0);
	}

	// ── HasRange ──────────────────────────────────────────────────────────────

	[Fact]
	public async Task Save_ProductPriceBelowMinRange_ThrowsValidationException()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Test" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product
		{
			Title = "Cheap",
			Price = -1m, // Range: 0 – 999999
			CategoryId = cat.Id
		});

		var ex = await Assert.ThrowsAsync<ValidationException>(
			() => ctx.SaveChangesAsync());

		Assert.Contains(ex.ValidationResult.Errors, e => e.PropertyName == "Price");
	}

	[Fact]
	public async Task Save_ProductPriceAboveMaxRange_ThrowsValidationException()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Test" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product
		{
			Title = "Expensive",
			Price = 1_000_000m, // exceeds 999999
			CategoryId = cat.Id
		});

		var ex = await Assert.ThrowsAsync<ValidationException>(
			() => ctx.SaveChangesAsync());

		Assert.Contains(ex.ValidationResult.Errors, e => e.PropertyName == "Price");
	}

	[Fact]
	public async Task Save_ProductPriceAtMinRange_Succeeds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Test" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product
		{
			Title = "Free Item",
			Price = 0m, // exactly at min
			CategoryId = cat.Id
		});

		await ctx.SaveChangesAsync(); // must not throw
	}

	[Fact]
	public async Task Save_ProductPriceAtMaxRange_Succeeds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Test" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product
		{
			Title = "Luxury",
			Price = 999_999m, // exactly at max
			CategoryId = cat.Id
		});

		await ctx.SaveChangesAsync(); // must not throw
	}

	// ── No validation error for valid data ────────────────────────────────────

	[Fact]
	public async Task Save_AllConstraintsSatisfied_Succeeds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		var cat = new Category { Name = "Valid Category" };
		ctx.Categories.Add(cat);
		await ctx.SaveChangesAsync();

		ctx.Products.Add(new Product
		{
			Title = "Valid Product",
			Price = 49.99m,
			Description = "This is fine.",
			CategoryId = cat.Id
		});

		await ctx.SaveChangesAsync(); // must not throw
	}
}
