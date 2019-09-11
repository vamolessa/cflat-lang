using System.Collections.Generic;
using Xunit;

public sealed class ArrayTests
{
	[Theory]
	[InlineData("[{}:0]", 0)]
	[InlineData("[{}:1]", 1)]
	[InlineData("[{}:8]", 8)]
	[InlineData("[{}:999999]", 999999)]
	public void UnitArrayCreationTests(string source, int expectedLength)
	{
		var v = TestHelper.RunExpression<Array<Unit>>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expectedLength, v.Length);

		var expectedArray = new Unit[expectedLength];
		System.Array.Fill(expectedArray, new Unit());

		var resultArray = new Unit[v.Length];
		for (var i = 0; i < resultArray.Length; i++)
			resultArray[i] = v[i];

		Assert.Equal(expectedArray, resultArray, EqualityComparer<Unit>.Default);
	}

	[Theory]
	[InlineData("[27:0]", 0, 27)]
	[InlineData("[27:1]", 1, 27)]
	[InlineData("[27:8]", 8, 27)]
	[InlineData("[27:999999]", 999999, 27)]
	public void IntArrayCreationTests(string source, int expectedLength, int expectedElements)
	{
		var v = TestHelper.RunExpression<Array<Int>>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expectedLength, v.Length);

		var expectedArray = new int[expectedLength];
		System.Array.Fill(expectedArray, expectedElements);

		var resultArray = new int[v.Length];
		for (var i = 0; i < resultArray.Length; i++)
			resultArray[i] = v[i];

		Assert.Equal(expectedArray, resultArray);
	}
}