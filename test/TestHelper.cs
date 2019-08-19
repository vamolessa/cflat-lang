public static class TestHelper
{
	public static T[] BufferToArray<T>(Buffer<T> buffer)
	{
		var array = new T[buffer.count];
		if (buffer.count > 0)
			System.Array.Copy(buffer.buffer, 0, array, 0, array.Length);
		return array;
	}

	public static string Run(string source, out ValueData value, out ValueType type)
	{
		const int TabSize = 8;
		value = new ValueData();
		type = new ValueType();

		var pepper = new Pepper();
		var compileErrors = pepper.CompileSource(source);
		if (compileErrors.count > 0)
			return "COMPILE ERROR: " + CompilerHelper.FormatError(source, compileErrors, 1, TabSize);

		var runErrors = pepper.RunLastFunction();
		if (runErrors.count > 0)
			return "RUNTIME ERROR: " + VirtualMachineHelper.FormatError(source, runErrors, 1, TabSize);

		value = pepper.Pop();
		var lastFunction = pepper.byteCode.functions.buffer[pepper.byteCode.functions.count - 1];
		type = pepper.byteCode.functionTypes.buffer[lastFunction.typeIndex].returnType;

		return null;
	}

	public static string RunExpression(string source, out ValueData value, out ValueType type)
	{
		const int TabSize = 8;
		value = new ValueData();
		type = new ValueType();

		var pepper = new Pepper();
		var compileErrors = pepper.CompileExpression(source);
		if (compileErrors.count > 0)
			return "COMPILE ERROR: " + CompilerHelper.FormatError(source, compileErrors, 1, TabSize);

		var runErrors = pepper.RunLastFunction();
		if (runErrors.count > 0)
			return "RUNTIME ERROR: " + VirtualMachineHelper.FormatError(source, runErrors, 1, TabSize);

		value = pepper.Pop();
		var lastFunction = pepper.byteCode.functions.buffer[pepper.byteCode.functions.count - 1];
		type = pepper.byteCode.functionTypes.buffer[lastFunction.typeIndex].returnType;

		return null;
	}
}