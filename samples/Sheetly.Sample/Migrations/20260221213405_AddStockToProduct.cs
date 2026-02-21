using Sheetly.Core.Migrations;
using Sheetly.Core.Migrations.Operations;

namespace Sheetly.Sample.Migrations;

[Migration("20260221213405_AddStockToProduct")]
public partial class AddStockToProduct : Migration
{
    public override void Up(MigrationBuilder builder)
    {
        builder.AddColumn<int>("Products", "Stock", c => c.IsRequired());

    }

    public override void Down(MigrationBuilder builder)
    {
        builder.DropColumn("Products", "Stock");
    }
}
