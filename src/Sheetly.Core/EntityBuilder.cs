namespace Sheetly.Core;

public abstract class EntityBuilder { }

public class EntityBuilder<T> : EntityBuilder where T : class
{
	internal string? TableName { get; private set; }
	internal string? PrimaryKeyProperty { get; private set; }
	internal Dictionary<string, PropertyBuilder> Properties { get; } = new();

	public EntityBuilder<T> ToTable(string name)
	{
		TableName = name;
		return this;
	}

	public EntityBuilder<T> HasKey(string propertyName)
	{
		PrimaryKeyProperty = propertyName;
		return this;
	}
	
	public EntityBuilder<T> HasSheetName(string name)
	{
		TableName = name;
		return this;
	}
	
	public EntityBuilder<T> HasKey<TProperty>(System.Linq.Expressions.Expression<Func<T, TProperty>> keyExpression)
	{
		if (keyExpression.Body is System.Linq.Expressions.MemberExpression memberExpr)
		{
			PrimaryKeyProperty = memberExpr.Member.Name;
		}
		return this;
	}
	
	public PropertyBuilder Property<TProperty>(System.Linq.Expressions.Expression<Func<T, TProperty>> propertyExpression)
	{
		if (propertyExpression.Body is System.Linq.Expressions.MemberExpression memberExpr)
		{
			var propertyName = memberExpr.Member.Name;
			if (!Properties.ContainsKey(propertyName))
			{
				Properties[propertyName] = new PropertyBuilder(propertyName);
			}
			return Properties[propertyName];
		}
		throw new ArgumentException("Invalid property expression");
	}
}
