namespace Sheetly.Core.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class TableAttribute(string name) : Attribute { public string Name { get; } = name; }

[AttributeUsage(AttributeTargets.Property)]
public class ColumnAttribute(string name) : Attribute { public string Name { get; } = name; }

[AttributeUsage(AttributeTargets.Property)]
public class PrimaryKeyAttribute : Attribute { public bool AutoIncrement { get; set; } = true; }

[AttributeUsage(AttributeTargets.Property)]
public class IgnoreAttribute : Attribute { }