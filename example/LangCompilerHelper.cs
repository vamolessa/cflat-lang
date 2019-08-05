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

		if (compiler.Match((int)TokenKind.Identifier))
			type = lang.ResolveIdentifierType(compiler, recursionLevel + 1);
		else if (compiler.Match((int)TokenKind.Function))
			type = lang.ResolveFunctionType(compiler, recursionLevel + 1);

		if (type.isSome)
			return type.value;

		compiler.AddSoftError(compiler.previousToken.slice, error);
		return ValueType.Unit;
	}

	public static Option<ValueType> ResolveBasicType(string source, Slice slice)
	{
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

	private static Option<ValueType> ResolveIdentifierType(this LangCompiler lang, Compiler compiler, int recursionLevel)
	{
		var source = compiler.tokenizer.Source;
		var slice = compiler.previousToken.slice;

		var basicType = ResolveBasicType(source, slice);
		if (basicType.isSome)
			return basicType;

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

		compiler.Consume((int)TokenKind.OpenParenthesis, "Expected '(' after function type");
		if (!compiler.Check((int)TokenKind.CloseParenthesis))
		{
			do
			{
				var paramType = lang.ConsumeType(compiler, "Expected function parameter type", recursionLevel);
				declaration.AddParam(paramType);
			} while (compiler.Match((int)TokenKind.Comma));
		}
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expected ')' after function type parameter list");
		if (compiler.Match((int)TokenKind.Colon))
			declaration.returnType = lang.ConsumeType(compiler, "Expected function return type", recursionLevel);

		var functionTypeIndex = compiler.chunk.EndAddFunctionType(declaration);
		var type = ValueTypeHelper.SetIndex(ValueType.Function, functionTypeIndex);

		return Option.Some(type);
	}
}