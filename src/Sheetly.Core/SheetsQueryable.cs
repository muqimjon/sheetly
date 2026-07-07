using System.Runtime.CompilerServices;

namespace Sheetly.Core;

/// <summary>
/// Deferred, in-memory query over a <see cref="SheetsSet{T}"/>. Because the Google Sheets
/// and Excel backends have no server-side query language, ordering/paging/projection are
/// applied client-side after the rows are fetched once. Composition is lazy — nothing runs
/// until a terminal method is awaited.
/// </summary>
public class SheetsQueryable<T>
{
	private protected readonly Func<Task<List<T>>> _source;
	private protected readonly Func<IEnumerable<T>, IEnumerable<T>> _pipeline;

	internal SheetsQueryable(Func<Task<List<T>>> source, Func<IEnumerable<T>, IEnumerable<T>> pipeline)
	{
		_source = source;
		_pipeline = pipeline;
	}

	private SheetsQueryable<T> Chain(Func<IEnumerable<T>, IEnumerable<T>> op)
		=> new(_source, items => op(_pipeline(items)));

	public SheetsQueryable<T> Where(Func<T, bool> predicate) => Chain(s => s.Where(predicate));
	public SheetsQueryable<T> Skip(int count) => Chain(s => s.Skip(count));
	public SheetsQueryable<T> Take(int count) => Chain(s => s.Take(count));
	public SheetsQueryable<T> Distinct() => Chain(s => s.Distinct());
	public SheetsQueryable<T> DistinctBy<TKey>(Func<T, TKey> keySelector) => Chain(s => s.DistinctBy(keySelector));

	public OrderedSheetsQueryable<T> OrderBy<TKey>(Func<T, TKey> keySelector)
		=> new(_source, _pipeline, s => s.OrderBy(keySelector));
	public OrderedSheetsQueryable<T> OrderByDescending<TKey>(Func<T, TKey> keySelector)
		=> new(_source, _pipeline, s => s.OrderByDescending(keySelector));

	/// <summary>Deferred projection — composes further and runs on the terminal.</summary>
	public SheetsQueryable<TResult> Select<TResult>(Func<T, TResult> selector)
		=> new(async () => _pipeline(await _source()).Select(selector).ToList(), x => x);

	private async Task<IEnumerable<T>> RunAsync() => _pipeline(await _source());

	public async Task<List<T>> ToListAsync(CancellationToken ct = default) => (await RunAsync()).ToList();
	public async Task<T[]> ToArrayAsync(CancellationToken ct = default) => (await RunAsync()).ToArray();
	public async Task<HashSet<T>> ToHashSetAsync(CancellationToken ct = default) => (await RunAsync()).ToHashSet();

	public async Task<Dictionary<TKey, T>> ToDictionaryAsync<TKey>(Func<T, TKey> keySelector, CancellationToken ct = default) where TKey : notnull
		=> (await RunAsync()).ToDictionary(keySelector);
	public async Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(Func<T, TKey> keySelector, Func<T, TValue> valueSelector, CancellationToken ct = default) where TKey : notnull
		=> (await RunAsync()).ToDictionary(keySelector, valueSelector);

	/// <summary>Terminal projection kept for source compatibility; prefer the composable <see cref="Select"/>.</summary>
	public async Task<List<TResult>> SelectAsync<TResult>(Func<T, TResult> selector)
		=> (await RunAsync()).Select(selector).ToList();

	public async Task<T?> FirstOrDefaultAsync(Func<T, bool>? predicate = null)
	{
		var q = await RunAsync();
		return predicate is null ? q.FirstOrDefault() : q.FirstOrDefault(predicate);
	}

	public async Task<T> FirstAsync(Func<T, bool>? predicate = null)
	{
		var q = await RunAsync();
		return predicate is null ? q.First() : q.First(predicate);
	}

	public async Task<T?> SingleOrDefaultAsync(Func<T, bool>? predicate = null)
	{
		var q = await RunAsync();
		return predicate is null ? q.SingleOrDefault() : q.SingleOrDefault(predicate);
	}

	public async Task<T> SingleAsync(Func<T, bool>? predicate = null)
	{
		var q = await RunAsync();
		return predicate is null ? q.Single() : q.Single(predicate);
	}

	public async Task<T?> LastOrDefaultAsync(Func<T, bool>? predicate = null)
	{
		var q = await RunAsync();
		return predicate is null ? q.LastOrDefault() : q.LastOrDefault(predicate);
	}

	public async Task<T> LastAsync(Func<T, bool>? predicate = null)
	{
		var q = await RunAsync();
		return predicate is null ? q.Last() : q.Last(predicate);
	}

	public async Task<int> CountAsync(Func<T, bool>? predicate = null)
	{
		var q = await RunAsync();
		return predicate is null ? q.Count() : q.Count(predicate);
	}

	public async Task<long> LongCountAsync(Func<T, bool>? predicate = null)
	{
		var q = await RunAsync();
		return predicate is null ? q.LongCount() : q.LongCount(predicate);
	}

	public async Task<bool> AnyAsync(Func<T, bool>? predicate = null)
	{
		var q = await RunAsync();
		return predicate is null ? q.Any() : q.Any(predicate);
	}

	public async Task<bool> AllAsync(Func<T, bool> predicate) => (await RunAsync()).All(predicate);

	public async Task<TResult?> MaxAsync<TResult>(Func<T, TResult> selector) => (await RunAsync()).Max(selector);
	public async Task<TResult?> MinAsync<TResult>(Func<T, TResult> selector) => (await RunAsync()).Min(selector);

	public async Task<decimal> SumAsync(Func<T, decimal> selector) => (await RunAsync()).Sum(selector);
	public async Task<double> SumAsync(Func<T, double> selector) => (await RunAsync()).Sum(selector);
	public async Task<int> SumAsync(Func<T, int> selector) => (await RunAsync()).Sum(selector);
	public async Task<long> SumAsync(Func<T, long> selector) => (await RunAsync()).Sum(selector);

	public async Task<decimal> AverageAsync(Func<T, decimal> selector) => (await RunAsync()).Average(selector);
	public async Task<double> AverageAsync(Func<T, double> selector) => (await RunAsync()).Average(selector);
	public async Task<double> AverageAsync(Func<T, int> selector) => (await RunAsync()).Average(selector);
	public async Task<double> AverageAsync(Func<T, long> selector) => (await RunAsync()).Average(selector);

	public async IAsyncEnumerable<T> AsAsyncEnumerable([EnumeratorCancellation] CancellationToken ct = default)
	{
		foreach (var item in await RunAsync())
		{
			ct.ThrowIfCancellationRequested();
			yield return item;
		}
	}
}

/// <summary>
/// An ordered <see cref="SheetsQueryable{T}"/> that supports <c>ThenBy</c>/<c>ThenByDescending</c>,
/// mirroring EF Core / LINQ ordering composition.
/// </summary>
public sealed class OrderedSheetsQueryable<T> : SheetsQueryable<T>
{
	private readonly Func<IEnumerable<T>, IEnumerable<T>> _prePipeline;
	private readonly Func<IEnumerable<T>, IOrderedEnumerable<T>> _order;

	internal OrderedSheetsQueryable(
		Func<Task<List<T>>> source,
		Func<IEnumerable<T>, IEnumerable<T>> prePipeline,
		Func<IEnumerable<T>, IOrderedEnumerable<T>> order)
		: base(source, items => order(prePipeline(items)))
	{
		_prePipeline = prePipeline;
		_order = order;
	}

	public OrderedSheetsQueryable<T> ThenBy<TKey>(Func<T, TKey> keySelector)
		=> new(_source, _prePipeline, s => _order(s).ThenBy(keySelector));
	public OrderedSheetsQueryable<T> ThenByDescending<TKey>(Func<T, TKey> keySelector)
		=> new(_source, _prePipeline, s => _order(s).ThenByDescending(keySelector));
}
