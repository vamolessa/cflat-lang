using Xunit;

public sealed class ArrayTests
{
	[Theory]
	[InlineData("[27:0]", 0)]
	[InlineData("[27:1]", 1)]
	[InlineData("[27:8]", 8)]
	[InlineData("[27:9999999]", 9999999)]
	public void IntArrayCreationTests(string source, int expectedLength)
	{
		var v = TestHelper.RunExpression<Array<Int>>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expectedLength, v.Length);
	}
}