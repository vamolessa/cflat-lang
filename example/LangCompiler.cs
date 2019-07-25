public static class LangCompiler
{
	public static Result<ByteCodeChunk, string> Compile(string source, ITokenizer tokenizer)
	{
		tokenizer.Begin(LangScanners.scanners, source);
		var chunk = new ByteCodeChunk();

/*
		Advance();
		Expression();
		Consume(Token.EndKind, "Expect end of expression.");
 */

		return Result.Ok(chunk);
	}
}