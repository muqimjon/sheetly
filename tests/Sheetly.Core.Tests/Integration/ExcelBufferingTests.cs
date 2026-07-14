using Sheetly.Excel;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// L1 — Excel writes are buffered in memory and hit disk once on FlushAsync/Dispose,
/// while reads still see uncommitted mutations through the in-memory workbook.
/// </summary>
public class ExcelBufferingTests : IDisposable
{
	private readonly string _path = Path.Combine(Path.GetTempPath(), $"sheetly_buf_{Guid.NewGuid():N}.xlsx");

	[Fact]
	public async Task Writes_ArePersisted_OnlyAfterFlush()
	{
		await using (var provider = new ExcelSheetProvider(_path))
		{
			await provider.InitializeAsync();
			await provider.CreateSheetAsync("T", ["A"]);
			await provider.AppendRowsAsync("T", [["1"], ["2"]]);

			// Buffered writes are visible to reads on the same instance.
			Assert.Equal(3, (await provider.GetAllRowsAsync("T")).Count);

			// Nothing written to disk yet.
			Assert.False(File.Exists(_path));

			await provider.FlushAsync();
			Assert.True(File.Exists(_path));
		}

		await using var reopened = new ExcelSheetProvider(_path);
		await reopened.InitializeAsync();
		var rows = await reopened.GetAllRowsAsync("T");
		Assert.Equal(3, rows.Count);
	}

	[Fact]
	public async Task Dispose_FlushesPendingWrites()
	{
		await using (var provider = new ExcelSheetProvider(_path))
		{
			await provider.InitializeAsync();
			await provider.CreateSheetAsync("T", ["A"]);
			await provider.AppendRowAsync("T", ["9"]);
		}

		await using var reopened = new ExcelSheetProvider(_path);
		await reopened.InitializeAsync();
		Assert.Equal(1, (await reopened.GetAllRowsAsync("T")).Count - 1);
	}

	[Fact]
	public async Task DropDatabase_OnAFileThatDoesNotExist_DoesNotThrow()
	{
		await using var provider = new ExcelSheetProvider(_path);
		await provider.InitializeAsync();

		await provider.DropDatabaseAsync();
		await provider.FlushAsync();

		Assert.False(File.Exists(_path));
	}

	[Fact]
	public async Task DropDatabase_OnAFileThatDoesNotExist_DoesNotSwallowTheNextWrite()
	{
		await using (var provider = new ExcelSheetProvider(_path))
		{
			await provider.InitializeAsync();
			await provider.DropDatabaseAsync();
			await provider.FlushAsync();

			await provider.CreateSheetAsync("T", ["A"]);
			await provider.AppendRowAsync("T", ["1"]);
		}

		await using var reopened = new ExcelSheetProvider(_path);
		await reopened.InitializeAsync();
		Assert.Equal(2, (await reopened.GetAllRowsAsync("T")).Count);
	}

	public void Dispose()
	{
		if (File.Exists(_path)) File.Delete(_path);
	}
}
