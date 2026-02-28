using System;
using Sheetly.Core.Migration;

namespace Sheetly.Sample.Migrations;

public partial class AppDbModelSnapshot : MigrationSnapshot
{
    public AppDbModelSnapshot()
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
            ModelHash = "B9emMa1A++cOQMHt5sY3NkJRTAb1yP/Ei7sKWFlwVDw=",
            Version = "1.0.0",
            LastUpdated = DateTime.Parse("2026-02-28T07:44:24.5479128Z")
        };

        // Category
        snapshot.Entities["Categories"] = new EntitySchema
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
                    MaxLength = 100,
                    MinLength = 3
                }
            },
            Relationships = new List<RelationshipSchema>()
        };

        // Product
        snapshot.Entities["Products"] = new EntitySchema
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
                    IsForeignKey = false,
                    ForeignKeyColumn = "Id",
                    IsNullable = false,
                    IsRequired = false
                },
                new ColumnSchema
                {
                    Name = "Title",
                    PropertyName = "Title",
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
                    IsRequired = true,
                    MinValue = 0m,
                    MaxValue = 1000000m
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
                    IsRequired = false,
                    MaxLength = 500
                },
                new ColumnSchema
                {
                    Name = "Stock",
                    PropertyName = "Stock",
                    DataType = "Int32",
                    IsPrimaryKey = false,
                    IsAutoIncrement = false,
                    IsForeignKey = false,
                    ForeignKeyColumn = "Id",
                    IsNullable = false,
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
