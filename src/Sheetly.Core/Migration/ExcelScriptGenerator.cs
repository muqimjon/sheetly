using ClosedXML.Excel;
using Sheetly.Core.Migration;

namespace Sheetly.Core.Migration;

public class ExcelScriptGenerator
{
	public byte[] GenerateXlsx(MigrationSnapshot snapshot)
	{
		using var workbook = new XLWorkbook();

		foreach (var entity in snapshot.Entities.Values)
		{
			var worksheet = workbook.Worksheets.Add(entity.TableName);

			for (int i = 0; i < entity.Columns.Count; i++)
			{
				var column = entity.Columns[i];
				var cell = worksheet.Cell(1, i + 1);

				cell.Value = column.Name;
				cell.Style.Font.Bold = true;
				cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F3F3");
				cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

				if (column.IsPrimaryKey)
				{
					cell.Style.Font.FontColor = XLColor.Red;
				}

				ApplyTypeValidation(worksheet, i + 1, column);
			}

			worksheet.SheetView.FreezeRows(1);
			worksheet.Columns().AdjustToContents();
		}

		CreateSchemaSheet(workbook, snapshot);

		using var stream = new MemoryStream();
		workbook.SaveAs(stream);
		return stream.ToArray();
	}

	private void ApplyTypeValidation(IXLWorksheet worksheet, int colIndex, ColumnSchema column)
	{
		var range = worksheet.Column(colIndex).AsRange();
		var validation = range.CreateDataValidation();

		if (column.DataType == "Boolean")
		{
			validation.List("TRUE, FALSE", true);
		}
		else if (column.DataType == "Int32" || column.DataType == "Int64")
		{
			validation.WholeNumber.Between(int.MinValue, int.MaxValue);
		}
		else if (column.DataType == "Decimal" || column.DataType == "Double")
		{
			validation.Decimal.Between(double.MinValue, double.MaxValue);
		}
	}

	private void CreateSchemaSheet(XLWorkbook workbook, MigrationSnapshot snapshot)
	{
		var schemaSheet = workbook.Worksheets.Add("__SheetlySchema__");
		schemaSheet.Hide();

		string[] headers = ["TableName", "PropertyName", "DataType", "Constraints", "Relation", "Default", "LastId"];
		for (int i = 0; i < headers.Length; i++)
		{
			schemaSheet.Cell(1, i + 1).Value = headers[i];
		}

		int row = 2;
		foreach (var entity in snapshot.Entities.Values)
		{
			foreach (var col in entity.Columns)
			{
				schemaSheet.Cell(row, 1).Value = entity.TableName;
				schemaSheet.Cell(row, 2).Value = col.PropertyName;
				schemaSheet.Cell(row, 3).Value = col.DataType;
				schemaSheet.Cell(row, 4).Value = $"{(col.IsPrimaryKey ? "PK" : "")},{(col.IsNullable ? "" : "Required")}";
				schemaSheet.Cell(row, 5).Value = col.RelatedTable ?? "";
				schemaSheet.Cell(row, 6).Value = col.DefaultValue?.ToString() ?? "";
				schemaSheet.Cell(row, 7).Value = col.IsPrimaryKey ? "1" : "";
				row++;
			}
		}
	}
}