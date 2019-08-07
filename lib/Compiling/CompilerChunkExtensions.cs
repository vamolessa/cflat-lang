public static class CompilerChunkExtensions
{
	public static CompilerCommon EmitByte(this CompilerCommon compiler, byte value)
	{
		compiler.chunk.WriteByte(value, compiler.parser.previousToken.slice);
		return compiler;
	}

	public static CompilerCommon EmitInstruction(this CompilerCommon compiler, Instruction instruction)
	{
		compiler.EmitByte((byte)instruction);
		return compiler;
	}

	public static CompilerCommon EmitLoadLiteral(this CompilerCommon compiler, ValueData value, ValueType type)
	{
		var index = compiler.chunk.AddValueLiteral(value, type);
		compiler.EmitInstruction(Instruction.LoadLiteral);
		compiler.EmitByte((byte)index);

		return compiler;
	}

	public static CompilerCommon EmitLoadFunction(this CompilerCommon compiler, int functionIndex)
	{
		compiler.EmitInstruction(Instruction.LoadFunction);
		BytesHelper.ShortToBytes((ushort)functionIndex, out var b0, out var b1);
		compiler.EmitByte(b0);
		compiler.EmitByte(b1);

		return compiler;
	}

	public static CompilerCommon EmitLoadStringLiteral(this CompilerCommon compiler, string value)
	{
		var index = compiler.chunk.AddStringLiteral(value);
		compiler.EmitInstruction(Instruction.LoadLiteral);
		compiler.EmitByte((byte)index);

		return compiler;
	}

	public static int BeginEmitBackwardJump(this CompilerCommon compiler)
	{
		return compiler.chunk.bytes.count;
	}

	public static void EndEmitBackwardJump(this CompilerCommon compiler, Instruction instruction, int jumpIndex)
	{
		compiler.EmitInstruction(instruction);

		var offset = compiler.chunk.bytes.count - jumpIndex + 2;
		if (offset > ushort.MaxValue)
		{
			compiler.AddSoftError(compiler.parser.previousToken.slice, "Too much code to jump over");
			return;
		}

		BytesHelper.ShortToBytes((ushort)offset, out var b0, out var b1);
		compiler.EmitByte(b0);
		compiler.EmitByte(b1);
	}

	public static int BeginEmitForwardJump(this CompilerCommon compiler, Instruction instruction)
	{
		compiler.EmitInstruction(instruction);
		compiler.EmitByte(0);
		compiler.EmitByte(0);

		return compiler.chunk.bytes.count - 2;
	}

	public static void EndEmitForwardJump(this CompilerCommon compiler, int jumpIndex)
	{
		var offset = compiler.chunk.bytes.count - jumpIndex - 2;
		if (offset > ushort.MaxValue)
			compiler.AddSoftError(compiler.parser.previousToken.slice, "Too much code to jump over");

		BytesHelper.ShortToBytes(
			(ushort)offset,
			out compiler.chunk.bytes.buffer[jumpIndex],
			out compiler.chunk.bytes.buffer[jumpIndex + 1]
		);
	}
}