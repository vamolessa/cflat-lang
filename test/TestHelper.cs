using Xunit;

public sealed class CompileErrorException : System.Exception
{
	public CompileErrorException(string error) : base(error) { }
}

public sealed class FunctionNotFoundException : System.Exception
{
}

public static class TestHelper
{
	public static readonly Mode CompilerMode = Mode.Debug;

	public readonly struct CallAssertion
	{
		private readonly string source;
		private readonly CFlat cflat;

		public CallAssertion(string source, CFlat cflat)
		{
			this.source = source;
			this.cflat = cflat;
		}

		public void AssertSuccessCall()
		{
			string errorMessage = null;
			var error = cflat.GetRuntimeError();
			if (error.isSome)
				errorMessage = cflat.GetFormattedCompileErrors();
			Assert.Null(errorMessage);

			if (CompilerMode == Mode.Release)
				cflat.vm.debugData.typeStack.PushBack(new ValueType(TypeKind.Unit));
			Assert.Single(cflat.vm.debugData.typeStack.ToArray());
		}
	}

	public static R Run<R>(string source, out CallAssertion assertion)
		where R : struct, IMarshalable
	{
		return Run<R>(new CFlat(), source, out assertion);
	}

	public static R Run<R>(CFlat cflat, string source, out CallAssertion assertion)
		where R : struct, IMarshalable
	{
		var compileErrors = cflat.CompileSource("tests", source, CompilerMode, Option.None);
		if (compileErrors.count > 0)
			throw new CompileErrorException(cflat.GetFormattedCompileErrors());

		assertion = new CallAssertion(source, cflat);
		var function = cflat.GetFunction<Empty, R>("f");
		if (!function.isSome)
			throw new FunctionNotFoundException();

		return function.value.Call(cflat, new Empty());
	}

	public static R RunExpression<R>(string source, out CallAssertion assertion)
	where R : struct, IMarshalable
	{
		return RunExpression<R>(new CFlat(), source, out assertion);
	}

	public static R RunExpression<R>(CFlat cflat, string source, out CallAssertion assertion)
		where R : struct, IMarshalable
	{
		var compileErrors = cflat.CompileExpression(source, CompilerMode);
		if (compileErrors.count > 0)
			throw new CompileErrorException(cflat.GetFormattedCompileErrors());

		assertion = new CallAssertion(source, cflat);
		var function = cflat.GetFunction<Empty, R>(string.Empty);
		if (!function.isSome)
			throw new FunctionNotFoundException();

		return function.value.Call(cflat, new Empty());
	}
}