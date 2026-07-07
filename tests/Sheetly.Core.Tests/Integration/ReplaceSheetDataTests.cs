using Sheetly.Core.Tests.Integration.Helpers;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// M6 — ReplaceSheetDataAsync swaps a sheet's whole contents (header + data) in one shot,
/// leaving exactly the new rows behind (old trailing rows removed).
/// </summary>
public class ReplaceSheetDataTests
{
	[Fact]
	public async Task ReplaceSheetData_ShrinksToExactRows()
	{
		var provider = new InMemorySheetsProvider();
		await provider.CreateSheetAsync("T", ["A", "B"]);
		await provider.AppendRowsAsync("T", [["1", "x"], ["2", "y"], ["3", "z"]]);

		await provider.ReplaceSheetDataAsync("T", [["A", "B"], ["9", "keep"]]);

		var rows = await provider.GetAllRowsAsync("T");
		Assert.Equal(2, rows.Count);
		Assert.Equal("A", rows[0][0]);
		Assert.Equal("9", rows[1][0]);
		Assert.Equal("keep", rows[1][1]);
	}

	[Fact]
	public async Task ReplaceSheetData_HeaderOnly_LeavesNoDataRows()
	{
		var provider = new InMemorySheetsProvider();
		await provider.CreateSheetAsync("T", ["A"]);
		await provider.AppendRowsAsync("T", [["1"], ["2"]]);

		await provider.ReplaceSheetDataAsync("T", [["A"]]);

		Assert.Equal(0, provider.DataRowCount("T"));
	}
}
