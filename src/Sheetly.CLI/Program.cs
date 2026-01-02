using System.CommandLine;
using Sheetly.CLI.Commands;

// Root Command
RootCommand rootCommand = new("Sheetly CLI - Google Sheets ORM Tool");

// Buyruqlar ierarxiyasini yaratish
var migrationsCommand = new Command("migrations", "Commands to manage migrations");
var databaseCommand = new Command("database", "Commands to manage the database");

// Migrations subcommands
migrationsCommand.Subcommands.Add(new AddCommand());
migrationsCommand.Subcommands.Add(new RemoveCommand());
migrationsCommand.Subcommands.Add(new ScriptCommand());

// Database subcommands
databaseCommand.Subcommands.Add(new UpdateCommand());
databaseCommand.Subcommands.Add(new DropCommand());

// Root-ga qo'shish
rootCommand.Subcommands.Add(migrationsCommand);
rootCommand.Subcommands.Add(databaseCommand);
rootCommand.Subcommands.Add(new ScaffoldCommand());

return await rootCommand.Parse(args).InvokeAsync();