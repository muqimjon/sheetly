using Sheetly.Core.Migration;

namespace Sheetly.Sample.Migrations;

public partial class AppDbContextModelSnapshot : MigrationSnapshot
{
    public AppDbContextModelSnapshot()
    {
        Entities.Add("Categories", new EntitySchema
        {
            TableName = "Categories",
            ClassName = "Category",
            Namespace = "Sheetly.Sample.Models",
            Columns = new List<ColumnSchema>
            {
                new ColumnSchema
                {
                    Name = "Id",
                    PropertyName = "Id",
                    DataType = "Int64",
                    IsPrimaryKey = true,
                    IsAutoIncrement = true,
                    IsNullable = false,
                    IsRequired = true
                },
                new ColumnSchema
                {
                    Name = "Name",
                    PropertyName = "Name",
                    DataType = "String",
                    IsPrimaryKey = false,
                    IsAutoIncrement = false,
                    IsNullable = false,
                    IsRequired = true,
                    MaxLength = 100,
                    MinLength = 3
                }
            }
        });

        Entities.Add("Products", new EntitySchema
        {
            TableName = "Products",
            ClassName = "Product",
            Namespace = "Sheetly.Sample.Models",
            Columns = new List<ColumnSchema>
            {
                new ColumnSchema
                {
                    Name = "Id",
                    PropertyName = "Id",
                    DataType = "Int32",
                    IsPrimaryKey = true,
                    IsAutoIncrement = true,
                    IsNullable = false,
                    IsRequired = true
                },
                new ColumnSchema
                {
                    Name = "Title",
                    PropertyName = "Title",
                    DataType = "String",
                    IsPrimaryKey = false,
                    IsAutoIncrement = false,
                    IsNullable = false,
                    IsRequired = true,
                    MaxLength = 200
                },
                new ColumnSchema
                {
                    Name = "Price",
                    PropertyName = "Price",
                    DataType = "Decimal",
                    IsPrimaryKey = false,
                    IsAutoIncrement = false,
                    IsNullable = false,
                    IsRequired = true,
                    MinValue = 0,
                    MaxValue = 1000000
                },
                new ColumnSchema
                {
                    Name = "CategoryId",
                    PropertyName = "CategoryId",
                    DataType = "Int32",
                    IsPrimaryKey = false,
                    IsAutoIncrement = false,
                    IsNullable = false,
                    IsRequired = true,
                    IsForeignKey = true,
                    ForeignKeyTable = "Categories",
                    ForeignKeyColumn = "Id",
                    OnDelete = ForeignKeyAction.NoAction
                }
            }
        });

        ModelHash = "v1.0.0-with-constraints";
        LastUpdated = System.DateTime.UtcNow;
    }
}
