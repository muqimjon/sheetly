using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// Phase 2: primary-key and unique-column uniqueness validated against existing
/// remote data (not just the pending batch).
/// </summary>
public class Phase2ValidationTests
{
	// 2.1 — user-assigned PK colliding with an already-persisted row
	[Fact]
	public async Task DuplicateUserAssignedPk_AgainstExisting_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Users.Add(new UserAccount { Username = "alice", Email = "alice@x.com" });
		await ctx.SaveChangesAsync();

		ctx.Users.Add(new UserAccount { Username = "alice", Email = "alice2@x.com" });

		await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
	}

	// 2.1 — unique column colliding with an already-persisted row
	[Fact]
	public async Task DuplicateUnique_AgainstExisting_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Users.Add(new UserAccount { Username = "bob", Email = "shared@x.com" });
		await ctx.SaveChangesAsync();

		ctx.Users.Add(new UserAccount { Username = "carol", Email = "shared@x.com" });

		await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
	}

	// 2.1 — duplicates within a single pending insert batch are rejected
	[Fact]
	public async Task DuplicateUnique_InBatch_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Users.Add(new UserAccount { Username = "dan", Email = "dup@x.com" });
		ctx.Users.Add(new UserAccount { Username = "erin", Email = "dup@x.com" });

		await Assert.ThrowsAnyAsync<Exception>(() => ctx.SaveChangesAsync());
	}

	// 2.1 — distinct values save cleanly
	[Fact]
	public async Task DistinctUsers_Succeed()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Users.Add(new UserAccount { Username = "frank", Email = "frank@x.com" });
		ctx.Users.Add(new UserAccount { Username = "grace", Email = "grace@x.com" });

		var changes = await ctx.SaveChangesAsync();
		Assert.Equal(2, changes);

		var users = await ctx.Users.ToListAsync();
		Assert.Equal(2, users.Count);
	}

	// 2.1 — updating an existing row must not trip the insert-uniqueness check
	[Fact]
	public async Task UpdatingExistingRow_DoesNotTripUniqueness()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();

		ctx.Users.Add(new UserAccount { Username = "henry", Email = "henry@x.com" });
		await ctx.SaveChangesAsync();

		var henry = (await ctx.Users.ToListAsync()).Single();
		henry.Email = "henry.new@x.com";

		var changes = await ctx.SaveChangesAsync();
		Assert.Equal(1, changes);
	}
}
