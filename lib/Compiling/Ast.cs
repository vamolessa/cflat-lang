public sealed class Ast
{
	public Buffer<AstNode> nodes = new Buffer<AstNode>(1024);
}

public readonly struct AstNode
{
	public readonly Token token;
	public readonly int childCount;
	public readonly int returnType;

	public AstNode(Token token, int childCount, int returnType)
	{
		this.token = token;
		this.childCount = childCount;
		this.returnType = returnType;
	}
}
