using System.Collections.Generic;

public readonly struct CompileError
{
	public readonly Slice slice;
	public readonly string message;

	public CompileError(Slice slice, string message)
	{
		this.slice = slice;
		this.message = message;
	}
}

public struct LocalVariable
{
	public Slice slice;
	public int stackIndex;
	public int depth;
	public ValueType type;
	public bool isMutable;
	public bool isUsed;

	public LocalVariable(Slice slice, int stackIndex, int depth, ValueType type, bool isMutable, bool isUsed)
	{
		this.slice = slice;
		this.stackIndex = stackIndex;
		this.depth = depth;
		this.type = type;
		this.isMutable = isMutable;
		this.isUsed = isUsed;
	}
}

public sealed class Compiler
{
	public readonly struct Scope
	{
		public readonly int localVarStartIndex;

		public Scope(int localVarStartIndex)
		{
			this.localVarStartIndex = localVarStartIndex;
		}
	}

	public readonly List<CompileError> errors = new List<CompileError>();
	public Parser parser;
	public ParseFunction onParseWithPrecedence;

	public bool isInPanicMode;
	public ByteCodeChunk chunk;
	public Buffer<ValueType> typeStack = new Buffer<ValueType>(256);
	public Buffer<LocalVariable> localVariables = new Buffer<LocalVariable>(256);
	public int scopeDepth;

	public void Reset(Parser parser, ParseFunction onParseWithPrecedence)
	{
		errors.Clear();
		this.parser = parser;
		this.onParseWithPrecedence = onParseWithPrecedence;

		isInPanicMode = false;
		chunk = new ByteCodeChunk();
		typeStack.count = 0;
		localVariables.count = 0;
		scopeDepth = 0;
	}

	public Compiler AddSoftError(Slice slice, string format, params object[] args)
	{
		if (!isInPanicMode)
			errors.Add(new CompileError(slice, string.Format(format, args)));
		return this;
	}

	public Compiler AddHardError(Slice slice, string format, params object[] args)
	{
		if (!isInPanicMode)
		{
			isInPanicMode = true;
			if (args == null || args.Length == 0)
				errors.Add(new CompileError(slice, format));
			else
				errors.Add(new CompileError(slice, string.Format(format, args)));
		}
		return this;
	}

	public Scope BeginScope()
	{
		scopeDepth += 1;
		return new Scope(localVariables.count);
	}

	public void EndScope(Scope scope)
	{
		scopeDepth -= 1;

		for (var i = scope.localVarStartIndex; i < localVariables.count; i++)
		{
			var variable = localVariables.buffer[i];
			if (!variable.isUsed)
				AddSoftError(variable.slice, "Unused variable");
		}

		var localCount = localVariables.count - scope.localVarStartIndex;
		if (localCount > 0)
		{
			this.EmitInstruction(Instruction.PopMultiple);
			this.EmitByte((byte)localCount);

			localVariables.count -= localCount;
			typeStack.count -= localCount;
		}
	}

	public ByteCodeChunk.FunctionTypeBuilder BeginFunctionDeclaration()
	{
		return chunk.BeginAddFunctionType();
	}

	public void EndFunctionDeclaration(ByteCodeChunk.FunctionTypeBuilder builder, Slice slice)
	{
		if (chunk.functions.count >= ushort.MaxValue)
		{
			chunk.functionTypeParams.count -= builder.parameterCount;
			AddSoftError(slice, "Too many function declarations");
			return;
		}

		var typeIndex = chunk.EndAddFunctionType(builder);
		var name = CompilerHelper.GetSlice(this, slice);
		chunk.AddFunction(name, typeIndex);
	}

	public ByteCodeChunk.StructTypeBuilder BeginStructDeclaration()
	{
		return chunk.BeginAddStructType();
	}

	public void EndStructDeclaration(ByteCodeChunk.StructTypeBuilder builder, Slice slice)
	{
		if (chunk.structTypes.count >= ushort.MaxValue)
		{
			chunk.structTypes.count -= builder.fieldCount;
			AddSoftError(slice, "Too many struct declarations");
			return;
		}

		var name = CompilerHelper.GetSlice(this, slice);

		for (var i = 0; i < chunk.structTypes.count; i++)
		{
			if (chunk.structTypes.buffer[i].name == name)
			{
				chunk.structTypes.count -= builder.fieldCount;
				AddSoftError(slice, "There's already a struct with this name");
				return;
			}
		}

		chunk.EndAddStructType(builder, name);
	}

	public int ResolveToFunctionIndex()
	{
		var source = parser.tokenizer.source;

		for (var i = 0; i < chunk.functions.count; i++)
		{
			var f = chunk.functions.buffer[i];
			if (CompilerHelper.AreEqual(source, parser.previousToken.slice, f.name))
				return i;
		}

		return -1;
	}

	public int AddLocalVariable(Slice slice, ValueType type, bool mutable, bool isUsed)
	{
		var stackIndex = 0;
		if (localVariables.count > 0)
		{
			var lastVar = localVariables.buffer[localVariables.count - 1];
			stackIndex = lastVar.stackIndex + chunk.GetTypeSize(lastVar.type);
		}

		localVariables.PushBack(new LocalVariable(
			slice,
			stackIndex,
			scopeDepth,
			type,
			mutable,
			isUsed
		));

		return localVariables.count - 1;
	}

	public int DeclareLocalVariable(Slice slice, bool mutable)
	{
		if (localVariables.count >= localVariables.buffer.Length)
		{
			AddSoftError(slice, "Too many local variables in function");
			return -1;
		}

		var type = ValueType.Unit;
		if (typeStack.count > 0)
			type = typeStack.buffer[typeStack.count - 1];

		return AddLocalVariable(slice, type, mutable, false);
	}

	public int ResolveToLocalVariableIndex()
	{
		var source = parser.tokenizer.source;

		for (var i = 0; i < localVariables.count; i++)
		{
			var local = localVariables.buffer[i];
			if (CompilerHelper.AreEqual(source, parser.previousToken.slice, local.slice))
				return i;
		}

		return -1;
	}
}