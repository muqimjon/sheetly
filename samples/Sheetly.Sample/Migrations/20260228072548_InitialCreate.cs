using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Sample.Migrations;

[Migration("20260228072548_InitialCreate")]
public partial class InitialCreate : Migration
{
    public override void Up(MigrationBuilder builder)
    {
        builder.CreateTable("Categories", table => table
            .Column<long>("Id", c => c.IsPrimaryKey().IsUnique())
            .Column<string>("Name", c => c.IsRequired().HasMaxLength(100))
        );

        builder.CreateTable("Products", table => table
            .Column<int>("Id", c => c.IsPrimaryKey().IsUnique())
            .Column<string>("Title", c => c.IsRequired().HasMaxLength(200))
            .Column<decimal>("Price", c => c.IsRequired())
            .Column<string>("Description", c => c.HasMaxLength(500))
            .Column<int>("Stock", c => c.IsRequired())
            .Column<int>("CategoryId", c => c.IsRequired().IsForeignKey("Categories"))
        );

    }

    public override void Down(MigrationBuilder builder)
    {
        builder.DropTable("Products");
        builder.DropTable("Categories");
    }
}
