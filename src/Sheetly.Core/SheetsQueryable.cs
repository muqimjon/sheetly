namespace Sheetly.Core;

/// <summary>
/// Deferred, in-memory query over a <see cref="SheetsSet{T}"/>. Because the Google Sheets
/// and Excel backends have no server-side query language, ordering/paging/projection are
/// applied client-side after the rows are fetched once. Composition is lazy — nothing runs
/// until a terminal method (ToListAsync/FirstOrDefaultAsync/CountAsync/AnyAsync) is awaited.
/// </summary>
public sealed class SheetsQueryable<T>
{
	private readonly Func<Task<List<T>>> _source;
	private readonly Func<IEnumerable<T>, IEnumerable<T>> _pipeline;

	internal SheetsQueryable(Func<Task<List<T>>> source, Func<IEnumerable<T>, IEnumerable<T>> pipeline)
	{
		_source = source;
		_pipeline = pipeline;
	}

	private SheetsQueryable<T> Chain(Func<IEnumerable<T>, IEnumerable<T>> op)
		=> new(_source, items => op(_pipeline(items)));

	public SheetsQueryable<T> Where(Func<T, bool> predicate) => Chain(s => s.Where(predicate));
	public SheetsQueryable<T> OrderBy<TKey>(Func<T, TKey> keySelector) => Chain(s => s.OrderBy(keySelector));
	public SheetsQueryable<T> OrderByDescending<TKey>(Func<T, TKey> keySelector) => Chain(s => s.OrderByDescending(keySelector));
	public SheetsQueryable<T> Skip(int count) => Chain(s => s.Skip(count));
	public SheetsQueryable<T> Take(int count) => Chain(s => s.Take(count));

	public async Task<List<T>> ToListAsync() => _pipeline(await _source()).ToList();

	public async Task<List<TResult>> SelectAsync<TResult>(Func<T, TResult> selector)
		=> _pipeline(await _source()).Select(selector).ToList();

	public async Task<T?> FirstOrDefaultAsync(Func<T, bool>? predicate = null)
	{
		var query = _pipeline(await _source());
		return predicate is null ? query.FirstOrDefault() : query.FirstOrDefault(predicate);
	}

	public async Task<int> CountAsync() => _pipeline(await _source()).Count();

	public async Task<bool> AnyAsync(Func<T, bool>? predicate = null)
	{
		var query = _pipeline(await _source());
		return predicate is null ? query.Any() : query.Any(predicate);
	}
}
