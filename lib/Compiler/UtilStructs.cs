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

[System.Flags]
public enum VariableFlags : byte
{
	None = 0b0000,
	Iteration = 0b0001,
	Mutable = 0b0010,
	Used = 0b0100,
	Changed = 0b1000,
}

public struct LocalVariable
{
	public Slice slice;
	public int depth;
	public ValueType type;
	public VariableFlags flags;
	public byte stackIndex;

	public bool IsIteration
	{
		get { return (flags & VariableFlags.Iteration) != 0; }
	}

	public bool IsMutable
	{
		get { return (flags & VariableFlags.Mutable) != 0; }
	}

	public bool IsUsed
	{
		get { return (flags & VariableFlags.Used) != 0; }
	}

	public bool IsChanged
	{
		get { return (flags & VariableFlags.Changed) != 0; }
	}

	public LocalVariable(Slice slice, byte stackIndex, int depth, ValueType type, VariableFlags flags)
	{
		this.slice = slice;
		this.stackIndex = stackIndex;
		this.depth = depth;
		this.type = type;
		this.flags = flags;
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