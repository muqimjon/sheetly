using Microsoft.Extensions.Configuration;
using Sheetly.Core.Migration;
using System.Text;

namespace Sheetly.CLI.Helpers;

public static class CliHelper
{
	public static bool IsSubclassOfSheetsContext(Type? type)
	{
		while (type != null && type != typeof(object))
		{
			if (type.FullName == "Sheetly.Core.SheetsContext") return true;
			type = type.BaseType;
		}
		return false;
	}

	public static string FindProjectDll(bool noBuild, string? manualPath)
	{
		if (!string.IsNullOrEmpty(manualPath)) return manualPath;
		var csproj = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj").FirstOrDefault();
		if (csproj == null) return string.Empty;

		if (!noBuild)
		{
			Console.WriteLine($"⏳ Loyiha qurilmoqda: '{Path.GetFileName(csproj)}'...");
			var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("dotnet", "build -c Debug") { UseShellExecute = false });
			process?.WaitForExit();
		}

		var targetDir = Path.Combine(Directory.GetCurrentDirectory(), "bin", "Debug");
		var dllFiles = Directory.GetFiles(targetDir, $"{Path.GetFileNameWithoutExtension(csproj)}.dll", SearchOption.AllDirectories);
		return dllFiles.OrderByDescending(File.GetLastWriteTime).FirstOrDefault() ?? string.Empty;
	}

	public static string FindProjectRootFromDll(string dllPath)
	{
		var dir = new DirectoryInfo(Path.GetDirectoryName(dllPath)!);
		while (dir != null && !dir.GetFiles("*.csproj").Any()) dir = dir.Parent;
		return dir?.FullName ?? Path.GetDirectoryName(dllPath)!;
	}

	public static string? GetConnectionString(string projectDir)
	{
		var config = new ConfigurationBuilder()
			.SetBasePath(projectDir)
			.AddJsonFile("appsettings.json", true)
			.AddJsonFile("appsettings.Development.json", true)
			.Build();

		return config.GetConnectionString("DefaultConnection")
			   ?? config.GetSection("Sheetly")["ConnectionString"];
	}

	public static string GenerateClassCode(EntitySchema entity)
	{
		var sb = new StringBuilder();
		sb.AppendLine("using System.ComponentModel.DataAnnotations;");
		sb.AppendLine("using System.ComponentModel.DataAnnotations.Schema;");
		sb.AppendLine();
		sb.AppendLine($"namespace {entity.Namespace}.Scaffolded;");
		sb.AppendLine();
		sb.AppendLine($"[Table(\"{entity.TableName}\")]");
		sb.AppendLine($"public class {entity.ClassName}");
		sb.AppendLine("{");
		foreach (var col in entity.Columns)
		{
			if (col.IsPrimaryKey) sb.AppendLine("    [Key]");
			if (col.IsForeignKey) sb.AppendLine($"    [ForeignKey(\"{col.RelatedTable}\")]");
			var type = col.DataType;
			if (col.IsNullable && type != "String" && !type.EndsWith("?")) type += "?";
			sb.AppendLine($"    public {type} {col.PropertyName} {{ get; set; }}");
			sb.AppendLine();
		}
		sb.AppendLine("}");
		return sb.ToString();
	}
}