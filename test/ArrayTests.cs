using Xunit;

public sealed class ArrayTests
{
	[Theory]
	[InlineData("[27:0]")]
	[InlineData("[27:1]")]
	[InlineData("[27:8]")]
	[InlineData("[27:1234567890]")]
	public void ArrayCreationTests(string source)
	{
		
	}
}