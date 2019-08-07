public static class LangCompilerHelper
{
	public static void BeginLoop(LangCompiler lang)
	{
		lang.loopNesting += 1;
	}

	public static void EndLoop(LangCompiler lang, Compiler compiler)
	{
		lang.loopNesting -= 1;

		for (var i = lang.loopBreaks.count - 1; i >= 0; i--)
		{
			var loopBreak = lang.loopBreaks.buffer[i];
			if (loopBreak.nesting == lang.loopNesting)
			{
				compiler.EndEmitForwardJump(loopBreak.jump);
				lang.loopBreaks.SwapRemove(i);
			}
		}
	}

	public static bool BreakLoop(LangCompiler lang, int nesting, int jump)
	{
		if (lang.loopNesting < nesting)
			return false;

		lang.loopBreaks.PushBack(new LoopBreak(lang.loopNesting - nesting, jump));
		return true;
	}

	public static ValueType ConsumeType(this LangCompiler lang, Compiler compiler, string error, int recursionLevel)
	{
		if (recursionLevel > 8)
		{
			compiler.AddSoftError(compiler.previousToken.slice, "Type is nested too deeply");
			return ValueType.Unit;
		}

		var type = new Option<ValueType>();

		if (compiler.Match(TokenKind.Bool))
			type = Option.Some(ValueType.Bool);
		else if (compiler.Match(TokenKind.Int))
			type = Option.Some(ValueType.Int);
		else if (compiler.Match(TokenKind.Float))
			type = Option.Some(ValueType.Float);
		else if (compiler.Match(TokenKind.String))
			type = Option.Some(ValueType.String);
		else if (compiler.Match(TokenKind.Identifier))
			type = lang.ResolveStructType(compiler, recursionLevel + 1);
		else if (compiler.Match(TokenKind.Function))
			type = lang.ResolveFunctionType(compiler, recursionLevel + 1);

		if (type.isSome)
			return type.value;

		compiler.AddSoftError(compiler.previousToken.slice, error);
		return ValueType.Unit;
	}

	private static Option<ValueType> ResolveStructType(this LangCompiler lang, Compiler compiler, int recursionLevel)
	{
		var source = compiler.tokenizer.source;
		var slice = compiler.previousToken.slice;

		for (var i = 0; i < compiler.chunk.structTypes.count; i++)
		{
			var structName = compiler.chunk.structTypes.buffer[i].name;
			if (CompilerHelper.AreEqual(source, slice, structName))
				return Option.Some(ValueTypeHelper.SetIndex(ValueType.String, i));
		}

		return Option.None;
	}

	private static Option<ValueType> ResolveFunctionType(this LangCompiler lang, Compiler compiler, int recursionLevel)
	{
		var declaration = compiler.chunk.BeginAddFunctionType();

		compiler.Consume(TokenKind.OpenParenthesis, "Expected '(' after function type");
		if (!compiler.Check(TokenKind.CloseParenthesis))
		{
			do
			{
				var paramType = lang.ConsumeType(compiler, "Expected function parameter type", recursionLevel);
				declaration.AddParam(paramType);
			} while (compiler.Match(TokenKind.Comma));
		}
		compiler.Consume(TokenKind.CloseParenthesis, "Expected ')' after function type parameter list");
		if (compiler.Match(TokenKind.Colon))
			declaration.returnType = lang.ConsumeType(compiler, "Expected function return type", recursionLevel);

		var functionTypeIndex = compiler.chunk.EndAddFunctionType(declaration);
		var type = ValueTypeHelper.SetIndex(ValueType.Function, functionTypeIndex);

		return Option.Some(type);
	}
}