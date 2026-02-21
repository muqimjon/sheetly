using Sheetly.Core.Migrations;

namespace Sheetly.Sample.Migrations;

[Migration("20260221191400_InitialCreate")]
public partial class InitialCreate : Migration
{
	public override void Up(MigrationBuilder builder)
	{
		// ClassName: Category
		builder.CreateTable("Categories", table => table
			.Column<long>("Id", c => c.IsPrimaryKey().IsUnique())
			.Column<string>("Name")
		);

		// ClassName: Product
		builder.CreateTable("Products", table => table
			.Column<int>("Id", c => c.IsPrimaryKey().IsUnique())
			.Column<string>("Title")
			.Column<decimal>("Price", c => c.IsRequired())
			.Column<int>("CategoryId", c => c.IsRequired().IsForeignKey("Categories"))
		);

	}

	public override void Down(MigrationBuilder builder)
	{
		builder.DropTable("Products");
		builder.DropTable("Categories");
	}
}
