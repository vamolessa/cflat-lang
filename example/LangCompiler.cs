using System.Collections.Generic;

public sealed class LangCompiler
{
	private readonly Parser parser = new Parser();

	public Result<ByteCodeChunk, List<ParseError>> Compile(string source, ITokenizer tokenizer)
	{
		var chunk = new ByteCodeChunk();

		tokenizer.Begin(LangScanners.scanners, source);
		parser.Begin(tokenizer);

		parser.Next();
		Expression(chunk);
		parser.Consume(Token.EndKind, "Expect end of expression.");

		// end compiler
		chunk.EmitInstruction(parser, Instruction.Return);

		if (parser.errors.Count > 0)
			return Result.Error(parser.errors);
		return Result.Ok(chunk);
	}

	private void Expression(ByteCodeChunk chunk)
	{

	}

	private void Number(ByteCodeChunk chunk)
	{
		var value = parser.Convert((s, t) =>
		{
			if (t.kind == (int)TokenKind.IntegerNumber)
				return new Value(ParserHelper.ToInteger(s, t));
			else
				return new Value(ParserHelper.ToFloat(s, t));
		});

		chunk.EmitLoadConstant(parser, value);
	}
}