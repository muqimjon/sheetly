namespace Sheetly.Sample.Models;

public class Category
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
	public List<Product> Products { get; set; } = [];
}

public class Product
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
	public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; }
}