namespace cflat
{
	internal static class CompilerEmitExtensions
	{
		public static CompilerIO EmitByte(this CompilerIO self, byte value)
		{
			self.chunk.WriteByte(value, self.parser.previousToken.slice);
			return self;
		}

		public static CompilerIO EmitUShort(this CompilerIO self, ushort value)
		{
			BytesHelper.UShortToBytes(value, out var b0, out var b1);
			self.chunk.WriteByte(b0, self.parser.previousToken.slice);
			self.chunk.WriteByte(b1, self.parser.previousToken.slice);
			return self;
		}

		public static CompilerIO EmitInstruction(this CompilerIO self, Instruction instruction)
		{
			if (self.mode == Mode.Debug && instruction < Instruction.DebugHook)
				self.EmitByte((byte)Instruction.DebugHook);
			return self.EmitByte((byte)instruction);
		}

		public static CompilerIO EmitPop(this CompilerIO self, int size)
		{
			if (size > 1)
			{
				self.EmitInstruction(Instruction.PopMultiple);
				self.EmitByte((byte)size);
			}
			else if (size == 1)
			{
				self.EmitInstruction(Instruction.Pop);
			}

			return self;
		}

		public static CompilerIO EmitLoadLiteral(this CompilerIO self, ValueData value, TypeKind type)
		{
			var index = self.chunk.AddValueLiteral(value, type);
			self.EmitInstruction(Instruction.LoadLiteral);
			return self.EmitUShort((ushort)index);
		}

		public static CompilerIO EmitLoadStringLiteral(this CompilerIO self, string value)
		{
			var index = self.chunk.AddStringLiteral(value);
			self.EmitInstruction(Instruction.LoadLiteral);
			return self.EmitUShort((ushort)index);
		}

		public static CompilerIO EmitSetLocal(this CompilerIO self, int stackIndex, ValueType type)
		{
			var typeSize = type.GetSize(self.chunk);
			if (typeSize > 1)
			{
				self.EmitInstruction(Instruction.SetLocalMultiple);
				self.EmitByte((byte)stackIndex);
				return self.EmitByte(typeSize);
			}
			else
			{
				self.EmitInstruction(Instruction.SetLocal);
				return self.EmitByte((byte)stackIndex);
			}
		}

		public static CompilerIO EmitLoadLocal(this CompilerIO self, int stackIndex, ValueType type)
		{
			var typeSize = type.GetSize(self.chunk);
			if (typeSize > 1)
			{
				self.EmitInstruction(Instruction.LoadLocalMultiple);
				self.EmitByte((byte)stackIndex);
				return self.EmitByte(typeSize);
			}
			else
			{
				self.EmitInstruction(Instruction.LoadLocal);
				return self.EmitByte((byte)stackIndex);
			}
		}

		public static CompilerIO EmitLoadFunction(this CompilerIO self, Instruction instruction, int functionIndex)
		{
			self.EmitInstruction(instruction);
			return self.EmitUShort((ushort)functionIndex);
		}

		public static int BeginEmitBackwardJump(this CompilerIO self)
		{
			return self.chunk.bytes.count;
		}

		public static void EndEmitBackwardJump(this CompilerIO self, Instruction instruction, int jumpIndex)
		{
			self.EmitInstruction(instruction);

			var offset = self.chunk.bytes.count - jumpIndex + 2;
			if (offset > ushort.MaxValue)
			{
				self.AddSoftError(self.parser.previousToken.slice, "Too much code to jump over");
				return;
			}

			self.EmitUShort((ushort)offset);
		}

		public static int BeginEmitForwardJump(this CompilerIO self, Instruction instruction)
		{
			self.EmitInstruction(instruction);
			self.EmitUShort(0);

			return self.chunk.bytes.count - 2;
		}

		public static void EndEmitForwardJump(this CompilerIO self, int jumpIndex)
		{
			var offset = self.chunk.bytes.count - jumpIndex - 2;
			if (offset > ushort.MaxValue)
				self.AddSoftError(self.parser.previousToken.slice, "Too much code to jump over");

			BytesHelper.UShortToBytes(
				(ushort)offset,
				out self.chunk.bytes.buffer[jumpIndex],
				out self.chunk.bytes.buffer[jumpIndex + 1]
			);
		}

		public static void EmitType(this CompilerIO self, ValueType type)
		{
			type.Write(out var b0, out var b1, out var b2, out var b3);
			self.EmitByte(b0);
			self.EmitByte(b1);
			self.EmitByte(b2);
			self.EmitByte(b3);
		}
	}
}