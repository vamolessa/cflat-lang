using Xunit;

public sealed class LangTests
{
	public static string RunExpression(string source, out ValueData value, out ValueType type)
	{
		LangParseRules.InitRules();

		value = new ValueData();
		type = new ValueType();

		var tokenizer = new Tokenizer();
		var compiler = new LangCompiler();

		var compileResult = compiler.CompileExpression(source, tokenizer);
		if (!compileResult.isOk)
			return "COMPILE ERROR: " + CompilerHelper.FormatError(source, compileResult.error, 1, 8);

		var vm = new VirtualMachine();
		var runResult = vm.Run(source, compileResult.ok);
		if (!runResult.isOk)
			return "RUNTIME ERROR: " + VirtualMachineHelper.FormatError(source, runResult.error, 1, 8);

		type = vm.PeekType();
		value = vm.PopValue();
		return null;
	}

	[Theory]
	[InlineData("if true {}")]
	[InlineData("if true {} else {}")]
	[InlineData("if true {} else if false {}")]
	[InlineData("if true {} else if false {} else {}")]
	[InlineData("if true {let a=0 a+1 nil}")]
	[InlineData("if true {nil}")]
	public void IfNilTests(string source)
	{
		var error = RunExpression(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(ValueType.Nil, t);
	}

	[Theory]
	[InlineData("if true {0} else {1}", 0)]
	[InlineData("if 2>3 {0} else {1}", 1)]
	[InlineData("if 3>3 {1} else if 3<3 {-1} else {0}", 0)]
	[InlineData("if if false{true}else{false} {1} else {0}", 0)]
	public void IfIntTests(string source, int expected)
	{
		var error = RunExpression(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(ValueType.Int, t);
		Assert.Equal(expected, v.asInt);
	}
}