using Xunit;

public sealed class InterfaceTests
{
	public static Function<Tuple<Int>, Int> someFunction;

	public static Return TupleTestFunction<C>(ref C context) where C : IContext
	{
		var t = context.Arg<Tuple<Int, Bool>>();
		var body = context.Body<Tuple<Int, Bool>>();
		t.e0.value += 1;
		t.e1.value = !t.e1.value;
		return body.Return(t);
	}

	[Theory]
	[InlineData("TupleTestFunction({1,true})", 2, false)]
	[InlineData("TupleTestFunction({4,false})", 5, true)]
	public void TupleIoTest(string source, int n, bool b)
	{
		var cflat = new CFlat();
		cflat.AddFunction(TupleTestFunction, TupleTestFunction);
		var t = TestHelper.RunExpression<Tuple<Int, Bool>>(cflat, source, out var a);
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
		var t = context.Arg<MyTuple>();
		var body = context.Body<MyTuple>();
		t.n += 1;
		t.b = !t.b;
		return body.Return(t);
	}

	[Theory]
	[InlineData("NamedTupleTestFunction({1,true})", 2, false)]
	[InlineData("NamedTupleTestFunction({4,false})", 5, true)]
	public void NamedTupleIoTest(string source, int n, bool b)
	{
		var cflat = new CFlat();
		cflat.AddFunction(NamedTupleTestFunction, NamedTupleTestFunction);
		var t = TestHelper.RunExpression<MyTuple>(cflat, source, out var a);
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
		var source = "MyStruct{x=0,y=0,z=0}";
		var cflat = new CFlat();
		cflat.AddStruct<MyStruct>();
		cflat.AddStruct<MyStruct>();
		var s = TestHelper.RunExpression<MyStruct>(cflat, source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(0, s.x);
		Assert.Equal(0, s.y);
		Assert.Equal(0, s.z);
	}

	[Theory]
	[InlineData("MyStruct{x=0,y=0,z=0}", 0, 0, 0)]
	[InlineData("MyStruct{x=1,y=2,z=3}", 1, 2, 3)]
	public void MarshalStructTest(string source, int x, int y, int z)
	{
		var cflat = new CFlat();
		cflat.AddStruct<MyStruct>();
		cflat.AddStruct<MyStruct>();
		var s = TestHelper.RunExpression<MyStruct>(cflat, source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(x, s.x);
		Assert.Equal(y, s.y);
		Assert.Equal(z, s.z);
	}

	private static Return StructTestFunction<C>(ref C context) where C : IContext
	{
		var p = context.Arg<MyStruct>();
		var body = context.Body<MyStruct>();
		System.Console.WriteLine("HELLO FROM C# {0}, {1}, {2}", p.x, p.y, p.z);
		p.x += 1;
		p.y += 1;
		p.z += 1;
		return body.Return(p);
	}

	[Theory]
	[InlineData("StructTestFunction(MyStruct{x=0,y=0,z=0})", 1, 1, 1)]
	[InlineData("StructTestFunction(MyStruct{x=1,y=2,z=3})", 2, 3, 4)]
	public void StructIoTest(string source, int x, int y, int z)
	{
		var cflat = new CFlat();
		cflat.AddFunction(StructTestFunction, StructTestFunction);
		var s = TestHelper.RunExpression<MyStruct>(cflat, source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(x, s.x);
		Assert.Equal(y, s.y);
		Assert.Equal(z, s.z);
	}

	public sealed class MyClass
	{
		public int boxed;
	}

	public static Return CreateClassTestFunction<C>(ref C context) where C : IContext
	{
		var n = context.Arg<Int>();
		var body = context.Body<Class<MyClass>>();
		return body.Return(new MyClass { boxed = n });
	}

	public static Return ModifyClassTestFunction<C>(ref C context) where C : IContext
	{
		var c = context.Arg<Class<MyClass>>().value;
		var body = context.Body<Unit>();
		c.boxed += 1;
		return body.Return(new Unit());
	}

	[Theory]
	[InlineData("fn f():MyClass{let c=CreateClassTestFunction(1) ModifyClassTestFunction(c) c}", 2)]
	[InlineData("fn f():MyClass{let c=CreateClassTestFunction(4) ModifyClassTestFunction(c) c}", 5)]
	public void ClassIoTest(string source, int n)
	{
		var cflat = new CFlat();
		cflat.AddClass<MyClass>();
		cflat.AddFunction(CreateClassTestFunction, CreateClassTestFunction);
		cflat.AddFunction(ModifyClassTestFunction, ModifyClassTestFunction);
		var c = TestHelper.Run<Class<MyClass>>(cflat, source, out var a).value;
		a.AssertSuccessCall();
		Assert.Equal(n, c.boxed);
	}

	public static Return FunctionTestFunction<C>(ref C context) where C : IContext
	{
		var body = context.Body<Int>();
		var n = someFunction.Call(ref context, Tuple.New(new Int(6)));
		return body.Return(n);
	}

	[Fact]
	public void FunctionCallTest()
	{
		var source = "fn some_function(a:int):int{a+1} fn f():int{FunctionTestFunction()}";
		var cflat = new CFlat();
		cflat.AddFunction(FunctionTestFunction, FunctionTestFunction);

		var compileErrors = cflat.CompileSource(source, TestHelper.CompilerMode);
		if (compileErrors.count > 0)
			throw new CompileErrorException(CompilerHelper.FormatError(source, compileErrors, 1, 1));

		someFunction = cflat.GetFunction<Tuple<Int>, Int>("some_function").value;

		var n = cflat.GetFunction<Empty, Int>("f").value.Call(cflat, new Empty());
		Assert.False(cflat.GetError().isSome);
		Assert.Equal(7, n.value);
	}
}