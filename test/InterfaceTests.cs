using Xunit;

public sealed class InterfaceTests
{
	public struct Point : IStruct
	{
		public int x;
		public int y;
		public int z;

		public void Marshal<M>(ref M marshaler) where M : IMarshaler
		{
			marshaler.Marshal(ref x, nameof(x));
			marshaler.Marshal(ref y, nameof(y));
			marshaler.Marshal(ref z, nameof(z));
		}
	}

	[Fact]
	public void AddStructTwice()
	{
		var source = "Point{x=0 y=0 z=0}";
		var pepper = new Pepper();
		pepper.AddStruct<Point>();
		pepper.AddStruct<Point>();
		TestHelper.RunExpression(pepper, source, out var a).GetStruct<Point>(out var p);
		a.AssertSuccessCall();
		Assert.Equal(0, p.x);
		Assert.Equal(0, p.y);
		Assert.Equal(0, p.z);
	}

	[Theory]
	[InlineData("Point{x=0 y=0 z=0}", 0, 0, 0)]
	[InlineData("Point{x=1 y=2 z=3}", 1, 2, 3)]
	public void MarshalPointStruct(string source, int x, int y, int z)
	{
		var pepper = new Pepper();
		pepper.AddStruct<Point>();
		pepper.AddStruct<Point>();
		TestHelper.RunExpression(pepper, source, out var a).GetStruct<Point>(out var p);
		a.AssertSuccessCall();
		Assert.Equal(x, p.x);
		Assert.Equal(y, p.y);
		Assert.Equal(z, p.z);
	}

	private static Return TestFunction<C>(ref C context) where C : IContext
	{
		var p = context.ArgStruct<Point>();
		var body = context.BodyOfStruct<Point>();
		System.Console.WriteLine("HELLO FROM C# {0}, {1}, {2}", p.x, p.y, p.z);
		p.x += 1;
		p.y += 1;
		p.z += 1;
		return body.Return(p);
	}

	[Theory]
	[InlineData("TestFunction(Point{x=0 y=0 z=0})", 1, 1, 1)]
	[InlineData("TestFunction(Point{x=1 y=2 z=3})", 2, 3, 4)]
	public void StructIoTest(string source, int x, int y, int z)
	{
		var pepper = new Pepper();
		pepper.AddFunction(TestFunction, TestFunction);
		TestHelper.RunExpression(pepper, source, out var a).GetStruct<Point>(out var p);
		a.AssertSuccessCall();
		Assert.Equal(x, p.x);
		Assert.Equal(y, p.y);
		Assert.Equal(z, p.z);
	}
}