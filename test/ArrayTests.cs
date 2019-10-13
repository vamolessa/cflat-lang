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
	[InlineData("{let a=[{}:0] length a}", 0)]
	[InlineData("{let a=[{}:1] length a}", 1)]
	[InlineData("{let a=[{}:8] length a}", 8)]
	[InlineData("{let a=[{}:999999] length a}", 999999)]
	public void ArrayLengthTest(string source, int expectedLength)
	{
		var v = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expectedLength, v.value);
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

	[Theory]
	[InlineData("fn f():int{let a=[SS{a=11,b=22,c=33}:1] a[0].a}", 11)]
	[InlineData("fn f():int{let a=[SS{a=11,b=22,c=33}:1] a[0].b}", 22)]
	[InlineData("fn f():int{let a=[SS{a=11,b=22,c=33}:1] a[0].c}", 33)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] a[0].a.f0}", 11)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] a[0].a.f1}", 12)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] a[0].b.f0}", 21)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] a[0].c.f0}", 31)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] a[0].c.f1}", 32)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] a[0].c.f2}", 33)]
	public void IntArrayFieldIndexingTest(string source, int expected)
	{
		var declarations =
			"struct S1{f0:int}" +
			"struct S2{f0:int,f1:int}" +
			"struct S3{f0:int,f1:int,f2:int}" +
			"struct S{a:S2,b:S1,c:S3}" +
			"struct SS{a:int,b:int,c:int}";
		var v = TestHelper.Run<Int>(declarations + source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("{let a=[5:1] set a[0]=99 a[0]}", 99)]
	[InlineData("{let a=[7:9] set a[0]=99 a[0]}", 99)]
	[InlineData("{let a=[7:9] set a[1]=99 a[1]}", 99)]
	[InlineData("{let a=[7:9] set a[8]=99 a[8]}", 99)]
	[InlineData("{let a=[7:9] set a[4+2*2]=99 a[4+2*2]}", 99)]
	public void IntArrayIndexAssignmentTest(string source, int expected)
	{
		var v = TestHelper.RunExpression<Int>(source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Theory]
	[InlineData("fn f():int{let a=[SS{a=11,b=22,c=33}:1] set a[0].a=99 a[0].a}", 99)]
	[InlineData("fn f():int{let a=[SS{a=11,b=22,c=33}:1] set a[0].b=99 a[0].b}", 99)]
	[InlineData("fn f():int{let a=[SS{a=11,b=22,c=33}:1] set a[0].c=99 a[0].c}", 99)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] set a[0].a.f0=99 a[0].a.f0}", 99)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] set a[0].a.f1=99 a[0].a.f1}", 99)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] set a[0].b.f0=99 a[0].b.f0}", 99)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] set a[0].c.f0=99 a[0].c.f0}", 99)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] set a[0].c.f1=99 a[0].c.f1}", 99)]
	[InlineData("fn f():int{let a=[S{a=S2{f0=11,f1=12},b=S1{f0=21},c=S3{f0=31,f1=32,f2=33}}:1] set a[0].c.f2=99 a[0].c.f2}", 99)]
	public void IntArrayFieldIndexAssignmentTest(string source, int expected)
	{
		var declarations =
			"struct S1{f0:int}" +
			"struct S2{f0:int,f1:int}" +
			"struct S3{f0:int,f1:int,f2:int}" +
			"struct S{a:S2,b:S1,c:S3}" +
			"struct SS{a:int,b:int,c:int}";
		var v = TestHelper.Run<Int>(declarations + source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(expected, v.value);
	}

	[Fact]
	public void ArrayMutabilityError()
	{
		var source = "fn func(a:[int]){set a[0]=8} fn f(){func([0:1])}";
		Assert.Throws<CompileErrorException>(() =>
		{
			TestHelper.Run<Int>(source, out var a);
		});
	}
}