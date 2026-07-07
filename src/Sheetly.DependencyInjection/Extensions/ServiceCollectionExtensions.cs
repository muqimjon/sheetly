using Microsoft.Extensions.DependencyInjection;
using Sheetly.Core;
using Sheetly.Core.Configuration;
using Sheetly.Core.Infrastructure;

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
		services.AddScoped<TContext>(_ =>
		{
			var (context, options, usesOptionsCtor) = Build(configure);
			InitializeContext(context, options, usesOptionsCtor).GetAwaiter().GetResult();
			return context;
		});

		return services;
	}

	/// <summary>
	/// Registers an <see cref="ISheetsContextFactory{TContext}"/> singleton, the EF Core
	/// <c>IDbContextFactory</c> analog. Its <c>CreateContextAsync</c> initializes contexts
	/// with a real await instead of the sync-over-async blocking that scoped resolution forces.
	/// </summary>
	public static IServiceCollection AddSheetsContextFactory<TContext>(
		this IServiceCollection services,
		Action<SheetsContextOptions<TContext>>? configure = null) where TContext : SheetsContext
	{
		services.AddSingleton<ISheetsContextFactory<TContext>>(_ => new SheetsContextFactory<TContext>(configure));
		return services;
	}

	private static (TContext context, SheetsContextOptions<TContext> options, bool usesOptionsCtor) Build<TContext>(
		Action<SheetsContextOptions<TContext>>? configure) where TContext : SheetsContext
	{
		var options = new SheetsContextOptions<TContext>();
		configure?.Invoke(options);

		var ctorWithOptions = typeof(TContext).GetConstructor([typeof(SheetsContextOptions<TContext>)]);
		if (ctorWithOptions is not null)
			return ((TContext)ctorWithOptions.Invoke([options]), options, true);

		return ((TContext)Activator.CreateInstance(typeof(TContext), nonPublic: true)!, options, false);
	}

	private static Task InitializeContext<TContext>(TContext context, SheetsContextOptions<TContext> options, bool usesOptionsCtor)
		where TContext : SheetsContext
	{
		if (usesOptionsCtor) return context.InitializeAsync();
		return options.Provider is not null ? context.InitializeAsync(options.Provider) : context.InitializeAsync();
	}

	private sealed class SheetsContextFactory<TContext>(Action<SheetsContextOptions<TContext>>? configure)
		: ISheetsContextFactory<TContext> where TContext : SheetsContext
	{
		public async Task<TContext> CreateContextAsync(CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			var (context, options, usesOptionsCtor) = Build(configure);
			await InitializeContext(context, options, usesOptionsCtor);
			return context;
		}
	}
}
