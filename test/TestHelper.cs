using Xunit;

public sealed class CompileErrorException : System.Exception
{
	public CompileErrorException(string error) : base(error) { }
}

public static class TestHelper
{
	public const int TabSize = 8;

	public readonly struct CallAssertion
	{
		private readonly string source;
		private readonly Pepper pepper;

		public CallAssertion(string source, Pepper pepper)
		{
			this.source = source;
			this.pepper = pepper;
		}

		public void AssertSuccessCall()
		{
			string errorMessage = null;
			var error = pepper.GetError();
			if (error.isSome)
				errorMessage = VirtualMachineHelper.FormatError(source, error.value, 1, TabSize);
			Assert.Null(errorMessage);
		}
	}

	public static T[] BufferToArray<T>(Buffer<T> buffer)
	{
		var array = new T[buffer.count];
		if (buffer.count > 0)
			System.Array.Copy(buffer.buffer, 0, array, 0, array.Length);
		return array;
	}

	public static R Run<R>(string source, out CallAssertion assertion)
		where R : struct, IMarshalable
	{
		return Run<R>(new Pepper(), source, out assertion);
	}

	public static R Run<R>(Pepper pepper, string source, out CallAssertion assertion)
		where R : struct, IMarshalable
	{
		var compileErrors = pepper.CompileSource(source);
		if (compileErrors.count > 0)
			throw new CompileErrorException(CompilerHelper.FormatError(source, compileErrors, 1, TabSize));

		assertion = new CallAssertion(source, pepper);
		return pepper.GetFunction<Empty, R>("f").value.Call(pepper, new Empty());
	}

	public static R RunExpression<R>(string source, out CallAssertion assertion)
	where R : struct, IMarshalable
	{
		return RunExpression<R>(new Pepper(), source, out assertion);
	}

	public static R RunExpression<R>(Pepper pepper, string source, out CallAssertion assertion)
		where R : struct, IMarshalable
	{
		var compileErrors = pepper.CompileExpression(source);
		if (compileErrors.count > 0)
			throw new CompileErrorException(CompilerHelper.FormatError(source, compileErrors, 1, TabSize));

		assertion = new CallAssertion(source, pepper);
		return pepper.GetFunction<Empty, R>(string.Empty).value.Call(pepper, new Empty());
	}
}