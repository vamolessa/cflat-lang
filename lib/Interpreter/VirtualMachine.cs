using System.Collections.Generic;
using System.Text;

public readonly struct RuntimeError
{
	public readonly int instructionIndex;
	public readonly Slice slice;
	public readonly string message;

	public RuntimeError(int instructionIndex, Slice slice, string message)
	{
		this.instructionIndex = instructionIndex;
		this.slice = slice;
		this.message = message;
	}
}

internal struct CallFrame
{
	public int functionIndex;
	public int codeIndex;
	public int baseStackIndex;

	public CallFrame(int functionIndex, int codeIndex, int baseStackIndex)
	{
		this.functionIndex = functionIndex;
		this.codeIndex = codeIndex;
		this.baseStackIndex = baseStackIndex;
	}
}

internal readonly struct ReflectionData
{
	public readonly ValueType type;
	public readonly byte size;

	public ReflectionData(ValueType type, byte size)
	{
		this.type = type;
		this.size = size;
	}
}

public sealed class VirtualMachine
{
	public bool debugMode = false;

	internal ByteCodeChunk chunk;
	internal Buffer<ValueData> valueStack = new Buffer<ValueData>(256);
	internal Buffer<CallFrame> callframeStack = new Buffer<CallFrame>(64);
	internal Buffer<object> heap;
	internal Option<RuntimeError> error;

	internal Dictionary<System.Type, ReflectionData> reflectionData = new Dictionary<System.Type, ReflectionData>();

	public void Load(ByteCodeChunk chunk)
	{
		this.chunk = chunk;
		error = Option.None;

		valueStack.count = 0;
		callframeStack.count = 0;

		heap = new Buffer<object>
		{
			buffer = new object[chunk.stringLiterals.buffer.Length],
			count = chunk.stringLiterals.count
		};
		for (var i = 0; i < heap.count; i++)
			heap.buffer[i] = chunk.stringLiterals.buffer[i];
	}

	public FunctionCall CallFunction(string functionName)
	{
		for (var i = 0; i < chunk.functions.count; i++)
		{
			var function = chunk.functions.buffer[i];
			if (function.name == functionName)
			{
				valueStack.PushBack(new ValueData(i));
				callframeStack.PushBack(new CallFrame(
					-1,
					chunk.bytes.count - 1,
					valueStack.count
				));
				callframeStack.PushBack(new CallFrame(
					i,
					function.codeIndex,
					valueStack.count
				));

				return new FunctionCall(this, (ushort)i);
			}
		}

		Error(string.Format("Could not find function named '{0}'", functionName));
		return new FunctionCall(this, ushort.MaxValue);
	}

	public void CallTopFunction()
	{
		if (debugMode)
		{
			var sb = new StringBuilder();
			do
			{
				var ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;
				sb.Clear();
				VirtualMachineHelper.TraceStack(this, sb);
				chunk.DisassembleInstruction(ip, sb);
				System.Console.WriteLine(sb);
			} while (VirtualMachineInstructions.Tick(this));
			sb.Clear();
			VirtualMachineHelper.TraceStack(this, sb);
			System.Console.WriteLine(sb);
		}
		else
		{
			while (VirtualMachineInstructions.Tick(this)) { }
		}
	}

	public void Error(string message)
	{
		var ip = -1;
		if (callframeStack.count > 0)
			ip = callframeStack.buffer[callframeStack.count - 1].codeIndex;

		error = Option.Some(new RuntimeError(
			ip,
			ip >= 0 ? chunk.slices.buffer[ip] : new Slice(),
			message
		));
	}
}