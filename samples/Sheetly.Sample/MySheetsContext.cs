namespace Sheetly.Sample;

using Sheetly.Core;
using Sheetly.Sample.Models;

public class MySheetsContext : SheetsContext
{
    public SheetsSet<Product> Products { get; set; } = null!;

}