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

	public static ValueType ConsumeType(this LangCompiler lang, Compiler compiler, string error, int recursiveLevel)
	{
		if (recursiveLevel > 8)
		{
			compiler.AddSoftError(compiler.previousToken.slice, "Type is nested too deeply");
			return ValueType.Unit;
		}

		var type = new Option<ValueType>();
		if (compiler.Match((int)TokenKind.Identifier))
			type = lang.ResolveSimpleType(compiler, compiler.previousToken.slice, recursiveLevel + 1);
		else if (compiler.Match((int)TokenKind.Function))
			type = lang.ResolveFunctionType(compiler, recursiveLevel + 1);

		if (type.isSome)
			return type.value;

		compiler.AddSoftError(compiler.previousToken.slice, error);
		return ValueType.Unit;
	}

	private static Option<ValueType> ResolveSimpleType(this LangCompiler lang, Compiler compiler, Slice slice, int recursiveLevel)
	{
		var source = compiler.tokenizer.Source;
		if (CompilerHelper.AreEqual(source, slice, "bool"))
			return Option.Some(ValueType.Bool);
		else if (CompilerHelper.AreEqual(source, slice, "int"))
			return Option.Some(ValueType.Int);
		else if (CompilerHelper.AreEqual(source, slice, "float"))
			return Option.Some(ValueType.Float);
		else if (CompilerHelper.AreEqual(source, slice, "string"))
			return Option.Some(ValueType.String);
		return Option.None;
	}

	private static Option<ValueType> ResolveFunctionType(this LangCompiler lang, Compiler compiler, int recursiveLevel)
	{
		var declaration = compiler.chunk.BeginAddFunctionType();

		compiler.Consume((int)TokenKind.OpenParenthesis, "Expected '(' after function type");
		if (!compiler.Check((int)TokenKind.CloseParenthesis))
		{
			do
			{
				var paramType = lang.ConsumeType(compiler, "Expected function parameter type", recursiveLevel);
				declaration.AddParam(paramType);
			} while (compiler.Match((int)TokenKind.Comma));
		}
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expected ')' after function type parameter list");
		if (compiler.Match((int)TokenKind.Colon))
			declaration.returnType = lang.ConsumeType(compiler, "Expected function return type", recursiveLevel);

		var functionTypeIndex = compiler.chunk.EndAddFunctionType(declaration);
		var type = ValueTypeHelper.SetIndex(ValueType.Function, functionTypeIndex);

		return Option.Some(type);
	}
}