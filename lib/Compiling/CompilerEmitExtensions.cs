public static class CompilerEmitExtensions
{
	public static Compiler EmitByte(this Compiler self, byte value)
	{
		self.chunk.WriteByte(value, self.parser.previousToken.slice);
		return self;
	}

	public static Compiler EmitInstruction(this Compiler self, Instruction instruction)
	{
		return self.EmitByte((byte)instruction);
	}

	public static Compiler EmitLoadLiteral(this Compiler self, ValueData value, ValueKind type)
	{
		var index = self.chunk.AddValueLiteral(value, type);
		self.EmitInstruction(Instruction.LoadLiteral);
		return self.EmitByte((byte)index);
	}

	public static Compiler EmitLoadFunction(this Compiler self, Instruction instruction, int functionIndex)
	{
		self.EmitInstruction(instruction);
		BytesHelper.ShortToBytes((ushort)functionIndex, out var b0, out var b1);
		self.EmitByte(b0);
		return self.EmitByte(b1);
	}

	public static Compiler EmitConvertToStruct(this Compiler self, int structTypeIndex)
	{
		self.EmitInstruction(Instruction.ConvertToStruct);
		BytesHelper.ShortToBytes((ushort)structTypeIndex, out var b0, out var b1);
		self.EmitByte(b0);
		return self.EmitByte(b1);
	}

	public static Compiler EmitLoadStringLiteral(this Compiler self, string value)
	{
		var index = self.chunk.AddStringLiteral(value);
		self.EmitInstruction(Instruction.LoadLiteral);
		return self.EmitByte((byte)index);
	}

	public static int BeginEmitBackwardJump(this Compiler self)
	{
		return self.chunk.bytes.count;
	}

	public static void EndEmitBackwardJump(this Compiler self, Instruction instruction, int jumpIndex)
	{
		self.EmitInstruction(instruction);

		var offset = self.chunk.bytes.count - jumpIndex + 2;
		if (offset > ushort.MaxValue)
		{
			self.AddSoftError(self.parser.previousToken.slice, "Too much code to jump over");
			return;
		}

		BytesHelper.ShortToBytes((ushort)offset, out var b0, out var b1);
		self.EmitByte(b0);
		self.EmitByte(b1);
	}

	public static int BeginEmitForwardJump(this Compiler self, Instruction instruction)
	{
		self.EmitInstruction(instruction);
		self.EmitByte(0);
		self.EmitByte(0);

		return self.chunk.bytes.count - 2;
	}

	public static void EndEmitForwardJump(this Compiler self, int jumpIndex)
	{
		var offset = self.chunk.bytes.count - jumpIndex - 2;
		if (offset > ushort.MaxValue)
			self.AddSoftError(self.parser.previousToken.slice, "Too much code to jump over");

		BytesHelper.ShortToBytes(
			(ushort)offset,
			out self.chunk.bytes.buffer[jumpIndex],
			out self.chunk.bytes.buffer[jumpIndex + 1]
		);
	}
}