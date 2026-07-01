using Sheetly.Google;

namespace Sheetly.Core.Tests;

public class CredentialRotatorTests
{
	// ── Constructor validation ───────────────────────────────────────────────

	[Fact]
	public void Constructor_NullArray_Throws()
	{
		var ex = Assert.Throws<InvalidOperationException>(
			() => new CredentialRotator<string>(null!));
		Assert.Contains("No credentials loaded", ex.Message);
	}

	[Fact]
	public void Constructor_EmptyArray_Throws()
	{
		var ex = Assert.Throws<InvalidOperationException>(
			() => new CredentialRotator<string>([]));
		Assert.Contains("No credentials loaded", ex.Message);
	}

	[Fact]
	public void Constructor_SingleService_Stored()
	{
		var r = new CredentialRotator<string>(["svc-a"]);
		Assert.Single(r.Services);
		Assert.Equal("svc-a", r.Services[0]);
	}

	[Fact]
	public void Constructor_MultipleServices_AllStored()
	{
		var r = new CredentialRotator<string>(["a", "b", "c"]);
		Assert.Equal(3, r.Services.Length);
	}

	// ── Round-robin selection ────────────────────────────────────────────────

	[Fact]
	public async Task Acquire_SingleService_AlwaysReturnsIndex0()
	{
		var r = new CredentialRotator<string>(["only"]);

		for (int i = 0; i < 5; i++)
		{
			var (svc, idx) = await r.AcquireAsync();
			Assert.Equal("only", svc);
			Assert.Equal(0, idx);
		}
	}

	[Fact]
	public async Task Acquire_TwoServices_AlternatesIndexes()
	{
		var r = new CredentialRotator<string>(["a", "b"]);

		var seen = new List<int>();
		for (int i = 0; i < 4; i++)
		{
			var (_, idx) = await r.AcquireAsync();
			seen.Add(idx);
		}

		// Round-robin: 0,1,0,1
		Assert.Equal([0, 1, 0, 1], seen);
	}

	[Fact]
	public async Task Acquire_ThreeServices_CyclesAllIndexes()
	{
		var r = new CredentialRotator<string>(["a", "b", "c"]);

		var seen = new HashSet<int>();
		for (int i = 0; i < 6; i++)
		{
			var (_, idx) = await r.AcquireAsync();
			seen.Add(idx);
		}

		Assert.Equal(3, seen.Count);
		Assert.Contains(0, seen);
		Assert.Contains(1, seen);
		Assert.Contains(2, seen);
	}

	// ── Rate limit marking ───────────────────────────────────────────────────

	[Fact]
	public void MarkRateLimited_Index0_IsRateLimited()
	{
		var r = new CredentialRotator<string>(["a", "b"]);

		Assert.False(r.IsRateLimited(0));
		r.MarkRateLimited(0);
		Assert.True(r.IsRateLimited(0));
	}

	[Fact]
	public void MarkRateLimited_Index0_DoesNotAffectIndex1()
	{
		var r = new CredentialRotator<string>(["a", "b"]);
		r.MarkRateLimited(0);

		Assert.True(r.IsRateLimited(0));
		Assert.False(r.IsRateLimited(1));
	}

	[Fact]
	public void MarkRateLimited_All_CountEqualsServiceCount()
	{
		var r = new CredentialRotator<string>(["a", "b", "c"]);
		r.MarkRateLimited(0);
		r.MarkRateLimited(1);
		r.MarkRateLimited(2);

		Assert.Equal(3, r.RateLimitedCount());
	}

	[Fact]
	public void RateLimitedCount_NoneMarked_ReturnsZero()
	{
		var r = new CredentialRotator<string>(["a", "b", "c"]);
		Assert.Equal(0, r.RateLimitedCount());
	}

	// ── Failover: skips rate-limited services ────────────────────────────────

	[Fact]
	public async Task Acquire_SkipsRateLimitedIndex0_ReturnsIndex1()
	{
		var r = new CredentialRotator<string>(["a", "b"]);
		r.MarkRateLimited(0);

		// Even though round-robin starts at 0, it should skip and return 1
		var (svc, idx) = await r.AcquireAsync();
		Assert.Equal("b", svc);
		Assert.Equal(1, idx);
	}

	[Fact]
	public async Task Acquire_SkipsRateLimitedIndex1_ReturnsIndex0()
	{
		var r = new CredentialRotator<string>(["a", "b"]);
		// Advance rotator so next would normally be index 1
		await r.AcquireAsync(); // index 0
		r.MarkRateLimited(1);

		// Next call would be index 1 (limited) → should return index 0
		var (svc, idx) = await r.AcquireAsync();
		Assert.Equal("a", svc);
		Assert.Equal(0, idx);
	}

	[Fact]
	public async Task Acquire_OnlyOneServiceAvailable_ReturnsThatOne()
	{
		var r = new CredentialRotator<string>(["a", "b", "c"]);
		r.MarkRateLimited(0);
		r.MarkRateLimited(2);

		// Only index 1 is available
		for (int i = 0; i < 4; i++)
		{
			var (svc, idx) = await r.AcquireAsync();
			Assert.Equal("b", svc);
			Assert.Equal(1, idx);
		}
	}

	// ── All rate-limited: waits for soonest ──────────────────────────────────

	[Fact]
	public async Task Acquire_AllRateLimited_WaitsAndReturnsSoonestService()
	{
		var r = new CredentialRotator<string>(["a", "b"]);

		// Mark both as rate-limited but index 1 recovers sooner (1 ms from now)
		r.MarkRateLimited(0);
		// Manually set index 1 to recover almost immediately
		// We do this by using reflection to set a very small future tick
		var field = typeof(CredentialRotator<string>)
			.GetField("_rateLimitedUntilTicks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		var ticks = (long[])field.GetValue(r)!;
		ticks[1] = DateTimeOffset.UtcNow.AddMilliseconds(50).UtcTicks; // 50ms

		var sw = System.Diagnostics.Stopwatch.StartNew();
		var (svc, idx) = await r.AcquireAsync();
		sw.Stop();

		// Should have waited ~50ms and returned index 1 (soonest recovery)
		Assert.Equal(1, idx);
		Assert.True(sw.ElapsedMilliseconds >= 40, $"Expected ~50ms wait, got {sw.ElapsedMilliseconds}ms");
	}

	// ── RateLimitWindow ──────────────────────────────────────────────────────

	[Fact]
	public void RateLimitWindow_Is65Seconds()
	{
		Assert.Equal(TimeSpan.FromSeconds(65), CredentialRotator<string>.RateLimitWindow);
	}

	// ── Thread safety: concurrent Acquire calls ──────────────────────────────

	[Fact]
	public async Task Acquire_Concurrent_NoCrashAndAllIndexesReturned()
	{
		var r = new CredentialRotator<string>(["a", "b", "c"]);
		var results = new System.Collections.Concurrent.ConcurrentBag<int>();

		await Task.WhenAll(Enumerable.Range(0, 30).Select(async _ =>
		{
			var (_, idx) = await r.AcquireAsync();
			results.Add(idx);
		}));

		Assert.Equal(30, results.Count);
		// All 3 indexes should have been used
		Assert.Contains(0, results);
		Assert.Contains(1, results);
		Assert.Contains(2, results);
	}
}
