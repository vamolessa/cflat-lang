using Xunit;

public sealed class InterfaceTests
{
	public struct MyStruct : IStruct
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
	public void AddStructTwiceTest()
	{
		var source = "MyStruct{x=0 y=0 z=0}";
		var pepper = new Pepper();
		pepper.AddStruct<MyStruct>();
		pepper.AddStruct<MyStruct>();
		TestHelper.RunExpression(pepper, source, out var a).GetStruct<MyStruct>(out var p);
		a.AssertSuccessCall();
		Assert.Equal(0, p.x);
		Assert.Equal(0, p.y);
		Assert.Equal(0, p.z);
	}

	[Theory]
	[InlineData("MyStruct{x=0 y=0 z=0}", 0, 0, 0)]
	[InlineData("MyStruct{x=1 y=2 z=3}", 1, 2, 3)]
	public void MarshalPointStruct(string source, int x, int y, int z)
	{
		var pepper = new Pepper();
		pepper.AddStruct<MyStruct>();
		pepper.AddStruct<MyStruct>();
		TestHelper.RunExpression(pepper, source, out var a).GetStruct<MyStruct>(out var p);
		a.AssertSuccessCall();
		Assert.Equal(x, p.x);
		Assert.Equal(y, p.y);
		Assert.Equal(z, p.z);
	}

	private static Return StructTestFunction<C>(ref C context) where C : IContext
	{
		var p = context.ArgStruct<MyStruct>();
		var body = context.BodyOfStruct<MyStruct>();
		System.Console.WriteLine("HELLO FROM C# {0}, {1}, {2}", p.x, p.y, p.z);
		p.x += 1;
		p.y += 1;
		p.z += 1;
		return body.Return(p);
	}

	[Theory]
	[InlineData("StructTestFunction(MyStruct{x=0 y=0 z=0})", 1, 1, 1)]
	[InlineData("StructTestFunction(MyStruct{x=1 y=2 z=3})", 2, 3, 4)]
	public void StructIoTest(string source, int x, int y, int z)
	{
		var pepper = new Pepper();
		pepper.AddFunction(StructTestFunction, StructTestFunction);
		TestHelper.RunExpression(pepper, source, out var a).GetStruct<MyStruct>(out var p);
		a.AssertSuccessCall();
		Assert.Equal(x, p.x);
		Assert.Equal(y, p.y);
		Assert.Equal(z, p.z);
	}

	public sealed class MyClass
	{
		public int boxed;
	}

	public static Return ClassTestFunction<C>(ref C context) where C : IContext
	{
		var n = context.ArgInt();
		var body = context.BodyOfObject<MyClass>();
		return body.Return(new MyClass { boxed = n });
	}

	[Theory]
	[InlineData("ClassTestFunction(1))", 1)]
	[InlineData("ClassTestFunction(4))", 4)]
	public void ClassIoTest(string source, int n)
	{
		var pepper = new Pepper();
		pepper.AddFunction(ClassTestFunction, ClassTestFunction);
		TestHelper.RunExpression(pepper, source, out var a).GetObject<MyClass>(out var c);
		a.AssertSuccessCall();
		Assert.Equal(n, c.boxed);
	}

	public static Return FunctionTestFunction<C>(ref C context) where C : IContext
	{
		var body = context.BodyOfInt();
		var success = body.Call("some_function").WithInt(6).GetInt(out var n);
		return body.Return(n);
	}

	[Fact]
	public void FunctionCallTest()
	{
		var source = "fn some_function(a:int):int{a+1} fn f():int{FunctionTestFunction()}";
		var pepper = new Pepper();
		pepper.AddFunction(FunctionTestFunction, FunctionTestFunction);
		TestHelper.Run(pepper, source, out var a).GetInt(out var n);
		a.AssertSuccessCall();
		Assert.Equal(7, n);
	}
}