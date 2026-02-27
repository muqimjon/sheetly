using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Test.Contexts.Migrations;

[Migration("20260227224251_InitialMigrate")]
public partial class InitialMigrate : Migration
{
    public override void Up(MigrationBuilder builder)
    {
        // ClassName: Category
        builder.CreateTable("Categories", table => table
            .Column<int>("Id", c => c.IsPrimaryKey().IsUnique())
            .Column<string>("Name", c => c.IsRequired().HasMaxLength(100))
        );

        // ClassName: Product
        builder.CreateTable("Products", table => table
            .Column<int>("Id", c => c.IsPrimaryKey().IsUnique())
            .Column<string>("Name", c => c.IsRequired().HasMaxLength(200))
            .Column<decimal>("Price", c => c.IsRequired())
            .Column<string>("Description")
            .Column<int>("CategoryId", c => c.IsRequired().IsForeignKey("Categories"))
        );

    }

    public override void Down(MigrationBuilder builder)
    {
        builder.DropTable("Products");
        builder.DropTable("Categories");
    }
}
