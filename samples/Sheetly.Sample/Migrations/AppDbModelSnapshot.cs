using Sheetly.Core.Migration;

namespace Sheetly.Sample.Migrations;

public partial class AppDbModelSnapshot
{
	public static MigrationSnapshot BuildModel()
	{
		var snapshot = new MigrationSnapshot
		{
			ModelHash = "riYgwR2SXPYT5o74QU28GUXJ7F28Sn0K9lvTsmuV2v0=",
			Version = "1.0.0",
			LastUpdated = DateTime.Parse("2026-02-21T19:14:00.7304096Z")
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
					IsNullable = true,
					IsRequired = false
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
					IsNullable = true,
					IsRequired = false
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
