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

public enum OrderStatus
{
	Pending,
	Shipped,
	Delivered
}

public class Order
{
	public int Id { get; set; }
	public string Customer { get; set; } = string.Empty;
	public OrderStatus Status { get; set; }
	public decimal Total { get; set; }
}

public class Department
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public List<Employee> Employees { get; set; } = [];
}

public class Employee
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int DepartmentId { get; set; }
	public Department Department { get; set; } = default!;
}

public class Document
{
	public int Id { get; set; }
	public string Title { get; set; } = string.Empty;
	public int Version { get; set; }
}

public class OrderLine
{
	public int OrderId { get; set; }
	public int LineNo { get; set; }
	public string Product { get; set; } = string.Empty;
	public int Quantity { get; set; }
}

public class UserAccount
{
	[System.ComponentModel.DataAnnotations.Key]
	public string Username { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
}
