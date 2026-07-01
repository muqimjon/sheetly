namespace Sheetly.Google;

/// <summary>
/// Thread-safe round-robin credential rotator with per-credential rate-limit tracking.
/// When a credential hits the Google API rate limit (429), it is marked as unavailable
/// for <see cref="RateLimitWindow"/>. The next available credential is used immediately.
/// When all credentials are exhausted, waits for the soonest recovery.
/// </summary>
internal sealed class CredentialRotator<T>
{
	internal readonly T[] Services;
	private int _index = -1;
	private readonly long[] _rateLimitedUntilTicks;

	internal static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(65);

	internal CredentialRotator(T[] services)
	{
		if (services is null || services.Length == 0)
			throw new InvalidOperationException(
				"No credentials loaded. credentials.json must contain at least one service account object.");
		Services = services;
		_rateLimitedUntilTicks = new long[services.Length];
	}

	/// <summary>
	/// Returns the next available (non-rate-limited) service and its index.
	/// If all services are currently rate-limited, waits until the soonest one recovers.
	/// </summary>
	internal async ValueTask<(T service, int index)> AcquireAsync()
	{
		var now = DateTimeOffset.UtcNow.UtcTicks;

		for (int i = 0; i < Services.Length; i++)
		{
			int idx = (int)((uint)Interlocked.Increment(ref _index) % (uint)Services.Length);
			if (Interlocked.Read(ref _rateLimitedUntilTicks[idx]) <= now)
				return (Services[idx], idx);
		}

		// All rate-limited — wait for the soonest recovery
		int minIdx = 0;
		long minTicks = Interlocked.Read(ref _rateLimitedUntilTicks[0]);
		for (int i = 1; i < Services.Length; i++)
		{
			long t = Interlocked.Read(ref _rateLimitedUntilTicks[i]);
			if (t < minTicks) { minTicks = t; minIdx = i; }
		}

		var waitMs = (int)Math.Max(0, TimeSpan.FromTicks(minTicks - DateTimeOffset.UtcNow.UtcTicks).TotalMilliseconds);
		if (waitMs > 0)
			await Task.Delay(waitMs);

		return (Services[minIdx], minIdx);
	}

	/// <summary>
	/// Marks the credential at <paramref name="index"/> as rate-limited for <see cref="RateLimitWindow"/>.
	/// </summary>
	internal void MarkRateLimited(int index)
		=> Interlocked.Exchange(ref _rateLimitedUntilTicks[index],
			DateTimeOffset.UtcNow.Add(RateLimitWindow).UtcTicks);

	/// <summary>Returns true if the credential at <paramref name="index"/> is currently rate-limited.</summary>
	internal bool IsRateLimited(int index)
		=> Interlocked.Read(ref _rateLimitedUntilTicks[index]) > DateTimeOffset.UtcNow.UtcTicks;

	/// <summary>Returns how many credentials are currently rate-limited.</summary>
	internal int RateLimitedCount()
	{
		var now = DateTimeOffset.UtcNow.UtcTicks;
		int count = 0;
		for (int i = 0; i < Services.Length; i++)
			if (Interlocked.Read(ref _rateLimitedUntilTicks[i]) > now) count++;
		return count;
	}
}
