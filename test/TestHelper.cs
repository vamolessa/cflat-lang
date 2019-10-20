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
	public const int TabSize = 8;

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
			var error = cflat.GetError();
			if (error.isSome)
				errorMessage = VirtualMachineHelper.FormatError(source, error.value, 1, TabSize);
			Assert.Null(errorMessage);

			{
				var flags =
					System.Reflection.BindingFlags.NonPublic |
					System.Reflection.BindingFlags.Instance;
				var virtualMachineField = cflat.GetType().GetField("virtualMachine", flags);
				var virtualMachine = virtualMachineField.GetValue(cflat);
				var debugDataField = virtualMachineField.FieldType.GetField("debugData", flags);
				var debugData = debugDataField.GetValue(virtualMachine);
				var typeStackField = debugDataField.FieldType.GetField("typeStack");
				var typeStack = (Buffer<ValueType>)typeStackField.GetValue(debugData);

				if (CompilerMode == Mode.Release)
					typeStack.PushBack(new ValueType(TypeKind.Unit));
				Assert.Single(typeStack.ToArray());
			}
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
		var compileErrors = cflat.CompileSource("tests", source, CompilerMode);
		if (compileErrors.count > 0)
			throw new CompileErrorException(CompilerHelper.FormatError(source, compileErrors, 1, TabSize));

		cflat.Load();
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
			throw new CompileErrorException(CompilerHelper.FormatError(source, compileErrors, 1, TabSize));

		cflat.Load();
		assertion = new CallAssertion(source, cflat);
		var function = cflat.GetFunction<Empty, R>(string.Empty);
		if (!function.isSome)
			throw new FunctionNotFoundException();

		return function.value.Call(cflat, new Empty());
	}
}