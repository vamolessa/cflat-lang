using Xunit;

public sealed class CompileErrorException : System.Exception
{
	public CompileErrorException(string error) : base(error) { }
}

public static class TestHelper
{
	public static readonly Mode CompilerMode = Mode.Debug;
	public const int TabSize = 8;

	public readonly struct CallAssertion
	{
		private readonly string source;
		private readonly Clef clef;

		public CallAssertion(string source, Clef clef)
		{
			this.source = source;
			this.clef = clef;
		}

		public void AssertSuccessCall()
		{
			string errorMessage = null;
			var error = clef.GetError();
			if (error.isSome)
				errorMessage = VirtualMachineHelper.FormatError(source, error.value, 1, TabSize);
			Assert.Null(errorMessage);

			{
				var flags =
					System.Reflection.BindingFlags.NonPublic |
					System.Reflection.BindingFlags.Instance;
				var virtualMachineField = clef.GetType().GetField("virtualMachine", flags);
				var virtualMachine = virtualMachineField.GetValue(clef);
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
		return Run<R>(new Clef(), source, out assertion);
	}

	public static R Run<R>(Clef clef, string source, out CallAssertion assertion)
		where R : struct, IMarshalable
	{
		var compileErrors = clef.CompileSource(source, CompilerMode);
		if (compileErrors.count > 0)
			throw new CompileErrorException(CompilerHelper.FormatError(source, compileErrors, 1, TabSize));

		assertion = new CallAssertion(source, clef);
		return clef.GetFunction<Empty, R>("f").value.Call(clef, new Empty());
	}

	public static R RunExpression<R>(string source, out CallAssertion assertion)
	where R : struct, IMarshalable
	{
		return RunExpression<R>(new Clef(), source, out assertion);
	}

	public static R RunExpression<R>(Clef clef, string source, out CallAssertion assertion)
		where R : struct, IMarshalable
	{
		var compileErrors = clef.CompileExpression(source, CompilerMode);
		if (compileErrors.count > 0)
			throw new CompileErrorException(CompilerHelper.FormatError(source, compileErrors, 1, TabSize));

		assertion = new CallAssertion(source, clef);
		return clef.GetFunction<Empty, R>(string.Empty).value.Call(clef, new Empty());
	}
}