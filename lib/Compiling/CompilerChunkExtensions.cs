public static class CompilerChunkExtensions
{
	public static Compiler EmitByte(this Compiler compiler, byte value)
	{
		compiler.chunk.WriteByte(value, compiler.previousToken.slice);
		return compiler;
	}

	public static Compiler EmitInstruction(this Compiler compiler, Instruction instruction)
	{
		compiler.EmitByte((byte)instruction);
		return compiler;
	}

	public static Compiler EmitLoadLiteral(this Compiler compiler, ValueData value, ValueType type)
	{
		var index = compiler.chunk.AddValueLiteral(value, type);
		compiler.EmitInstruction(Instruction.LoadLiteral);
		compiler.EmitByte((byte)index);

		return compiler;
	}

	public static Compiler EmitLoadFunction(this Compiler compiler, int functionIndex)
	{
		compiler.EmitInstruction(Instruction.LoadFunction);
		BytesHelper.ShortToBytes((ushort)functionIndex, out var b0, out var b1);
		compiler.EmitByte(b0);
		compiler.EmitByte(b1);

		return compiler;
	}

	public static Compiler EmitLoadStringLiteral(this Compiler compiler, string value)
	{
		var index = compiler.chunk.AddStringLiteral(value);
		compiler.EmitInstruction(Instruction.LoadLiteral);
		compiler.EmitByte((byte)index);

		return compiler;
	}

	public static int BeginEmitBackwardJump(this Compiler compiler)
	{
		return compiler.chunk.bytes.count;
	}

	public static void EndEmitBackwardJump(this Compiler compiler, Instruction instruction, int jumpIndex)
	{
		compiler.EmitInstruction(instruction);

		var offset = compiler.chunk.bytes.count - jumpIndex + 2;
		if (offset > ushort.MaxValue)
		{
			compiler.AddSoftError(compiler.previousToken.slice, "Too much code to jump over");
			return;
		}

		BytesHelper.ShortToBytes((ushort)offset, out var b0, out var b1);
		compiler.EmitByte(b0);
		compiler.EmitByte(b1);
	}

	public static int BeginEmitForwardJump(this Compiler compiler, Instruction instruction)
	{
		compiler.EmitInstruction(instruction);
		compiler.EmitByte(0);
		compiler.EmitByte(0);

		return compiler.chunk.bytes.count - 2;
	}

	public static void EndEmitForwardJump(this Compiler compiler, int jumpIndex)
	{
		var offset = compiler.chunk.bytes.count - jumpIndex - 2;
		if (offset > ushort.MaxValue)
			compiler.AddSoftError(compiler.previousToken.slice, "Too much code to jump over");

		BytesHelper.ShortToBytes(
			(ushort)offset,
			out compiler.chunk.bytes.buffer[jumpIndex],
			out compiler.chunk.bytes.buffer[jumpIndex + 1]
		);
	}
}