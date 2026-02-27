using System;
using Sheetly.Core.Migration;

namespace Sheetly.Test.Contexts.Migrations;

public partial class ExcelAppModelSnapshot : MigrationSnapshot
{
    public ExcelAppModelSnapshot()
    {
        var snapshot = BuildModel();
        this.Entities = snapshot.Entities;
        this.ModelHash = snapshot.ModelHash;
        this.Version = snapshot.Version;
        this.LastUpdated = snapshot.LastUpdated;
    }

    public static MigrationSnapshot BuildModel()
    {
        var snapshot = new MigrationSnapshot
        {
            ModelHash = "erfMXU/RWc/dJ2XYy4Tck5Nw4rMDkzpVEiJA1z5xQro=",
            Version = "1.0.0",
            LastUpdated = DateTime.Parse("2026-02-27T23:15:49.6662861Z")
        };

        // Category
        snapshot.Entities["Categories"] = new EntitySchema
        {
            TableName = "Categories",
            ClassName = "Category",
            Namespace = "Sheetly.Test.Models",
            Columns = new List<ColumnSchema>
            {
                new ColumnSchema
                {
                    Name = "Id",
                    PropertyName = "Id",
                    DataType = "Int32",
                    IsPrimaryKey = true,
                    IsAutoIncrement = true,
                    IsForeignKey = false,
                    ForeignKeyColumn = "Id",
                    IsNullable = false,
                    IsRequired = false
                },
                new ColumnSchema
                {
                    Name = "Name",
                    PropertyName = "Name",
                    DataType = "String",
                    IsPrimaryKey = false,
                    IsAutoIncrement = false,
                    IsForeignKey = false,
                    ForeignKeyColumn = "Id",
                    IsNullable = false,
                    IsRequired = true,
                    MaxLength = 100
                }
            },
            Relationships = new List<RelationshipSchema>()
        };

        // Product
        snapshot.Entities["Products"] = new EntitySchema
        {
            TableName = "Products",
            ClassName = "Product",
            Namespace = "Sheetly.Test.Models",
            Columns = new List<ColumnSchema>
            {
                new ColumnSchema
                {
                    Name = "Id",
                    PropertyName = "Id",
                    DataType = "Int32",
                    IsPrimaryKey = true,
                    IsAutoIncrement = true,
                    IsForeignKey = false,
                    ForeignKeyColumn = "Id",
                    IsNullable = false,
                    IsRequired = false
                },
                new ColumnSchema
                {
                    Name = "Name",
                    PropertyName = "Name",
                    DataType = "String",
                    IsPrimaryKey = false,
                    IsAutoIncrement = false,
                    IsForeignKey = false,
                    ForeignKeyColumn = "Id",
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
                    IsForeignKey = false,
                    ForeignKeyColumn = "Id",
                    IsNullable = false,
                    IsRequired = true
                },
                new ColumnSchema
                {
                    Name = "Description",
                    PropertyName = "Description",
                    DataType = "String",
                    IsPrimaryKey = false,
                    IsAutoIncrement = false,
                    IsForeignKey = false,
                    ForeignKeyColumn = "Id",
                    IsNullable = true,
                    IsRequired = false
                },
                new ColumnSchema
                {
                    Name = "CategoryId",
                    PropertyName = "CategoryId",
                    DataType = "Int32",
                    IsPrimaryKey = false,
                    IsAutoIncrement = false,
                    IsForeignKey = true,
                    ForeignKeyTable = "Categories",
                    ForeignKeyColumn = "Id",
                    IsNullable = false,
                    IsRequired = false
                }
            },
            Relationships = new List<RelationshipSchema>()
        };

        return snapshot;
    }
}
