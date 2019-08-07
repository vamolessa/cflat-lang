using Xunit;

public sealed class LangTests
{
	public static string RunExpression(string source, out ValueData value, out ValueType type)
	{
		value = new ValueData();
		type = new ValueType();

		var compiler = new Compiler();

		var compileResult = compiler.CompileExpression(source);
		if (!compileResult.isOk)
			return "COMPILE ERROR: " + CompilerHelper.FormatError(source, compileResult.error, 1, 8);

		var vm = new VirtualMachine();
		var runResult = vm.Run(compileResult.ok, "main");
		if (!runResult.isOk)
			return "RUNTIME ERROR: " + VirtualMachineHelper.FormatError(source, runResult.error, 1, 8);

		type = vm.PeekType();
		value = vm.PopValue();
		return null;
	}

	[Theory]
	[InlineData("{}")]
	[InlineData("{{}}")]
	[InlineData("{mut a=4 a=a+1 {}}")]
	public void BlockUnitTests(string source)
	{
		var error = RunExpression(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(ValueType.Unit, t);
	}

	[Theory]
	[InlineData("{0}", 0)]
	[InlineData("{4}", 4)]
	[InlineData("{({4})}", 4)]
	[InlineData("{let a=4 a}", 4)]
	[InlineData("{let a=4 a+5}", 9)]
	[InlineData("{let a=4 {let a=2 a+1} a+5}", 9)]
	public void BlockIntTests(string source, int expected)
	{
		var error = RunExpression(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(ValueType.Int, t);
		Assert.Equal(expected, v.asInt);
	}

	[Theory]
	[InlineData("if true {}")]
	[InlineData("if true {} else {}")]
	[InlineData("if true {} else if false {}")]
	[InlineData("if true {} else if false {} else {}")]
	[InlineData("if true {let a=0 a+1 {}}")]
	[InlineData("if true {{}}")]
	public void IfUnitTests(string source)
	{
		var error = RunExpression(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(ValueType.Unit, t);
	}

	[Theory]
	[InlineData("if true {4} else {5}", 4)]
	[InlineData("if 2>3 {4} else {5}", 5)]
	[InlineData("if 3>3 {4} else if 3<3 {-4} else {5}", 5)]
	[InlineData("if if false{true}else{false} {4} else {5}", 5)]
	[InlineData("if true {4} else {5} + 10", 14)]
	[InlineData("20 + if true {4} else {5}", 24)]
	public void IfIntTests(string source, int expected)
	{
		var error = RunExpression(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(ValueType.Int, t);
		Assert.Equal(expected, v.asInt);
	}

	[Theory]
	[InlineData("true and true", true)]
	[InlineData("true and false", false)]
	[InlineData("false and true", false)]
	[InlineData("false and false", false)]
	[InlineData("true or true", true)]
	[InlineData("true or false", true)]
	[InlineData("false or true", true)]
	[InlineData("false or false", false)]
	[InlineData("{mut a=false true or {a=true false} a}", false)]
	[InlineData("{mut a=false false and {a=true true} a}", false)]
	public void LogicalTests(string source, bool expected)
	{
		var error = RunExpression(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(ValueType.Bool, t);
		Assert.Equal(expected, v.asBool);
	}

	[Theory]
	[InlineData("{mut a=4 a=a+1 a}", 5)]
	[InlineData("{mut a=4 a=a=5 a}", 5)]
	[InlineData("{mut a=4 a=a=a+1 a}", 5)]
	[InlineData("{mut a=4 mut b=5 b+1 a=b=7 a}", 7)]
	public void AssignmentIntTests(string source, int expected)
	{
		var error = RunExpression(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(ValueType.Int, t);
		Assert.Equal(expected, v.asInt);
	}
}