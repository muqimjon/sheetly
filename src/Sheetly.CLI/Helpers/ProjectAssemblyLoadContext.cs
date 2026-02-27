using System.Reflection;
using System.Runtime.Loader;

namespace Sheetly.CLI.Helpers;

/// <summary>
/// Loads a target project's DLL and all its dependencies in an isolated context,
/// preventing MVID conflicts with assemblies already loaded by the CLI tool itself.
/// </summary>
internal sealed class ProjectAssemblyLoadContext : AssemblyLoadContext
{
	private readonly AssemblyDependencyResolver _resolver;

	public ProjectAssemblyLoadContext(string dllPath) : base(isCollectible: true)
	{
		_resolver = new AssemblyDependencyResolver(dllPath);
	}

	protected override Assembly? Load(AssemblyName assemblyName)
	{
		// Resolve from the project's own bin directory first
		var path = _resolver.ResolveAssemblyToPath(assemblyName);
		return path != null ? LoadFromAssemblyPath(path) : null;
	}
}
