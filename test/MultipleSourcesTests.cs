using Xunit;
using cflat;

public sealed class MultipleSourcesTests
{
	private sealed class ModuleResolver : IModuleResolver
	{
		private readonly Source[] sources;

		public ModuleResolver(Source[] sources)
		{
			this.sources = sources;
		}

		public Option<string> ResolveModuleSource(Uri requestingSourceUri, Uri moduleUri)
		{
			foreach (var source in sources)
			{
				if (source.uri.value == moduleUri.value)
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
	[InlineData("mod \"source1\" pub struct S{a:int}", "struct S{a:int}")]
	[InlineData("mod \"source1\" pub fn f(){}", "fn f(){}")]
	public void CompileMultipleSourcesTests(string source0, string source1)
	{
		var sources = new Source[] {
			new Source(new Uri("source0"), source0),
			new Source(new Uri("source1"), source1)
		};
		var cflat = new CFlat();
		var moduleResolver = new ModuleResolver(sources);

		var errors = cflat.CompileSource(sources[0], TestHelper.CompilerMode, moduleResolver);
		if (errors.count > 0)
			throw new CompileErrorException(cflat.GetFormattedCompileErrors());
	}

	[Theory]
	[InlineData("mod \"source1\" fn f(){let _=S{a=0}}", "struct S{a:int}")]
	[InlineData("mod \"source1\" struct S{a:int}", "pub struct S{a:int}")]
	[InlineData("mod \"source1\" fn f(){}", "pub fn f(){}")]
	public void CompileMultipleSourcesErrors(string source0, string source1)
	{
		var sources = new Source[] {
			new Source(new Uri("source0"), source0),
			new Source(new Uri("source1"), source1)
		};
		var cflat = new CFlat();
		var moduleResolver = new ModuleResolver(sources);

		var errors = cflat.CompileSource(sources[0], TestHelper.CompilerMode, moduleResolver);
		Assert.NotEqual(0, errors.count);
	}
}
