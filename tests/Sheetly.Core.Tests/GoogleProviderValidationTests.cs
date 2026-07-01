using Sheetly.Google;

namespace Sheetly.Core.Tests;

public class GoogleProviderValidationTests
{
	// ── ExtractCredentialJsonObjects ─────────────────────────────────────────

	[Fact]
	public void Extract_SingleObject_ReturnsOneItem()
	{
		var json = """{"type":"service_account","project_id":"test"}""";
		var result = GoogleSheetProvider.ExtractCredentialJsonObjects(json);

		Assert.Single(result);
		Assert.Equal(json, result[0]);
	}

	[Fact]
	public void Extract_ArrayWithOneObject_ReturnsOneItem()
	{
		var json = """[{"type":"service_account","project_id":"a"}]""";
		var result = GoogleSheetProvider.ExtractCredentialJsonObjects(json);

		Assert.Single(result);
	}

	[Fact]
	public void Extract_ArrayWithTwoObjects_ReturnsTwoItems()
	{
		var json = """[{"type":"service_account","project_id":"a"},{"type":"service_account","project_id":"b"}]""";
		var result = GoogleSheetProvider.ExtractCredentialJsonObjects(json);

		Assert.Equal(2, result.Length);
	}

	[Fact]
	public void Extract_ArrayWithThreeObjects_ReturnsThreeItems()
	{
		var json = """[{"a":1},{"a":2},{"a":3}]""";
		var result = GoogleSheetProvider.ExtractCredentialJsonObjects(json);

		Assert.Equal(3, result.Length);
	}

	[Fact]
	public void Extract_EmptyArray_Throws()
	{
		var ex = Assert.Throws<InvalidOperationException>(
			() => GoogleSheetProvider.ExtractCredentialJsonObjects("[]"));

		Assert.Contains("empty array", ex.Message);
		Assert.Contains("[{...}]", ex.Message);
	}

	[Fact]
	public void Extract_EmptyString_Throws()
	{
		var ex = Assert.Throws<InvalidOperationException>(
			() => GoogleSheetProvider.ExtractCredentialJsonObjects(""));

		Assert.Contains("empty", ex.Message);
	}

	[Fact]
	public void Extract_WhitespaceOnly_Throws()
	{
		var ex = Assert.Throws<InvalidOperationException>(
			() => GoogleSheetProvider.ExtractCredentialJsonObjects("   \t\n"));

		Assert.Contains("empty", ex.Message);
	}

	[Fact]
	public void Extract_NullString_Throws()
	{
		Assert.Throws<InvalidOperationException>(
			() => GoogleSheetProvider.ExtractCredentialJsonObjects(null!));
	}

	[Fact]
	public void Extract_ArrayJsonPreservesRawText()
	{
		var obj1 = """{"type":"service_account","project_id":"proj1","client_email":"a@a.iam"}""";
		var obj2 = """{"type":"service_account","project_id":"proj2","client_email":"b@b.iam"}""";
		var json = $"[{obj1},{obj2}]";

		var result = GoogleSheetProvider.ExtractCredentialJsonObjects(json);

		Assert.Equal(2, result.Length);
		Assert.Contains("proj1", result[0]);
		Assert.Contains("proj2", result[1]);
	}

	[Fact]
	public void Extract_LeadingWhitespace_HandledCorrectly()
	{
		var json = """  {"type":"service_account"}""";
		var result = GoogleSheetProvider.ExtractCredentialJsonObjects(json);

		Assert.Single(result);
	}

	[Fact]
	public void Extract_ArrayWithLeadingWhitespace_HandledCorrectly()
	{
		var json = """  [{"type":"service_account"}]""";
		var result = GoogleSheetProvider.ExtractCredentialJsonObjects(json);

		Assert.Single(result);
	}

	[Fact]
	public void Extract_UnexpectedJsonShape_Throws()
	{
		var ex = Assert.Throws<InvalidOperationException>(
			() => GoogleSheetProvider.ExtractCredentialJsonObjects("\"just a string\""));

		Assert.Contains("unexpected format", ex.Message);
	}

	[Fact]
	public void Extract_NumberJson_Throws()
	{
		var ex = Assert.Throws<InvalidOperationException>(
			() => GoogleSheetProvider.ExtractCredentialJsonObjects("42"));

		Assert.Contains("unexpected format", ex.Message);
	}

	[Fact]
	public void Extract_ErrorMessage_MentionsBothFormats()
	{
		var ex = Assert.Throws<InvalidOperationException>(
			() => GoogleSheetProvider.ExtractCredentialJsonObjects("true"));

		Assert.Contains("{}", ex.Message);
		Assert.Contains("[{}", ex.Message);
	}

	// ── Constructor argument validation ──────────────────────────────────────

	[Fact]
	public void Constructor_NullSpreadsheetId_Throws()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => new GoogleSheetProvider((string)null!, "path.json"));

		Assert.Equal("spreadsheetId", ex.ParamName);
		Assert.Contains("Spreadsheet ID", ex.Message);
	}

	[Fact]
	public void Constructor_EmptySpreadsheetId_Throws()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => new GoogleSheetProvider("", "path.json"));

		Assert.Equal("spreadsheetId", ex.ParamName);
	}

	[Fact]
	public void Constructor_WhitespaceSpreadsheetId_Throws()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => new GoogleSheetProvider("   ", "path.json"));

		Assert.Equal("spreadsheetId", ex.ParamName);
	}

	[Fact]
	public void Constructor_NullCredentialsPath_Throws()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => new GoogleSheetProvider("spreadsheet-id", null!));

		Assert.Equal("credentialsPath", ex.ParamName);
		Assert.Contains("path", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void Constructor_EmptyCredentialsPath_Throws()
	{
		var ex = Assert.Throws<ArgumentException>(
			() => new GoogleSheetProvider("spreadsheet-id", ""));

		Assert.Equal("credentialsPath", ex.ParamName);
	}

	[Fact]
	public void Constructor_NonExistentCredentialsFile_ThrowsFileNotFound()
	{
		var ex = Assert.Throws<FileNotFoundException>(
			() => new GoogleSheetProvider("spreadsheet-id", "nonexistent_credentials.json"));

		Assert.Contains("nonexistent_credentials.json", ex.Message);
		Assert.Contains("service account", ex.Message);
	}

	[Fact]
	public void Constructor_ErrorMessage_MentionsGoogleCloudConsole()
	{
		var ex = Assert.Throws<FileNotFoundException>(
			() => new GoogleSheetProvider("some-id", "no-such-file.json"));

		Assert.Contains("Google Cloud Console", ex.Message);
	}
}
