using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;
using System.Reflection;
using System.Runtime.Loader;

namespace Sheetly.CLI.Helpers;

/// <summary>
/// Loads a target project's DLL and all its dependencies in an isolated context,
/// preventing MVID conflicts with assemblies already loaded by the CLI tool itself.
/// When the default resolver can't locate a dependency, it falls back to the project's
/// deps.json so provider assemblies (ClosedXML, Google.Apis, …) resolve from the NuGet
/// cache even when the context lives in a class library that doesn't copy them to bin.
/// </summary>
internal sealed class ProjectAssemblyLoadContext : AssemblyLoadContext
{
	private readonly AssemblyDependencyResolver _resolver;
	private readonly string _baseDirectory;
	private readonly DependencyContext? _dependencyContext;
	private readonly ICompilationAssemblyResolver _assemblyResolver;

	public ProjectAssemblyLoadContext(string dllPath) : base(isCollectible: true)
	{
		var fullPath = Path.GetFullPath(dllPath);
		_baseDirectory = Path.GetDirectoryName(fullPath)!;
		_resolver = new AssemblyDependencyResolver(fullPath);
		_dependencyContext = LoadDependencyContext(fullPath);
		_assemblyResolver = new CompositeCompilationAssemblyResolver(
		[
			new AppBaseCompilationAssemblyResolver(_baseDirectory),
			new ReferenceAssemblyPathResolver(),
			new PackageCompilationAssemblyResolver()
		]);
	}

	protected override Assembly? Load(AssemblyName assemblyName)
	{
		var path = _resolver.ResolveAssemblyToPath(assemblyName)
			?? ResolveFromDependencyContext(assemblyName)
			?? ProbeBaseDirectory(assemblyName);

		return path is not null ? LoadFromAssemblyPath(path) : null;
	}

	private string? ResolveFromDependencyContext(AssemblyName assemblyName)
	{
		if (_dependencyContext is null) return null;

		foreach (var library in _dependencyContext.RuntimeLibraries)
		{
			var assets = library.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths).ToList();
			if (!assets.Any(a => NameMatches(a, assemblyName.Name)))
				continue;

			var wrapper = new CompilationLibrary(
				library.Type, library.Name, library.Version, library.Hash,
				assets, library.Dependencies, library.Serviceable);

			var resolved = new List<string>();
			_assemblyResolver.TryResolveAssemblyPaths(wrapper, resolved);
			var match = resolved.FirstOrDefault(p => NameMatches(p, assemblyName.Name));
			if (match is not null) return match;
		}

		return null;
	}

	private string? ProbeBaseDirectory(AssemblyName assemblyName)
	{
		if (assemblyName.Name is null) return null;
		var candidate = Path.Combine(_baseDirectory, assemblyName.Name + ".dll");
		return File.Exists(candidate) ? candidate : null;
	}

	private static bool NameMatches(string path, string? name) =>
		name is not null && Path.GetFileNameWithoutExtension(path).Equals(name, StringComparison.OrdinalIgnoreCase);

	private static DependencyContext? LoadDependencyContext(string dllPath)
	{
		var depsPath = Path.ChangeExtension(dllPath, ".deps.json");
		if (!File.Exists(depsPath)) return null;
		using var stream = File.OpenRead(depsPath);
		return new DependencyContextJsonReader().Read(stream);
	}
}
