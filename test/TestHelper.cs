public static class TestHelper
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

		pepper.virtualMachine.Pop(out value, out type);
		return null;
	}

	public static string RunExpression(string source, out ValueData value, out ValueType type)
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

		pepper.virtualMachine.Pop(out value, out type);
		return null;
	}
}