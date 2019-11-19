[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("test")]

namespace cflat
{
	public enum Mode
	{
		Release,
		Debug
	}

	public interface IModuleResolver
	{
		Option<string> ResolveModuleSource(Uri requestingSourceUri, Uri moduleUri);
	}

	public sealed class CFlat
	{
		internal readonly VirtualMachine vm = new VirtualMachine();
		internal readonly Compiler compiler = new Compiler();
		internal ByteCodeChunk chunk = new ByteCodeChunk();
		internal Buffer<CompileError> compileErrors = new Buffer<CompileError>();

		public void Reset()
		{
			chunk = new ByteCodeChunk();
			compileErrors.count = 0;
		}

		public Buffer<CompileError> CompileSource(Source source, Mode mode, Option<IModuleResolver> moduleResolver)
		{
			var errors = compiler.CompileSource(chunk, moduleResolver, mode, source);
			if (errors.count > 0)
				compileErrors = errors;
			else
				vm.Load(chunk);

			if (vm.debugger.isSome)
				vm.debugger.value.Reset(vm, compiler.compiledSources);

			return errors;
		}

		public Buffer<CompileError> CompileExpression(string expression, Mode mode)
		{
			var errors = compiler.CompileExpression(chunk, mode, new Source(new Uri("/"), expression));
			if (errors.count > 0)
				compileErrors = errors;
			else
				vm.Load(chunk);

			if (vm.debugger.isSome)
				vm.debugger.value.Reset(vm, compiler.compiledSources);

			return errors;
		}

		public void SetDebugger(Option<IDebugger> debugger)
		{
			vm.debugger = debugger;
		}

		public Option<RuntimeError> GetRuntimeError()
		{
			return vm.error;
		}
	}
}