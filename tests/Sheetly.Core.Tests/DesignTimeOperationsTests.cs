using Sheetly.Core.Migration;
using Sheetly.Core.Migrations.Design;
using System.Reflection;
using System.Reflection.Emit;

namespace Sheetly.Core.Tests.DesignTimeNs
{
	public class FakeAppDbContext { }
}

namespace Sheetly.Core.Tests.DesignTimeNs.Data
{
	public class FakeAppDbContext { }
}

namespace Sheetly.Core.Tests
{

	public class DesignTimeOperationsTests
	{
		[Fact]
		public void ResolveNamespace_WithNormalNamespace_ReturnsDotMigrations()
		{
			var ns = DesignTimeOperations.ResolveNamespace(typeof(DesignTimeNs.FakeAppDbContext));

			Assert.Equal("Sheetly.Core.Tests.DesignTimeNs.Migrations", ns);
		}

		[Fact]
		public void ResolveNamespace_WithDeepNamespace_ReturnsDotMigrations()
		{
			var ns = DesignTimeOperations.ResolveNamespace(typeof(DesignTimeNs.Data.FakeAppDbContext));

			Assert.Equal("Sheetly.Core.Tests.DesignTimeNs.Data.Migrations", ns);
		}

		[Fact]
		public void ResolveNamespace_WithNullNamespace_UsesAssemblyName()
		{
			var type = CreateGlobalNamespaceType("GlobalApp");

			var ns = DesignTimeOperations.ResolveNamespace(type);

			Assert.False(ns.StartsWith('.'), $"Namespace started with dot: '{ns}'");
			Assert.True(ns.EndsWith(".Migrations"), $"Namespace must end with .Migrations: '{ns}'");
			Assert.False(string.IsNullOrEmpty(ns.Replace(".Migrations", "")), "Root part of namespace must not be empty");
		}

		[Fact]
		public void ResolveNamespace_NeverStartsWithDot()
		{
			var type = CreateGlobalNamespaceType("SomeAssembly");

			var ns = DesignTimeOperations.ResolveNamespace(type);

			Assert.False(ns.StartsWith('.'), $"Namespace started with dot: '{ns}'");
		}

		[Fact]
		public void ResolveNamespace_GlobalNamespace_AssemblyNameUsedExactly()
		{
			var type = CreateGlobalNamespaceType("Contoso.TestApp");

			var ns = DesignTimeOperations.ResolveNamespace(type);

			Assert.Equal("Contoso.TestApp.Migrations", ns);
		}

		[Fact]
		public void ResolveNamespace_AlwaysEndsWith_DotMigrations()
		{
			var types = new[]
			{
			typeof(DesignTimeNs.FakeAppDbContext),
			typeof(DesignTimeNs.Data.FakeAppDbContext),
			CreateGlobalNamespaceType("SomeAssembly")
		};

			foreach (var t in types)
			{
				var ns = DesignTimeOperations.ResolveNamespace(t);
				Assert.True(ns.EndsWith(".Migrations"), $"Namespace did not end with .Migrations for {t}: '{ns}'");
			}
		}

		[Fact]
		public void ResolveNamespace_DoesNotContain_DoubleDot()
		{
			var types = new[]
			{
			typeof(DesignTimeNs.FakeAppDbContext),
			typeof(DesignTimeNs.Data.FakeAppDbContext),
			CreateGlobalNamespaceType("SomeAssembly")
		};

			foreach (var t in types)
			{
				var ns = DesignTimeOperations.ResolveNamespace(t);
				Assert.False(ns.Contains(".."), $"Namespace contained double dot: '{ns}'");
			}
		}

		[Fact]
		public void ResolveNamespace_GlobalContext_GeneratedFileHasCorrectNamespace()
		{
			var type = CreateGlobalNamespaceType("TestProject");
			var ns = DesignTimeOperations.ResolveNamespace(type);

			var generator = new CSharpMigrationGenerator();
			var code = generator.GenerateMigration("Init", "id", ns, []);

			Assert.Contains($"namespace {ns};", code);
			Assert.DoesNotContain("namespace .Migrations;", code);
		}

		[Fact]
		public void ResolveNamespace_GlobalContext_SnapshotHasCorrectNamespace()
		{
			var type = CreateGlobalNamespaceType("TestProject");
			var ns = DesignTimeOperations.ResolveNamespace(type);

			var generator = new ModelSnapshotGenerator();
			var snapshot = new Sheetly.Core.Migration.MigrationSnapshot { Entities = [] };
			var code = generator.GenerateModelSnapshot(snapshot, ns, "AppDb");

			Assert.Contains($"namespace {ns};", code);
			Assert.DoesNotContain("namespace .Migrations;", code);
		}

		[Fact]
		public void Scaffold_NullableString_EmitsQuestionMark()
		{
			var entity = new EntitySchema
			{
				TableName = "Products",
				Namespace = "App",
				Columns =
				[
					new ColumnSchema { Name = "Id", PropertyName = "Id", DataType = "Int32", IsPrimaryKey = true },
					new ColumnSchema { Name = "Note", PropertyName = "Note", DataType = "String", IsNullable = true },
					new ColumnSchema { Name = "Title", PropertyName = "Title", DataType = "String", IsNullable = false }
				]
			};

			var code = DesignTimeOperations.GenerateClassCode(entity, "Product");

			Assert.Contains("public string? Note { get; set; }", code);
			Assert.Contains("public string Title { get; set; }", code);
			Assert.DoesNotContain("string?? ", code);
		}

		[Fact]
		public void Scaffold_DuplicatePropertyNames_AreDeduped()
		{
			var entity = new EntitySchema
			{
				TableName = "T",
				Namespace = "App",
				Columns =
				[
					new ColumnSchema { Name = "Name", PropertyName = "Name", DataType = "String", IsNullable = false },
					new ColumnSchema { Name = "name", PropertyName = "Name", DataType = "String", IsNullable = false }
				]
			};

			var code = DesignTimeOperations.GenerateClassCode(entity, "T");

			Assert.Contains("public string Name { get; set; }", code);
			Assert.Contains("public string Name2 { get; set; }", code);
		}

		[Fact]
		public void Scaffold_PropertyMatchingClassName_IsDeduped()
		{
			var entity = new EntitySchema
			{
				TableName = "Product",
				Namespace = "App",
				Columns = [new ColumnSchema { Name = "Product", PropertyName = "Product", DataType = "String", IsNullable = false }]
			};

			var code = DesignTimeOperations.GenerateClassCode(entity, "Product");

			Assert.DoesNotContain("public string Product { get; set; }", code);
			Assert.Contains("public string Product2 { get; set; }", code);
		}

		[Theory]
		[InlineData("CON", "_CON.cs")]
		[InlineData("con", "_con.cs")]
		[InlineData("Lpt1", "_Lpt1.cs")]
		[InlineData("Product", "Product.cs")]
		public void Scaffold_ReservedDeviceName_IsPrefixed(string baseName, string expected)
		{
			var file = DesignTimeOperations.UniqueFileName(baseName, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
			Assert.Equal(expected, file);
		}

		[Fact]
		public void Scaffold_CollidingFileNames_AreSuffixed()
		{
			var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			Assert.Equal("Order.cs", DesignTimeOperations.UniqueFileName("Order", used));
			Assert.Equal("Order_2.cs", DesignTimeOperations.UniqueFileName("Order", used));
			Assert.Equal("order_3.cs", DesignTimeOperations.UniqueFileName("order", used));
		}

		private static Type CreateGlobalNamespaceType(string assemblyName)
		{
			var asmName = new AssemblyName(assemblyName);
			var asmBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
			var moduleBuilder = asmBuilder.DefineDynamicModule("MainModule");
			var typeBuilder = moduleBuilder.DefineType("AppDbContext", TypeAttributes.Public);
			var type = typeBuilder.CreateType()!;
			Assert.Null(type.Namespace);
			return type;
		}
	}

}
