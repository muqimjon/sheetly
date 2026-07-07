using System.Linq.Expressions;

namespace Sheetly.Core;

public class EntityTypeBuilder<T> : EntityMetadata where T : class
{
	public EntityTypeBuilder<T> HasSheetName(string name)
	{
		SheetName = name;
		return this;
	}

	/// <summary>EF Core muscle-memory alias for <see cref="HasSheetName"/> — the sheet is the table.</summary>
	public EntityTypeBuilder<T> ToTable(string name) => HasSheetName(name);

	public EntityTypeBuilder<T> HasKey(Expression<Func<T, object>> keyExpression)
	{
		PrimaryKeys.Clear();
		var body = keyExpression.Body;
		if (body is UnaryExpression unary) body = unary.Operand;

		if (body is NewExpression composite)
		{
			foreach (var arg in composite.Arguments)
				if (arg is MemberExpression m) PrimaryKeys.Add(m.Member.Name);
		}
		else if (body is MemberExpression member)
		{
			PrimaryKeys.Add(member.Member.Name);
		}
		else
		{
			throw new ArgumentException("Invalid key expression. Use e => e.Id or e => new { e.A, e.B }.");
		}

		return this;
	}

	/// <summary>Excludes a property from the model (EF Core <c>Ignore</c> / <c>[NotMapped]</c> equivalent).</summary>
	public EntityTypeBuilder<T> Ignore<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
	{
		IgnoredProperties.Add(GetPropertyName(propertyExpression));
		return this;
	}

	public PropertyBuilder Property<TProperty>(Expression<Func<T, TProperty>> propertyExpression)
	{
		var name = GetPropertyName(propertyExpression);
		if (!Properties.TryGetValue(name, out PropertyBuilder? value))
		{
			value = new PropertyBuilder(name);
			Properties[name] = value;
		}
		return value;
	}

	private static string GetPropertyName(LambdaExpression expression)
	{
		var body = expression.Body;
		if (body is UnaryExpression unary) body = unary.Operand;
		if (body is MemberExpression member) return member.Member.Name;
		throw new ArgumentException("Invalid expression");
	}
}