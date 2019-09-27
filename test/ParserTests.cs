using Xunit;

public sealed class ParserTest
{
	public static string CompileExpression(string source)
	{
		const int TabSize = 8;
		var compiler = new CompilerController();
		var compileErrors = compiler.CompileExpression(source, new ByteCodeChunk(), TestHelper.CompilerMode);
		if (compileErrors.count > 0)
			return "COMPILE ERROR: " + CompilerHelper.FormatError(source, compileErrors, 1, TabSize);
		return null;
	}

	[Theory]
	[InlineData(".")]
	[InlineData(".1")]
	public void TestFailExpressions(string source)
	{
		var result = CompileExpression(source);
		Assert.NotNull(result);
	}

	[Theory]
	[InlineData("-1")]
	[InlineData("1.0")]
	[InlineData("3341.1234")]
	[InlineData("int 3341.1234")]
	[InlineData("float 3341")]
	[InlineData("1 + 2")]
	[InlineData("1 * -2")]
	[InlineData("1 + 2 * 3")]
	[InlineData("(1 + 2) + 3 * 4 + 5")]
	[InlineData("(1 + 2) + 3 == 4 + 5")]
	[InlineData("1 < 2 != 3 >= 4")]
	[InlineData("true != false")]
	[InlineData("true || false")]
	[InlineData("true && !false")]
	[InlineData("true && false || 3 > 2")]
	[InlineData("{let assign = true || false assign}")]
	public void TestExpressions(string source)
	{
		var result = CompileExpression(source);
		Assert.Null(result);
	}

	[Theory]
	[InlineData("{while true { 1 + 2 }}")]
	[InlineData("if true { {} }")]
	[InlineData("if true { false } else { 3 == 4 }")]
	[InlineData("if true {false}else if 2>3 {3 == 4}else{let c=false c}")]
	[InlineData("if if true { false } else { true } { 4 != 6 {} }")]
	public void TestComplexExpressions(string source)
	{
		var result = CompileExpression(source);
		Assert.Null(result);
	}

	[Theory]
	[InlineData("{let a = 2 let b = 3 a + b}")]
	[InlineData("{let a = 2 let b = 3 + 4 a + b}")]
	[InlineData("{let a=if true{1<2}else{let b=3+4 b != 0} !a}")]
	public void TestMultiExpressions(string source)
	{
		var result = CompileExpression(source);
		Assert.Null(result);
	}

	[Theory]
	[InlineData("fn(){}")]
	[InlineData("fn(){return}")]
	[InlineData("fn(){return{}}")]
	[InlineData("fn(a:int,b:int):bool{true}")]
	[InlineData("fn(a:int,b:int):bool{return true}")]
	[InlineData("fn(a:int,b:int):bool{fn(){return {}} true}")]
	public void TestFunctionDeclaration(string source)
	{
		var result = CompileExpression(source);
		Assert.Null(result);
	}
}