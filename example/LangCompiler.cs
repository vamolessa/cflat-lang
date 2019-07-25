using System.Collections.Generic;

public sealed class LangCompiler
{
	private readonly Compiler compiler = new Compiler();

	public Result<ByteCodeChunk, List<CompileError>> Compile(string source, ITokenizer tokenizer)
	{
		tokenizer.Begin(LangScanners.scanners, source);
		compiler.Begin(tokenizer);

		compiler.Next();
		Expression();
		compiler.Consume(Token.EndKind, "Expect end of expression.");

		// end compiler
		compiler.EmitInstruction(Instruction.Return);

		if (compiler.errors.Count > 0)
			return Result.Error(compiler.errors);
		return Result.Ok(compiler.GetByteCodeChunk());
	}

	private void Expression()
	{

	}

	private void Grouping()
	{
		Expression();
		compiler.Consume((int)TokenKind.CloseParenthesis, "Expect ')' after expression.");
	}

	private void Number()
	{
		var value = compiler.Convert((s, t) =>
		{
			if (t.kind == (int)TokenKind.IntegerNumber)
				return new Value(CompilerHelper.ToInteger(s, t));
			else
				return new Value(CompilerHelper.ToFloat(s, t));
		});

		compiler.EmitLoadConstant(value);
	}
}