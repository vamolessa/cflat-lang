public static class CompilerTypeExtensions
{
	public static ValueType ConsumeType(this Compiler self, string error, int recursionLevel)
	{
		if (recursionLevel > 8)
		{
			self.AddSoftError(self.parser.previousToken.slice, "Type is nested too deeply");
			return ValueType.Unit;
		}

		var type = new Option<ValueType>();

		if (self.parser.Match(TokenKind.Bool))
			type = Option.Some(ValueType.Bool);
		else if (self.parser.Match(TokenKind.Int))
			type = Option.Some(ValueType.Int);
		else if (self.parser.Match(TokenKind.Float))
			type = Option.Some(ValueType.Float);
		else if (self.parser.Match(TokenKind.String))
			type = Option.Some(ValueType.String);
		else if (self.parser.Match(TokenKind.Identifier))
			type = self.ResolveStructType(recursionLevel + 1);
		else if (self.parser.Match(TokenKind.Function))
			type = self.ResolveFunctionType(recursionLevel + 1);

		if (type.isSome)
			return type.value;

		self.AddSoftError(self.parser.previousToken.slice, error);
		return ValueType.Unit;
	}

	private static Option<ValueType> ResolveStructType(this Compiler self, int recursionLevel)
	{
		var source = self.parser.tokenizer.source;
		var slice = self.parser.previousToken.slice;

		for (var i = 0; i < self.chunk.structTypes.count; i++)
		{
			var structName = self.chunk.structTypes.buffer[i].name;
			if (CompilerHelper.AreEqual(source, slice, structName))
				return Option.Some(ValueTypeHelper.SetIndex(ValueType.Struct, i));
		}

		return Option.None;
	}

	private static Option<ValueType> ResolveFunctionType(this Compiler self, int recursionLevel)
	{
		var declaration = self.chunk.BeginAddFunctionType();

		self.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function type");
		if (!self.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				var paramType = self.ConsumeType("Expected function parameter type", recursionLevel);
				declaration.AddParam(paramType);
			} while (self.parser.Match(TokenKind.Comma));
		}
		self.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function type parameter list");
		if (self.parser.Match(TokenKind.Colon))
			declaration.returnType = self.ConsumeType("Expected function return type", recursionLevel);

		var functionTypeIndex = self.chunk.EndAddFunctionType(declaration);
		var type = ValueTypeHelper.SetIndex(ValueType.Function, functionTypeIndex);

		return Option.Some(type);
	}
}