using Sheetly.Core.Tests.Integration.Models;

namespace Sheetly.Core.Tests.Integration;

/// <summary>
/// G4 — an update that collides with another row's unique value is rejected (C5), while an
/// update that keeps its own value is allowed; physically blank rows are skipped on read (C17).
/// </summary>
public class UniquenessAndBlankTests
{
	[Fact]
	public async Task Update_ToAnotherRowsUniqueValue_Throws()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		ctx.Users.Add(new UserAccount { Username = "u1", Email = "a@x.com" });
		ctx.Users.Add(new UserAccount { Username = "u2", Email = "b@x.com" });
		await ctx.SaveChangesAsync();

		var users = await ctx.Users.ToListAsync();
		users.First(u => u.Username == "u2").Email = "a@x.com";

		await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
	}

	[Fact]
	public async Task Update_KeepingOwnUniqueValue_Succeeds()
	{
		var (ctx, _) = await TestContextFactory.CreateAsync();
		ctx.Users.Add(new UserAccount { Username = "u1", Email = "a@x.com" });
		await ctx.SaveChangesAsync();

		var u = (await ctx.Users.ToListAsync()).Single();
		u.Email = "a@x.com";
		ctx.Users.Update(u);

		await ctx.SaveChangesAsync();
		Assert.Equal("a@x.com", (await ctx.Users.ToListAsync()).Single().Email);
	}

	[Fact]
	public async Task ToListAsync_SkipsPhysicallyBlankRows()
	{
		var (ctx, provider) = await TestContextFactory.CreateAsync();
		ctx.Categories.Add(new Category { Name = "Real" });
		await ctx.SaveChangesAsync();

		await provider.AppendRowsAsync("Categories", new List<IList<object>> { new List<object> { "", "" } });

		var cats = await ctx.Categories.ToListAsync();
		Assert.Single(cats);
		Assert.Equal("Real", cats[0].Name);
	}
}
