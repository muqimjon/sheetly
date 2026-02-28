using System.Reflection;

namespace Sheetly.Core.Design;

public static class ContextResolver
{
	public static SheetsContext CreateContextFromAssembly(Assembly assembly, string[] args)
	{
		var factoryType = assembly.GetTypes().FirstOrDefault(t =>
			!t.IsInterface && !t.IsAbstract &&
			t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDesignTimeSheetsContextFactory<>)));

		if (factoryType is not null)
		{
			var factory = Activator.CreateInstance(factoryType);
			var method = factoryType.GetMethod("CreateDbContext");
			return (SheetsContext)method!.Invoke(factory, [args])!;
		}

		var contextType = assembly.GetTypes().FirstOrDefault(t =>
			t.BaseType is not null && (t.BaseType.Name == "SheetsContext" || t.BaseType.Name.Contains("SheetsContext")) && !t.IsAbstract);

		if (contextType is null) throw new Exception("Project does not contain a class inheriting from SheetsContext.");

		return (SheetsContext)Activator.CreateInstance(contextType)!;
	}
}