public static class TestHelper
{
	public static string RunInt(string source, out ValueData value, out ValueType type)
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

		new RuntimeContext(pepper.virtualMachine).Pop(out value.asInt);
		var lastFunction = pepper.byteCode.functions.buffer[pepper.byteCode.functions.count - 1];
		type = pepper.byteCode.functionTypes.buffer[lastFunction.typeIndex].returnType;

		return null;
	}

	public static string RunExpressionInt(string source, out ValueData value, out ValueType type)
	{
		const int TabSize = 8;
		value = new ValueData();
		type = new ValueType();

		var pepper = new Pepper();
		var compileErrors = pepper.CompileExpression(source);
		if (compileErrors.Count > 0)
			return "COMPILE ERROR: " + CompilerHelper.FormatError(source, compileErrors, 1, TabSize);

		var runError = pepper.RunLastFunction();
		if (runError.isSome)
			return "RUNTIME ERROR: " + VirtualMachineHelper.FormatError(source, runError.value, 1, TabSize);

		new RuntimeContext(pepper.virtualMachine).Pop(out value.asInt);
		var lastFunction = pepper.byteCode.functions.buffer[pepper.byteCode.functions.count - 1];
		type = pepper.byteCode.functionTypes.buffer[lastFunction.typeIndex].returnType;

		return null;
	}
}