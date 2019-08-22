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

	[Fact]
	public void StructInteropTest()
	{
		var pepper = new Pepper();
		pepper.AddFunction(TestFunction, TestFunction);
		var source = "TestFunction(Point{x=1 y=2 z=3})";
		TestHelper.RunExpression(pepper, source, out var a).GetStruct<Point>(out var point);
		// var source = "fn f():Point{TestFunction(Point{x=1 y=2 z=3})}";
		// TestHelper.Run(pepper, source, out var a).GetStruct<Point>(out var point);
		a.AssertSuccessCall();
		Assert.Equal(2, point.x);
		Assert.Equal(3, point.y);
		Assert.Equal(4, point.z);
	}
}