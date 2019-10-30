using Xunit;

public sealed class MultipleSourcesTests
{
	[Theory]
	[InlineData("", "")]
	[InlineData("struct S{a:int} fn f(){let _=S{a=0}}", "")]
	[InlineData("", "struct S{a:int} fn f(){let _=S{a=0}}")]
	[InlineData("struct S{a:int}", "fn f(){let _=S{a=0}}")]
	public void CompileMultipleSourcesTests(string source0, string source1)
	{
		var cflat = new CFlat();
		var errors = cflat.CompileSource("source0", source0, TestHelper.CompilerMode);
		if (errors.count > 0)
			throw new CompileErrorException(cflat.GetFormattedCompileErrors(1));
		errors = cflat.CompileSource("source1", source1, TestHelper.CompilerMode);
		if (errors.count > 0)
			throw new CompileErrorException(cflat.GetFormattedCompileErrors(1));
		Assert.True(cflat.Load());
	}

	[Theory]
	[InlineData("", "")]
	[InlineData("struct S{a:int}", "")]
	[InlineData("", "struct S{a:int}")]
	[InlineData("struct S{a:int}", "struct S{a:int}")]
	public void ClearThenCompileTests(string source0, string source1)
	{
		var cflat = new CFlat();
		var errors = cflat.CompileSource("source0", source0, TestHelper.CompilerMode);
		if (errors.count > 0)
			throw new CompileErrorException(cflat.GetFormattedCompileErrors(1));
		cflat.Clear();
		errors = cflat.CompileSource("source1", source1, TestHelper.CompilerMode);
		if (errors.count > 0)
			throw new CompileErrorException(cflat.GetFormattedCompileErrors(1));
		Assert.True(cflat.Load());
	}
}