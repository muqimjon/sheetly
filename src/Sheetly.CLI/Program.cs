using Sheetly.CLI.Commands;
using System.CommandLine;
using System.Reflection;

var version = Assembly
	.GetExecutingAssembly()
	.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
	.InformationalVersion;

// Root Command
RootCommand rootCommand = new("Sheetly CLI - Google Sheets ORM Tool");

var migrationsCommand = new Command("migrations", "Manage migrations");
var databaseCommand = new Command("database", "Manage the database");

// Migrations subcommands
migrationsCommand.Subcommands.Add(new AddCommand());
migrationsCommand.Subcommands.Add(new RemoveCommand());
migrationsCommand.Subcommands.Add(new ListCommand());
migrationsCommand.Subcommands.Add(new ScriptCommand());

// Database subcommands
databaseCommand.Subcommands.Add(new UpdateCommand());
databaseCommand.Subcommands.Add(new DropCommand());

// Add to root
rootCommand.Subcommands.Add(migrationsCommand);
rootCommand.Subcommands.Add(databaseCommand);
rootCommand.Subcommands.Add(new ScaffoldCommand());

rootCommand.SetAction(_ =>
{
	Console.WriteLine();
	Console.ResetColor();
	Console.WriteLine(@"                                          /\");
	Console.WriteLine(@"                                         /  \");
	Console.WriteLine(@"                                        |    |");
	Console.WriteLine(@"                                      --|----|--");
	Console.ForegroundColor = ConsoleColor.DarkGreen;
	Console.Write(@"  ____  _               _   _          ");
	Console.ResetColor();
	Console.WriteLine(@"/| [] |\");
	Console.ForegroundColor = ConsoleColor.DarkGreen;
	Console.Write(@" / ___|| |__   ___  ___| |_| |_   _   ");
	Console.ResetColor();
	Console.WriteLine(@"/ |----| \");
	Console.ForegroundColor = ConsoleColor.DarkGreen;
	Console.Write(@" \___ \| '_ \ / _ \/ _ \ __| | | | | ");
	Console.ResetColor();
	Console.WriteLine(@"|  |(--)|  |");
	Console.ForegroundColor = ConsoleColor.DarkGreen;
	Console.Write(@"  ___) | | | |  __/  __/ |_| | |_| | ");
	Console.ResetColor();
	Console.WriteLine(@"| /|____|\ |");
	Console.ForegroundColor = ConsoleColor.DarkGreen;
	Console.Write(@" |____/|_| |_|\___|\___|\__|_|\__, | ");
	Console.ResetColor();
	Console.WriteLine(@"|/ |____| \|");
	Console.ForegroundColor = ConsoleColor.DarkGreen;
	Console.Write(@"                              |___/  ");
	Console.ResetColor();
	Console.WriteLine(@"/  /____\  \");
	Console.WriteLine(@"                                    /_ /_/_/_ \ _\");
	Console.WriteLine(@"                                     / / / / / /");
	Console.WriteLine(@"                                    / / / / / /");
	Console.WriteLine();
	Console.ForegroundColor = ConsoleColor.DarkGreen;
	Console.Write("Sheetly ");
	Console.ResetColor();
	Console.WriteLine("Google Sheets ORM .NET Command-line Tools");

	Console.ForegroundColor = ConsoleColor.DarkGray;
	Console.ResetColor();

	Console.WriteLine();
	Console.WriteLine("Usage: dotnet sheetly [options] [command]");
	Console.WriteLine();

	Console.WriteLine("Options:");
	Console.WriteLine("  --version         Show version information");
	Console.WriteLine("  -h|--help         Show help information");
	Console.WriteLine();

	Console.WriteLine("Commands:");
	Console.WriteLine("  database          Commands to manage the database.");
	Console.WriteLine("  migrations        Commands to manage migrations.");
	Console.WriteLine("  scaffold          Commands to scaffold entity classes.");
	Console.WriteLine();

	Console.WriteLine("Use \"dotnet sheetly [command] --help\" for more information about a command.");

	Console.WriteLine();

	return 0;
});

return await rootCommand.Parse(args).InvokeAsync();