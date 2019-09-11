using System.Collections.Generic;
using Xunit;

public sealed class ArrayTests
{
	[Theory]
	[InlineData("[{}:0]", 0)]
	[InlineData("[{}:1]", 1)]
	[InlineData("[{}:8]", 8)]
	[InlineData("[{}:999999]", 999999)]
	public void UnitArrayCreationTest(string source, int expectedLength)
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
	public void IntArrayCreationTest(string source, int expectedLength, int expectedElements)
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

	[Theory]
	[InlineData("[{11, 12, 13}:0]", 0, 11, 12, 13)]
	[InlineData("[{21, 22, 23}:1]", 1, 21, 22, 23)]
	[InlineData("[{31, 32, 33}:8]", 8, 31, 32, 33)]
	public void TupleArrayCreationTest(string source, int expectedLength, int expectedTupleElement0, int expectedTupleElement1, int expectedTupleElement2)
	{
		var v = TestHelper.RunExpression<Array<Tuple<Int, Int, Int>>>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expectedLength, v.Length);

		var expectedArray = new Tuple<Int, Int, Int>[expectedLength];
		System.Array.Fill(expectedArray, new Tuple<Int, Int, Int>(expectedTupleElement0, expectedTupleElement1, expectedTupleElement2));

		var resultArray = new Tuple<Int, Int, Int>[v.Length];
		for (var i = 0; i < resultArray.Length; i++)
			resultArray[i] = v[i];

		Assert.Equal(expectedArray, resultArray);
	}

	[Theory]
	[InlineData("{let a=[5:1] a[0]}", 5)]
	[InlineData("{let a=[7:9] a[0]}", 7)]
	[InlineData("{let a=[7:9] a[1]}", 7)]
	[InlineData("{let a=[7:9] a[8]}", 7)]
	[InlineData("{let a=[7:9] a[4+2*2]}", 7)]
	public void IntArrayIndexingTest(string source, int expected)
	{
		var v = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("{let a=[5:0] a[0]}")]
	[InlineData("{let a=[5:1] a[-1]}")]
	[InlineData("{let a=[5:1] a[1-2]}")]
	[InlineData("{let a=[5:4] a[4]}")]
	[InlineData("{let a=[5:4] a[5]}")]
	[InlineData("{let a=[5:4] a[3+1]}")]
	[InlineData("{let a=[5:4] a[99999]}")]
	public void IntArrayIndexingError(string source)
	{
		var clef = new Clef();
		var v = TestHelper.RunExpression<Int>(clef, source, out var a);
		Assert.True(clef.GetError().isSome);
	}
}