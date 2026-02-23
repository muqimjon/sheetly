using Sheetly.CLI.Commands;
using System.CommandLine;

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

return await rootCommand.Parse(args).InvokeAsync();