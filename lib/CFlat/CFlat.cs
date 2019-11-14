[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("test")]

public sealed class CFlat
{
	internal readonly struct Source
	{
		public readonly string name;
		public readonly string content;

		public Source(string name, string content)
		{
			this.name = name;
			this.content = content;
		}
	}

	internal readonly VirtualMachine vm = new VirtualMachine();
	internal readonly Compiler compiler = new Compiler();
	internal ByteCodeChunk chunk = new ByteCodeChunk();
	internal Buffer<Source> sources = new Buffer<Source>();
	internal Buffer<CompileError> compileErrors = new Buffer<CompileError>();

	public Buffer<CompileError> CompileSource(string sourceName, string source, Mode mode)
	{
		if (compileErrors.count > 0)
			return compileErrors;

		sources.PushBack(new Source(sourceName, source));
		chunk.sourceStartIndexes.PushBack(chunk.bytes.count);

		var errors = compiler.Compile(chunk, mode, source, sources.count - 1);
		if (errors.count > 0)
			compileErrors = errors;
		return errors;
	}

	public Buffer<CompileError> CompileExpression(string source, Mode mode)
	{
		if (compileErrors.count > 0)
			return compileErrors;

		sources.PushBack(new Source("expression", source));
		chunk.sourceStartIndexes.PushBack(chunk.bytes.count);

		var errors = compiler.CompileExpression(chunk, mode, source, sources.count - 1);
		if (errors.count > 0)
			compileErrors = errors;
		return errors;
	}

	public bool Load()
	{
		if (compileErrors.count > 0)
			return false;

		vm.Load(chunk);
		return true;
	}

	public void AddDebugHook(DebugHookCallback callback)
	{
		vm.debugHookCallback += callback;
	}

	public void RemoveDebugHook(DebugHookCallback callback)
	{
		vm.debugHookCallback -= callback;
	}

	public Option<RuntimeError> GetError()
	{
		return vm.error;
	}
}
