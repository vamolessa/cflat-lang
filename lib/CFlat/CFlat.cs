[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("test")]

public enum Mode
{
	Release,
	Debug
}

public readonly struct Source
{
	public readonly string uri;
	public readonly string content;

	public Source(string uri, string content)
	{
		this.uri = uri;
		this.content = content;
	}
}

public interface IModuleResolver
{
	Option<string> ResolveModuleUri(string requestingSourceUri, string modulePath);
	Option<string> ResolveModuleSource(string requestingSourceUri, string moduleUri);
}

public sealed class CFlat
{
	internal readonly VirtualMachine vm = new VirtualMachine();
	internal readonly Compiler compiler = new Compiler();
	internal ByteCodeChunk chunk = new ByteCodeChunk();
	internal Buffer<CompileError> compileErrors = new Buffer<CompileError>();

	public Buffer<CompileError> CompileSource(string sourceName, string source, Mode mode, Option<IModuleResolver> moduleResolver)
	{
		if (compileErrors.count > 0)
			return compileErrors;

		chunk.sourceStartIndexes.PushBack(chunk.bytes.count);

		var errors = compiler.CompileSource(chunk, moduleResolver, mode, new Source(sourceName, source));
		if (errors.count > 0)
			compileErrors = errors;
		else
			vm.Load(chunk);
		return errors;
	}

	public Buffer<CompileError> CompileExpression(string source, Mode mode)
	{
		if (compileErrors.count > 0)
			return compileErrors;

		chunk.sourceStartIndexes.PushBack(chunk.bytes.count);

		var errors = compiler.CompileExpression(chunk, mode, new Source("expression", source));
		if (errors.count > 0)
			compileErrors = errors;
		else
			vm.Load(chunk);
		return errors;
	}

	public void AddDebugHook(DebugHookCallback callback)
	{
		vm.debugHookCallback += callback;
	}

	public void RemoveDebugHook(DebugHookCallback callback)
	{
		vm.debugHookCallback -= callback;
	}

	public Option<RuntimeError> GetRuntimeError()
	{
		return vm.error;
	}
}
