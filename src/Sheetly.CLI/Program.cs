using Sheetly.Core;
using Sheetly.Core.Migration;
using System.CommandLine;
using System.Reflection;
using System.Text.Json;

// 1. Root Command yaratish
RootCommand rootCommand = new("Sheetly CLI - Professional Google Sheets ORM Tool");

// 2. Argument va Optionlarni aniqlash
var nameArg = new Argument<string>("name") { Description = "Migration name" };
var projectOption = new Option<string?>("--project", "-p") { Description = "Manual path to DLL" };
var noBuildOption = new Option<bool>("--no-build") { Description = "Do not build the project before running." };

// 3. Commandlarni yaratish
var migrationsCommand = new Command("migrations", "Manage migrations");
var addCommand = new Command("add", "Create migration");
var updateCommand = new Command("update-sheets", "Update Google Sheets");

// 4. Strukturani yig'ish (Yangi v2.0.1 sintaksisi: .Subcommands, .Options, .Arguments)
addCommand.Arguments.Add(nameArg);
addCommand.Options.Add(projectOption);
addCommand.Options.Add(noBuildOption);

updateCommand.Options.Add(projectOption);
updateCommand.Options.Add(noBuildOption);

migrationsCommand.Subcommands.Add(addCommand);
rootCommand.Subcommands.Add(migrationsCommand);
rootCommand.Subcommands.Add(updateCommand);

// --- ADD MIGRATION ACTION ---
addCommand.SetAction(async (parseResult, ct) =>
{
	Console.WriteLine("🚀 Sheetly CLI: Starting 'add migration' process...");

	string name = parseResult.GetValue(nameArg)!;
	string? manualPath = parseResult.GetValue(projectOption);
	bool noBuild = parseResult.GetValue(noBuildOption);

	// DLL yo'lini aniqlash
	string dllPath = manualPath ?? FindProjectDll(noBuild);

	if (string.IsNullOrEmpty(dllPath))
	{
		Console.WriteLine("❌ Action aborted: Could not resolve Project DLL.");
		return;
	}

	Console.WriteLine($"🔍 Loading Assembly: {dllPath}");

	try
	{
		var assembly = Assembly.LoadFrom(Path.GetFullPath(dllPath));
		var contextType = assembly.GetTypes().FirstOrDefault(t =>
			t.BaseType != null && (t.BaseType.Name == "SheetsContext" || t.BaseType.Name.Contains("SheetsContext")));

		if (contextType == null)
		{
			Console.WriteLine("❌ Error: A class inheriting from 'SheetsContext' was not found in your project.");
			return;
		}

		var context = (SheetsContext)Activator.CreateInstance(contextType)!;
		var modelBuilder = new ModelBuilder();

		var onModelCreatingMethod = contextType.GetMethod("OnModelCreating", BindingFlags.NonPublic | BindingFlags.Instance);
		onModelCreatingMethod?.Invoke(context, [modelBuilder]);

		var snapshot = MigrationBuilder.BuildFromContext(contextType, modelBuilder);

		string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Migrations");
		Directory.CreateDirectory(outputDir);

		string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
		string fileName = $"{timestamp}_{name}.json";

		await File.WriteAllTextAsync(Path.Combine(outputDir, fileName), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
		await File.WriteAllTextAsync(Path.Combine(outputDir, "sheetly_snapshot.json"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));

		Console.WriteLine($"✅ Success! Migration '{name}' created.");
		Console.WriteLine($"📂 Location: Migrations/{fileName}");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"❌ Critical Error: {ex.Message}");
	}
});

// --- UPDATE SHEETS ACTION ---
updateCommand.SetAction(async (parseResult, ct) =>
{
	Console.WriteLine("🚀 Sheetly CLI: Starting 'update-sheets' process...");
	// Update mantiqi...
});

// 5. Invoke (Yangi v2.0.1 sintaksisi)
return await rootCommand.Parse(args).InvokeAsync();

// --- YORDAMCHI METODLAR ---
static string FindProjectDll(bool noBuild)
{
	var currentDir = Directory.GetCurrentDirectory();
	var csproj = Directory.GetFiles(currentDir, "*.csproj").FirstOrDefault();

	if (csproj == null)
	{
		Console.WriteLine("❌ Error: No .csproj file found.");
		return string.Empty;
	}

	string projectName = Path.GetFileNameWithoutExtension(csproj);

	if (!noBuild)
	{
		Console.WriteLine($"🔨 Building project '{projectName}'...");
		var psi = new System.Diagnostics.ProcessStartInfo("dotnet", "build")
		{
			UseShellExecute = false,
			RedirectStandardOutput = false
		};
		var process = System.Diagnostics.Process.Start(psi);
		process?.WaitForExit();

		if (process?.ExitCode != 0) return string.Empty;
	}

	var binPath = Path.Combine(currentDir, "bin");
	if (!Directory.Exists(binPath)) return string.Empty;

	var dllFiles = Directory.GetFiles(binPath, $"{projectName}.dll", SearchOption.AllDirectories);
	var latestDll = dllFiles.Select(f => new FileInfo(f)).OrderByDescending(f => f.LastWriteTime).FirstOrDefault();

	return latestDll?.FullName ?? string.Empty;
}