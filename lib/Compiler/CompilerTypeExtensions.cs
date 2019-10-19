public static class CompilerTypeExtensions
{
	public static bool CheckFunctionBuild(this Compiler self, FunctionTypeBuilder.Result result, Slice slice)
	{
		switch (result)
		{
		case FunctionTypeBuilder.Result.Success:
			return true;
		case FunctionTypeBuilder.Result.TooManyFunctions:
			self.AddSoftError(slice, "Too many function declarations");
			return false;
		case FunctionTypeBuilder.Result.ParametersTooBig:
			self.AddSoftError(
				slice,
				"Function parameters size is too big. Max is {0}",
				byte.MaxValue
			);
			return false;
		default:
			return false;
		}
	}

	public static bool CheckTupleBuild(this Compiler self, TupleTypeBuilder.Result result, Slice slice)
	{
		switch (result)
		{
		case TupleTypeBuilder.Result.Success:
			return true;
		case TupleTypeBuilder.Result.TooManyTuples:
			self.AddSoftError(slice, "Too many tuple declarations");
			return false;
		case TupleTypeBuilder.Result.ElementsTooBig:
			self.AddSoftError(
				slice,
				"Tuple elements size is too big. Max is {0}",
				byte.MaxValue
			);
			return false;
		default:
			return false;
		}
	}

	public static bool CheckStructBuild(this Compiler self, StructTypeBuilder.Result result, Slice slice, string name)
	{
		switch (result)
		{
		case StructTypeBuilder.Result.Success:
			return true;
		case StructTypeBuilder.Result.TooManyStructs:
			self.AddSoftError(slice, "Too many struct declarations");
			return false;
		case StructTypeBuilder.Result.DuplicatedName:
			self.AddSoftError(slice, "There's already a struct named '{0}'", name);
			return false;
		default:
			return false;
		}
	}

	public static ValueType ParseType(this Compiler self, string error)
	{
		var slice = self.parser.currentToken.slice;
		return self.ParseTypeRecursive(error, slice, 0);
	}

	private static ValueType ParseTypeRecursive(this Compiler self, string error, Slice slice, int recursionLevel)
	{
		if (recursionLevel > 8)
		{
			self.AddSoftError(slice, "Type is nested too deeply");
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
		else if (self.parser.Match(TokenKind.OpenCurlyBrackets))
			type = self.ParseTupleType(slice, recursionLevel + 1);
		else if (self.parser.Match(TokenKind.Identifier))
			type = self.ParseStructOrClassType(recursionLevel + 1);
		else if (self.parser.Match(TokenKind.Function))
			type = self.ParseFunctionType(slice, recursionLevel + 1);
		else if (self.parser.Match(TokenKind.OpenSquareBrackets))
			type = self.ParseArrayType(slice, recursionLevel + 1);
		else if (self.parser.Match(TokenKind.Ampersand))
			type = self.ParseReferenceType(slice, recursionLevel + 1);

		if (type.isSome)
			return type.value;

		slice = Slice.FromTo(slice, self.parser.previousToken.slice);

		self.AddSoftError(slice, error);
		return new ValueType(TypeKind.Unit);
	}

	private static Option<ValueType> ParseTupleType(this Compiler self, Slice slice, int recursionLevel)
	{
		var source = self.parser.tokenizer.source;
		var builder = self.chunk.BeginTupleType();
		var elementStartIndex = self.chunk.tupleElementTypes.count;

		while (
			!self.parser.Check(TokenKind.CloseCurlyBrackets) &&
			!self.parser.Check(TokenKind.End)
		)
		{
			slice = Slice.FromTo(slice, self.parser.previousToken.slice);
			var elementType = self.ParseTypeRecursive("Expected tuple element type", slice, recursionLevel);
			if (!self.parser.Check(TokenKind.CloseCurlyBrackets))
				self.parser.Consume(TokenKind.Comma, "Expected ',' after element type");
			builder.WithElement(elementType);
		}
		self.parser.Consume(TokenKind.CloseCurlyBrackets, "Expected '}' after tuple elements");

		slice = Slice.FromTo(slice, self.parser.previousToken.slice);

		var result = builder.Build(out var tupleTypeIndex);
		if (!self.CheckTupleBuild(result, slice))
			return Option.None;

		var type = new ValueType(TypeKind.Tuple, tupleTypeIndex);
		return Option.Some(type);
	}

	private static Option<ValueType> ParseStructOrClassType(this Compiler self, int recursionLevel)
	{
		var source = self.parser.tokenizer.source;
		var slice = self.parser.previousToken.slice;

		for (var i = 0; i < self.chunk.structTypes.count; i++)
		{
			var structName = self.chunk.structTypes.buffer[i].name;
			if (CompilerHelper.AreEqual(source, slice, structName))
				return Option.Some(new ValueType(TypeKind.Struct, i));
		}

		for (var i = 0; i < self.chunk.nativeClassTypes.count; i++)
		{
			var className = self.chunk.nativeClassTypes.buffer[i].name;
			if (CompilerHelper.AreEqual(source, slice, className))
				return Option.Some(new ValueType(TypeKind.NativeClass, i));
		}

		return Option.None;
	}

	private static Option<ValueType> ParseFunctionType(this Compiler self, Slice slice, int recursionLevel)
	{
		var builder = self.chunk.BeginFunctionType();

		self.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function type");
		if (!self.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				slice = Slice.FromTo(slice, self.parser.previousToken.slice);
				var paramType = self.ParseTypeRecursive("Expected function parameter type", slice, recursionLevel);
				builder.WithParam(paramType);
			} while (self.parser.Match(TokenKind.Comma));
		}
		self.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function type parameter list");
		if (self.parser.Match(TokenKind.Colon))
		{
			slice = Slice.FromTo(slice, self.parser.previousToken.slice);
			builder.returnType = self.ParseTypeRecursive("Expected function return type", slice, recursionLevel);
		}

		slice = Slice.FromTo(slice, self.parser.previousToken.slice);

		var result = builder.Build(out var typeIndex);
		if (!self.CheckFunctionBuild(result, slice))
			return Option.None;

		var type = new ValueType(TypeKind.Function, typeIndex);
		return Option.Some(type);
	}

	private static Option<ValueType> ParseArrayType(this Compiler self, Slice slice, int recursionLevel)
	{
		slice = Slice.FromTo(slice, self.parser.previousToken.slice);
		var elementType = self.ParseTypeRecursive("Expected array element type", slice, recursionLevel);
		self.parser.Consume(TokenKind.CloseSquareBrackets, "Expected ']' after array type");
		slice = Slice.FromTo(slice, self.parser.previousToken.slice);

		if (elementType.IsArray)
		{
			self.AddSoftError(slice, "Can not declare array of arrays");
			return Option.None;
		}

		return Option.Some(elementType.ToArrayType());
	}

	private static Option<ValueType> ParseReferenceType(this Compiler self, Slice slice, int recursionLevel)
	{
		var isMutable = self.parser.Match(TokenKind.Mut);

		slice = Slice.FromTo(slice, self.parser.previousToken.slice);
		var referredType = self.ParseTypeRecursive("Expected referred type", slice, recursionLevel);
		if (referredType.IsReference)
		{
			self.AddSoftError(slice, "Can not declare reference of reference");
			return Option.None;
		}

		return Option.Some(referredType.ToReferenceType(isMutable));
	}
}