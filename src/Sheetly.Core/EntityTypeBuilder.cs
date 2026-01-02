using System.Linq.Expressions;

namespace Sheetly.Core;

public class EntityTypeBuilder<T> : EntityMetadata where T : class
{
	public EntityTypeBuilder<T> HasSheetName(string name)
	{
		SheetName = name;
		return this;
	}

	public EntityTypeBuilder<T> HasKey(Expression<Func<T, object>> keyExpression)
	{
		PrimaryKey = GetPropertyName(keyExpression);
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