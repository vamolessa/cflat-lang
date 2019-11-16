using Xunit;

public sealed class MultipleSourcesTests
{
	private sealed class ModuleResolver : IModuleResolver
	{
		private readonly Source[] sources;

		public ModuleResolver(params Source[] sources)
		{
			this.sources = sources;
		}

		public Option<string> ResolveModuleUri(string requestingSourceUri, string modulePath)
		{
			foreach (var source in sources)
			{
				if (source.uri == modulePath)
					return source.uri;
			}

			return Option.None;
		}

		public Option<string> ResolveModuleSource(string requestingSourceUri, string moduleUri)
		{
			foreach (var source in sources)
			{
				if (source.uri == moduleUri)
					return source.content;
			}

			return Option.None;
		}
	}

	[Theory]
	[InlineData("mod \"source0\"", "")]
	[InlineData("mod \"source1\"", "")]
	[InlineData("mod \"source1\" struct S{a:int} fn f(){let _=S{a=0}}", "")]
	[InlineData("mod \"source1\"", "struct S{a:int} fn f(){let _=S{a=0}}")]
	[InlineData("mod \"source1\" fn f(){let _=S{a=0}}", "pub struct S{a:int}")]
	[InlineData("mod \"source1\" mod \"source1\" fn f(){let _=S{a=0}}", "pub struct S{a:int}")]
	[InlineData("mod \"source1\"", "mod \"source0\"")]
	[InlineData("mod \"source1\" struct S{a:int}", "struct S{a:int}")]
	public void CompileMultipleSourcesTests(string source0, string source1)
	{
		var cflat = new CFlat();
		var moduleResolver = new ModuleResolver(
			new Source("source0", source0),
			new Source("source1", source1)
		);

		var errors = cflat.CompileSource("source0", source0, TestHelper.CompilerMode, moduleResolver);
		if (errors.count > 0)
			throw new CompileErrorException(cflat.GetFormattedCompileErrors());
	}

	[Theory]
	[InlineData("mod \"source1\" fn f(){let _=S{a=0}}", "struct S{a:int}")]
	[InlineData("mod \"source1\" struct S{a:int}", "pub struct S{a:int}")]
	public void CompileMultipleSourcesErrors(string source0, string source1)
	{
		var cflat = new CFlat();
		var moduleResolver = new ModuleResolver(
			new Source("source0", source0),
			new Source("source1", source1)
		);

		var errors = cflat.CompileSource("source0", source0, TestHelper.CompilerMode, moduleResolver);
		Assert.NotEqual(0, errors.count);
	}
}
