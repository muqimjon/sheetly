using Sheetly.CLI.Helpers;
using Sheetly.Core.Migration;
using System.CommandLine;
using System.Reflection;

namespace Sheetly.CLI.Commands;

public class ScriptCommand : Command
{
	private readonly Option<string?> _projectOption = new("--project", ["-p"]) { Description = "Manual path to DLL" };

	public ScriptCommand() : base("script", "Generate a schema script from the latest snapshot")
	{
		this.Add(_projectOption);
		this.SetAction(async (parseResult, ct) => await ExecuteAsync(parseResult.GetValue(_projectOption)));
	}

	private async Task ExecuteAsync(string? projectPath)
	{
		string dllPath = CliHelper.FindProjectDll(true, projectPath);
		if (string.IsNullOrEmpty(dllPath)) return;

		try
		{
			var (assembly, _) = CliHelper.LoadAssemblyIsolated(dllPath);

			var snapshotType = assembly.GetTypes()
				.FirstOrDefault(t => t.Name.EndsWith("ModelSnapshot") &&
								CliHelper.IsSubclassOf(t, "Sheetly.Core.Migrations.MigrationSnapshot"));

			if (snapshotType == null)
			{
				Console.WriteLine("⚠️ Snapshot not found. Run 'migrations add' first.");
				return;
			}

			var isolatedSnap = Activator.CreateInstance(snapshotType)!;
			var snapshot = CliHelper.BridgeFromJson<MigrationSnapshot>(isolatedSnap)!;

			Console.WriteLine($"--- Sheetly Schema Script (Generated at {DateTime.Now}) ---");
			foreach (var entity in snapshot.Entities.Values)
			{
				Console.WriteLine($"Sheet: {entity.TableName}");
				foreach (var col in entity.Columns)
				{
					string pk = col.IsPrimaryKey ? " [PK]" : "";
					string fk = col.IsForeignKey ? $" [FK → {col.ForeignKeyTable}]" : "";
					string req = col.IsRequired ? " [Required]" : "";
					Console.WriteLine($"  - {col.PropertyName} ({col.DataType}){pk}{fk}{req}");
				}
				Console.WriteLine();
			}
		}
		catch (Exception ex) { Console.WriteLine($"❌ Error: {ex.Message}"); }
	}
}