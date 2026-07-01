namespace Sheetly.Core;

/// <summary>
/// Thrown when an optimistic-concurrency check fails: the row was modified by someone else
/// between the time it was loaded and the time the update was saved. Mirrors EF Core.
/// </summary>
public class DbUpdateConcurrencyException(string message) : Exception(message);
