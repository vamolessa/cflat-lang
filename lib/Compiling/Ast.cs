public sealed class Ast
{
	public Buffer<AstNode> nodes = new Buffer<AstNode>(1024);
}

public readonly struct AstNode
{
	public readonly int kind;
	public readonly int childCount;
	public readonly int returnType;

	public AstNode(int kind, int childCount, int returnType)
	{
		this.kind = kind;
		this.childCount = childCount;
		this.returnType = returnType;
	}
}
