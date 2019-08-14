public static class CompilerTypeExtensions
{
	public static ValueType ParseType(this Compiler self, string error, int recursionLevel)
	{
		if (recursionLevel > 8)
		{
			self.AddSoftError(self.parser.previousToken.slice, "Type is nested too deeply");
			return new ValueType(TypeKind.Unit);
		}

		var type = new Option<ValueType>();

		if (self.parser.Match(TokenKind.Bool))
			type = Option.Some(new ValueType(TypeKind.Bool));
		else if (self.parser.Match(TokenKind.Int))
			type = Option.Some(new ValueType(TypeKind.Int));
		else if (self.parser.Match(TokenKind.Float))
			type = Option.Some(new ValueType(TypeKind.Float));
		else if (self.parser.Match(TokenKind.String))
			type = Option.Some(new ValueType(TypeKind.String));
		else if (self.parser.Match(TokenKind.Struct))
			type = self.ParseAnonymousStructType(recursionLevel + 1);
		else if (self.parser.Match(TokenKind.Identifier))
			type = self.ParseStructType(recursionLevel + 1);
		else if (self.parser.Match(TokenKind.Function))
			type = self.ParseFunctionType(recursionLevel + 1);

		if (type.isSome)
			return type.value;

		self.AddSoftError(self.parser.previousToken.slice, error);
		return new ValueType(TypeKind.Unit);
	}

	private static Option<ValueType> ParseAnonymousStructType(this Compiler self, int recursionLevel)
	{
		var source = self.parser.tokenizer.source;
		var builder = self.chunk.BeginStructType();
		var fieldStartIndex = self.chunk.structTypeFields.count;

		self.parser.Consume(TokenKind.OpenCurlyBrackets, "Expected '{' after anonymous struct type");
		while (
			!self.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!self.parser.Check(TokenKind.End)
		)
		{
			self.parser.Consume(TokenKind.Identifier, "Expected field name");
			var fieldSlice = self.parser.previousToken.slice;
			self.parser.Consume(TokenKind.Colon, "Expected ':' after field name");
			var fieldType = self.ParseType("Expected field type", 0);

			var hasDuplicate = false;
			for (var i = 0; i < builder.fieldCount; i++)
			{
				var otherName = self.chunk.structTypeFields.buffer[fieldStartIndex + i].name;
				if (CompilerHelper.AreEqual(source, fieldSlice, otherName))
				{
					hasDuplicate = true;
					break;
				}
			}

			var fieldName = CompilerHelper.GetSlice(self, fieldSlice);
			if (hasDuplicate)
			{
				self.AddSoftError(fieldSlice, "Struct already has a field named '{0}'", fieldName);
				continue;
			}

			builder.WithField(fieldName, fieldType);
		}
		self.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after anonymous struct fields");

		var structTypeIndex = builder.BuildAnonymous();
		var type = new ValueType(TypeKind.Struct, structTypeIndex);

		return Option.Some(type);
	}

	private static Option<ValueType> ParseStructType(this Compiler self, int recursionLevel)
	{
		var source = self.parser.tokenizer.source;
		var slice = self.parser.previousToken.slice;

		for (var i = 0; i < self.chunk.structTypes.count; i++)
		{
			var structName = self.chunk.structTypes.buffer[i].name;
			if (CompilerHelper.AreEqual(source, slice, structName))
				return Option.Some(new ValueType(TypeKind.Struct, i));
		}

		return Option.None;
	}

	private static Option<ValueType> ParseFunctionType(this Compiler self, int recursionLevel)
	{
		var builder = self.chunk.BeginFunctionType();

		self.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function type");
		if (!self.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				var paramType = self.ParseType("Expected function parameter type", recursionLevel);
				builder.WithParam(paramType);
			} while (self.parser.Match(TokenKind.Comma));
		}
		self.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function type parameter list");
		if (self.parser.Match(TokenKind.Colon))
			builder.returnType = self.ParseType("Expected function return type", recursionLevel);

		var functionTypeIndex = builder.Build();
		var type = new ValueType(TypeKind.Function, functionTypeIndex);

		return Option.Some(type);
	}
}