public static class CompilerHelper
{
	public static void EmitByte(this ByteCodeChunk chunk, Parser parser, byte value)
	{
		chunk.WriteByte(value, parser.previousToken.index);
	}

	public static void EmitInstruction(this ByteCodeChunk chunk, Parser parser, Instruction instruction)
	{
		EmitByte(chunk, parser, (byte)instruction);
	}

	public static void EmitLoadConstant(this ByteCodeChunk chunk, Parser parser, Value value)
	{
		var index = System.Array.IndexOf(chunk.constants.buffer, value);
		if (index < 0)
			index = chunk.AddConstant(value);

		EmitInstruction(chunk, parser, Instruction.LoadConstant);
		EmitByte(chunk, parser, (byte)index);
	}
}