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
}