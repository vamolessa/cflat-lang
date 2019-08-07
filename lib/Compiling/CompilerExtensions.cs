public static class CompilerExtensions
{
	public static void ParseWithPrecedence(this Compiler compiler, Precedence precedence)
	{
		var parser = compiler.common.parser;
		parser.Next();
		if (parser.previousToken.kind == TokenKind.End)
			return;

		var prefixRule = compiler.parseRules[(int)parser.previousToken.kind].prefixRule;
		if (prefixRule == null)
		{
			compiler.common.AddHardError(parser.previousToken.slice, "Expected expression");
			return;
		}
		prefixRule(precedence);

		while (
			parser.currentToken.kind != TokenKind.End &&
			precedence <= compiler.parseRules[(int)parser.currentToken.kind].precedence
		)
		{
			parser.Next();
			var infixRule = compiler.parseRules[(int)parser.previousToken.kind].infixRule;
			infixRule(precedence);
		}

		var canAssign = precedence <= Precedence.Assignment;
		if (canAssign && compiler.common.parser.Match(TokenKind.Equal))
		{
			compiler.common.AddHardError(compiler.common.parser.previousToken.slice, "Invalid assignment target");
			compiler.Expression();
		}
	}

	public static void BeginLoop(this Compiler compiler)
	{
		compiler.loopNesting += 1;
	}

	public static void EndLoop(this Compiler compiler)
	{
		compiler.loopNesting -= 1;

		for (var i = compiler.loopBreaks.count - 1; i >= 0; i--)
		{
			var loopBreak = compiler.loopBreaks.buffer[i];
			if (loopBreak.nesting == compiler.loopNesting)
			{
				compiler.common.EndEmitForwardJump(loopBreak.jump);
				compiler.loopBreaks.SwapRemove(i);
			}
		}
	}

	public static bool BreakLoop(this Compiler compiler, int nesting, int jump)
	{
		if (compiler.loopNesting < nesting)
			return false;

		compiler.loopBreaks.PushBack(new LoopBreak(compiler.loopNesting - nesting, jump));
		return true;
	}

	public static ValueType ConsumeType(this Compiler compiler, string error, int recursionLevel)
	{
		if (recursionLevel > 8)
		{
			compiler.common.AddSoftError(compiler.common.parser.previousToken.slice, "Type is nested too deeply");
			return ValueType.Unit;
		}

		var type = new Option<ValueType>();

		if (compiler.common.parser.Match(TokenKind.Bool))
			type = Option.Some(ValueType.Bool);
		else if (compiler.common.parser.Match(TokenKind.Int))
			type = Option.Some(ValueType.Int);
		else if (compiler.common.parser.Match(TokenKind.Float))
			type = Option.Some(ValueType.Float);
		else if (compiler.common.parser.Match(TokenKind.String))
			type = Option.Some(ValueType.String);
		else if (compiler.common.parser.Match(TokenKind.Identifier))
			type = compiler.ResolveStructType(recursionLevel + 1);
		else if (compiler.common.parser.Match(TokenKind.Function))
			type = compiler.ResolveFunctionType(recursionLevel + 1);

		if (type.isSome)
			return type.value;

		compiler.common.AddSoftError(compiler.common.parser.previousToken.slice, error);
		return ValueType.Unit;
	}

	private static Option<ValueType> ResolveStructType(this Compiler compiler, int recursionLevel)
	{
		var source = compiler.common.parser.tokenizer.source;
		var slice = compiler.common.parser.previousToken.slice;

		for (var i = 0; i < compiler.common.chunk.structTypes.count; i++)
		{
			var structName = compiler.common.chunk.structTypes.buffer[i].name;
			if (CompilerHelper.AreEqual(source, slice, structName))
				return Option.Some(ValueTypeHelper.SetIndex(ValueType.String, i));
		}

		return Option.None;
	}

	private static Option<ValueType> ResolveFunctionType(this Compiler compiler, int recursionLevel)
	{
		var declaration = compiler.common.chunk.BeginAddFunctionType();

		compiler.common.parser.Consume(TokenKind.OpenParenthesis, "Expected '(' after function type");
		if (!compiler.common.parser.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				var paramType = compiler.ConsumeType("Expected function parameter type", recursionLevel);
				declaration.AddParam(paramType);
			} while (compiler.common.parser.Match(TokenKind.Comma));
		}
		compiler.common.parser.Consume(TokenKind.CloseParenthesis, "Expected ')' after function type parameter list");
		if (compiler.common.parser.Match(TokenKind.Colon))
			declaration.returnType = compiler.ConsumeType("Expected function return type", recursionLevel);

		var functionTypeIndex = compiler.common.chunk.EndAddFunctionType(declaration);
		var type = ValueTypeHelper.SetIndex(ValueType.Function, functionTypeIndex);

		return Option.Some(type);
	}
}