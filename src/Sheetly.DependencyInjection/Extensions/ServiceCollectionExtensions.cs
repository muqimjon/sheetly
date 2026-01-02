using Microsoft.Extensions.DependencyInjection;
using Sheetly.Core;
using Sheetly.Core.Abstractions;
using Sheetly.Core.Configuration;

namespace Sheetly.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddSheetsContext<TContext>(
		this IServiceCollection services,
		Action<SheetsOptions>? configure = null) where TContext : SheetsContext, new()
	{
		services.AddScoped<TContext>(sp =>
		{
			var options = new SheetsOptions();
			configure?.Invoke(options);

			var context = new TContext();
			
			if (options.Provider != null)
			{
				context.InitializeAsync(options.Provider).GetAwaiter().GetResult();
			}
			else
			{
				context.InitializeAsync().GetAwaiter().GetResult();
			}

			return context;
		});

		return services;
	}
}