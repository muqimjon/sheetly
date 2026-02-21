using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Sample.Migrations;

[Migration("20260221195500_InitialCreate")]
public partial class InitialCreate : Sheetly.Core.Migrations.Migration
{
    public override void Up(Sheetly.Core.Migrations.MigrationBuilder builder)
    {
        // Categories table
        builder.CreateTable("Categories", table => table
            .Column<long>("Id", c => c.IsPrimaryKey())
            .Column<string>("Name", c => c.IsRequired().HasMaxLength(100))
        );

        // Products table
        builder.CreateTable("Products", table => table
            .Column<int>("Id", c => c.IsPrimaryKey())
            .Column<string>("Title", c => c.IsRequired().HasMaxLength(200))
            .Column<decimal>("Price", c => c.IsRequired())
            .Column<int>("CategoryId", c => c.IsRequired().IsForeignKey("Categories"))
        );
    }

    public override void Down(Sheetly.Core.Migrations.MigrationBuilder builder)
    {
        builder.DropTable("Products");
        builder.DropTable("Categories");
    }
}
