using System.ComponentModel.DataAnnotations;

namespace Sheetly.Test.Models;

public class Product
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? Description { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
