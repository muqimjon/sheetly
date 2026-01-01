namespace Sheetly.Core;

using System.Linq.Expressions;

public class SheetsSetQuery<T> where T : class, new()
{
	private readonly SheetsSet<T> sheetsSet;
	private Expression<Func<T, bool>>? wherePredicate;
	private Expression? orderByExpression;

	internal SheetsSetQuery(SheetsSet<T> sheetsSet, Expression<Func<T, bool>>? wherePredicate = null)
	{
		this.sheetsSet = sheetsSet;
		this.wherePredicate = wherePredicate;
	}

	public SheetsSetQuery<T> Where(Expression<Func<T, bool>> predicate)
	{
		wherePredicate = predicate;
		return this;
	}

	public SheetsSetQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> keySelector)
	{
		orderByExpression = keySelector;
		return this;
	}

	public async Task<List<T>> ToListAsync()
	{
		var all = await sheetsSet.ToListAsync();

		if (wherePredicate != null)
		{
			var compiled = wherePredicate.Compile();
			all = all.Where(compiled).ToList();
		}

		return all;
	}

	public async Task<T?> FirstOrDefaultAsync()
	{
		var list = await ToListAsync();
		return list.FirstOrDefault();
	}
}
