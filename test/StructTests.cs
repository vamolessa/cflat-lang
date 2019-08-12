using Xunit;

public sealed class StructTests
{
	public static string Run(string source, out ValueData value, out ValueType type)
	{
		const int TabSize = 8;
		value = new ValueData();
		type = new ValueType();

		var pepper = new Pepper();
		var compileErrors = pepper.CompileSource(source);
		if (compileErrors.Count > 0)
			return "COMPILE ERROR: " + CompilerHelper.FormatError(source, compileErrors, 1, TabSize);

		var runError = pepper.RunLastFunction();
		if (runError.isSome)
			return "RUNTIME ERROR: " + VirtualMachineHelper.FormatError(source, runError.value, 1, TabSize);

		pepper.virtualMachine.PopSimple(out value, out type);
		return null;
	}

	[Theory]
	[InlineData("struct S{a:int} fn f():int{let s=S{a=3} s.a}", 3)]
	[InlineData("struct S{a:int b:int} fn f():int{let s=S{a=3 b=7} s.a}", 3)]
	[InlineData("struct S{a:int b:int} fn f():int{let s=S{a=3 b=7} s.b}", 7)]
	[InlineData("struct S{a:int b:int c:int} fn f():int{let s=S{a=3 b=7 c=9} s.a}", 3)]
	[InlineData("struct S{a:int b:int c:int} fn f():int{let s=S{a=3 b=7 c=9} s.b}", 7)]
	[InlineData("struct S{a:int b:int c:int} fn f():int{let s=S{a=3 b=7 c=9} s.c}", 9)]
	[InlineData("struct S{a:int b:int} struct T{s:S c:int} fn f():int{let t=T{s=S{a=3 b=7} c=9} t.s.a}", 3)]
	[InlineData("struct S{a:int b:int} struct T{s:S c:int} fn f():int{let t=T{s=S{a=3 b=7} c=9} t.s.b}", 7)]
	[InlineData("struct S{a:int b:int} struct T{s:S c:int} fn f():int{let t=T{s=S{a=3 b=7} c=9} t.c}", 9)]
	public void StructFieldTests(string source, int expected)
	{
		var error = Run(source, out var v, out var t);
		Assert.Null(error);
		Assert.Equal(new ValueType(TypeKind.Int), t);
		Assert.Equal(expected, v.asInt);
	}
}