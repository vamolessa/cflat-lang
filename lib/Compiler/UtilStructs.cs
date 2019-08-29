public enum Mode
{
	Release,
	Debug
}

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

public readonly struct Scope
{
	public readonly int localVariablesStartIndex;

	public Scope(int localVarStartIndex)
	{
		this.localVariablesStartIndex = localVarStartIndex;
	}
}

public readonly struct LoopBreak
{
	public readonly int jump;
	public readonly byte nesting;

	public LoopBreak(int jump, byte nesting)
	{
		this.jump = jump;
		this.nesting = nesting;
	}
}