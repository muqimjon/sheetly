namespace Sheetly.Core.Tests.Integration.Models;

public class Category
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public List<Product> Products { get; set; } = [];
}

public class Product
{
	public int Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public decimal Price { get; set; }
	public string? Description { get; set; }
	public int Stock { get; set; }

	public int CategoryId { get; set; }
	public Category Category { get; set; } = default!;
}

public class Tag
{
	public int Id { get; set; }
	public string Label { get; set; } = string.Empty;
}

public class UserAccount
{
	[System.ComponentModel.DataAnnotations.Key]
	public string Username { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
}
