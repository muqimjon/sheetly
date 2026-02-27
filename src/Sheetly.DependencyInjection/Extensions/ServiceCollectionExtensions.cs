using Microsoft.Extensions.DependencyInjection;
using Sheetly.Core;
using Sheetly.Core.Configuration;

namespace Sheetly.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers a <typeparamref name="TContext"/> as a scoped service.
	/// Configures via <paramref name="configure"/> action on <see cref="SheetsContextOptions{TContext}"/>.
	/// If <typeparamref name="TContext"/> has a constructor accepting
	/// <see cref="SheetsContextOptions{TContext}"/>, it is used (EF Core style).
	/// Otherwise, falls back to the parameterless constructor with <c>InitializeAsync</c>.
	/// </summary>
	public static IServiceCollection AddSheetsContext<TContext>(
		this IServiceCollection services,
		Action<SheetsContextOptions<TContext>>? configure = null) where TContext : SheetsContext
	{
		services.AddScoped<TContext>(sp =>
		{
			var options = new SheetsContextOptions<TContext>();
			configure?.Invoke(options);

			// Try constructor injection (EF Core-style) first
			var ctorWithOptions = typeof(TContext)
				.GetConstructor([typeof(SheetsContextOptions<TContext>)]);

			TContext context;
			if (ctorWithOptions != null)
			{
				context = (TContext)ctorWithOptions.Invoke([options]);
				context.InitializeAsync().GetAwaiter().GetResult();
			}
			else
			{
				// Fallback: parameterless constructor + provider passed to InitializeAsync
				context = (TContext)Activator.CreateInstance(typeof(TContext), nonPublic: true)!;
				if (options.Provider != null)
					context.InitializeAsync(options.Provider).GetAwaiter().GetResult();
				else
					context.InitializeAsync().GetAwaiter().GetResult();
			}

			return context;
		});

		return services;
	}
}
