using Sheetly.Core.Configuration;
using Sheetly.Core.Diagnostics;
using Sheetly.Core.Tests.Integration.Helpers;

namespace Sheetly.Core.Tests;

/// <summary>F7 — LogTo sink logging and level filtering.</summary>
public class LoggingTests
{
	[Fact]
	public void Logger_RespectsMinimumLevel_AndFormats()
	{
		var lines = new List<string>();
		var logger = new SheetlyLogger(lines.Add, SheetlyLogLevel.Warning);

		logger.Log(SheetlyLogLevel.Debug, "quiet");
		logger.Log(SheetlyLogLevel.Warning, "loud");

		Assert.Single(lines);
		Assert.Contains("Warning", lines[0]);
		Assert.Contains("loud", lines[0]);
	}

	[Fact]
	public void LogTo_AttachesLoggerToSupportingProvider()
	{
		var provider = new InMemorySheetsProvider();
		var lines = new List<string>();
		var options = new SheetsOptions { Provider = provider };

		options.LogTo(lines.Add, SheetlyLogLevel.Debug);

		Assert.NotNull(provider.Logger);
		provider.Logger!.Log(SheetlyLogLevel.Information, "hello");
		Assert.Contains(lines, l => l.Contains("hello"));
	}
}
