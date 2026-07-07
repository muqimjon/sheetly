namespace Sheetly.Core.Diagnostics;

public enum SheetlyLogLevel
{
	Debug,
	Information,
	Warning,
	Error
}

/// <summary>
/// A minimal sink-based logger (the target of <c>LogTo</c>), mirroring EF Core's simple logging.
/// Messages at or above <see cref="MinimumLevel"/> are written to the configured delegate.
/// </summary>
public sealed class SheetlyLogger(Action<string> sink, SheetlyLogLevel minimumLevel)
{
	public SheetlyLogLevel MinimumLevel { get; } = minimumLevel;

	public bool IsEnabled(SheetlyLogLevel level) => level >= MinimumLevel;

	public void Log(SheetlyLogLevel level, string message)
	{
		if (level >= MinimumLevel) sink($"[Sheetly:{level}] {message}");
	}
}

/// <summary>
/// Opt-in interface a provider or migration service implements to receive the configured
/// <see cref="SheetlyLogger"/>. Kept off <c>ISheetsProvider</c> so logging stays optional.
/// </summary>
public interface ISupportsLogging
{
	void SetLogger(SheetlyLogger logger);
}
