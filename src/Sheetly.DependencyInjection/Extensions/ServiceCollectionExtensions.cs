using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sheetly.Core;
using Sheetly.Core.Abstractions;
using Sheetly.DependencyInjection.Options;
using Sheetly.Google;

namespace Sheetly.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddSheetsContext<TContext>(
		this IServiceCollection services,
		Action<SheetsOptionsBuilder>? configure = null) where TContext : SheetsContext, new()
	{
		services.AddScoped<TContext>(sp =>
		{
			var config = sp.GetRequiredService<IConfiguration>();
			var sheetlySection = config.GetSection("Sheetly");

			// 1. Builder orqali olingan sozlamalar
			var builder = new SheetsOptionsBuilder();
			configure?.Invoke(builder);
			var options = builder.Build();

			// 2. Ustuvorlikni tekshirish (Priority Logic)
			string? spreadsheetId = options.SpreadsheetId ?? sheetlySection["SpreadsheetId"];
			string? credentialsPath = options.CredentialsPath ?? sheetlySection["CredentialsPath"];

			// Migrations folder priority
			if (string.IsNullOrEmpty(options.MigrationsFolder) || options.MigrationsFolder == "Migrations")
			{
				var folderFromConfig = sheetlySection["MigrationsFolder"];
				if (!string.IsNullOrEmpty(folderFromConfig))
					options.MigrationsFolder = folderFromConfig;
			}

			// Providerni aniqlash
			ISheetProvider provider;
			var serviceAccountSection = sheetlySection.GetSection("ServiceAccount");

			if (serviceAccountSection.Exists())
				provider = new GoogleSheetProvider(serviceAccountSection, spreadsheetId!);
			else
				provider = new GoogleSheetProvider(credentialsPath!, spreadsheetId!);

			var migrationService = new GoogleMigrationService(provider, options.GetFullSnapshotPath());

			var context = new TContext();
			context.InitializeAsync(provider, migrationService).GetAwaiter().GetResult();

			return context;
		});

		return services;
	}
}