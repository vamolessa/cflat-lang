using Xunit;

public sealed class ParserTest
{
	public static string CopmileExpression(string source)
	{
		var compiler = new LangCompiler();
		var compileResult = compiler.CompileExpression(source);
		if (!compileResult.isOk)
			return "COMPILE ERROR: " + CompilerHelper.FormatError(source, compileResult.error, 1, 8);
		return null;
	}

	[Theory]
	[InlineData("-1")]
	[InlineData("1 + 2")]
	[InlineData("1 * -2")]
	[InlineData("1 + 2 * 3")]
	[InlineData("(1 + 2) + 3 * 4 + 5")]
	[InlineData("(1 + 2) + 3 == 4 + 5")]
	[InlineData("1 < 2 != 3 >= 4")]
	[InlineData("true == !false")]
	[InlineData("true or false")]
	[InlineData("true and false or 3 > 2")]
	[InlineData("{let assign = true or false assign}")]
	public void TestExpressions(string source)
	{
		var result = CopmileExpression(source);
		Assert.Null(result);
	}

	[Theory]
	[InlineData("{while true { 1 + 2 }}")]
	[InlineData("if true { {} }")]
	[InlineData("if true { false } else { 3 == 4 }")]
	[InlineData("if true {false}else if 2>3 {3==4}else{let c=false c}")]
	[InlineData("if if true { false } else { true } { 4 != 6 {} }")]
	public void TestComplexExpressions(string source)
	{
		var result = CopmileExpression(source);
		Assert.Null(result);
	}

	[Theory]
	[InlineData("{let a = 2 let b = 3 a + b}")]
	[InlineData("{let a = 2 let b = 3 + 4 a + b}")]
	[InlineData("{let a=if true{1<2}else{let b=3+4 b!=0} !a}")]
	public void TestMultiExpressions(string source)
	{
		var result = CopmileExpression(source);
		Assert.Null(result);
	}

	/*
		[Theory]
		[InlineData("fn foo(){}")]
		[InlineData("fn foo(a,b) { return true }")]
		[InlineData("fn foo(a,b) { fn bar() { return {} } return true }")]
		public void TestFunctionDeclaration(string source)
		{
			var result = CopmileExpression(source);
			Assert.Null(result);
		}
	*/
}