using Xunit;

public sealed class InterfaceTests
{
	public static Return TupleTestFunction<C>(ref C context) where C : IContext
	{
		var t = context.ArgTuple<Tuple<Int, Bool>>();
		var body = context.BodyOfTuple<Tuple<Int, Bool>>();
		t.e0.value += 1;
		t.e1.value = !t.e1.value;
		return body.Return(t);
	}

	[Theory]
	[InlineData("TupleTestFunction(tuple{1 true})", 2, false)]
	[InlineData("TupleTestFunction(tuple{4 false})", 5, true)]
	public void TupleIoTest(string source, int n, bool b)
	{
		var pepper = new Pepper();
		pepper.AddFunction(TupleTestFunction, TupleTestFunction);
		TestHelper.RunExpression(pepper, source, out var a).GetTuple<Tuple<Int, Bool>>(out var t);
		a.AssertSuccessCall();
		Assert.Equal(n, t.e0.value);
		Assert.Equal(b, t.e1.value);
	}

	public struct MyTuple : ITuple
	{
		public int n;
		public bool b;

		public void Marshal<M>(ref M marshaler) where M : IMarshaler
		{
			marshaler.Marshal(ref n, null);
			marshaler.Marshal(ref b, null);
		}
	}

	public static Return NamedTupleTestFunction<C>(ref C context) where C : IContext
	{
		var t = context.ArgTuple<MyTuple>();
		var body = context.BodyOfTuple<MyTuple>();
		t.n += 1;
		t.b = !t.b;
		return body.Return(t);
	}

	[Theory]
	[InlineData("NamedTupleTestFunction(tuple{1 true})", 2, false)]
	[InlineData("NamedTupleTestFunction(tuple{4 false})", 5, true)]
	public void NamedTupleIoTest(string source, int n, bool b)
	{
		var pepper = new Pepper();
		pepper.AddFunction(NamedTupleTestFunction, NamedTupleTestFunction);
		TestHelper.RunExpression(pepper, source, out var a).GetTuple<MyTuple>(out var t);
		a.AssertSuccessCall();
		Assert.Equal(n, t.n);
		Assert.Equal(b, t.b);
	}

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