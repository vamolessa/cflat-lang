using Xunit;

public sealed class InterfaceTests
{
	public static Function<Tuple<Int>, Int> someFunction;

	public static Tuple<Int, Bool> TupleTestFunction(VirtualMachine vm, Tuple<Int, Bool> tuple)
	{
		tuple.e0.value += 1;
		tuple.e1.value = !tuple.e1.value;
		return tuple;
	}

	[Theory]
	[InlineData("TupleTestFunction({1,true})", 2, false)]
	[InlineData("TupleTestFunction({4,false})", 5, true)]
	public void TupleIoTest(string source, int n, bool b)
	{
		var cflat = new CFlat();
		cflat.AddFunction<Tuple<Int, Bool>, Tuple<Int, Bool>>(nameof(TupleTestFunction), TupleTestFunction);
		var t = TestHelper.RunExpression<Tuple<Int, Bool>>(cflat, source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(n, t.e0.value);
		Assert.Equal(b, t.e1.value);
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
		var s = TestHelper.RunExpression<Struct<MyStruct>>(cflat, source, out var a).value;
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
		var s = TestHelper.RunExpression<Struct<MyStruct>>(cflat, source, out var a).value;
		a.AssertSuccessCall();
		Assert.Equal(x, s.x);
		Assert.Equal(y, s.y);
		Assert.Equal(z, s.z);
	}

	private static Struct<MyStruct> StructTestFunction(VirtualMachine vm, Struct<MyStruct> s)
	{
		s.value.x += 1;
		s.value.y += 1;
		s.value.z += 1;
		return s;
	}

	[Theory]
	[InlineData("StructTestFunction(MyStruct{x=0,y=0,z=0})", 1, 1, 1)]
	[InlineData("StructTestFunction(MyStruct{x=1,y=2,z=3})", 2, 3, 4)]
	public void StructIoTest(string source, int x, int y, int z)
	{
		var cflat = new CFlat();
		cflat.AddFunction<Struct<MyStruct>, Struct<MyStruct>>(nameof(StructTestFunction), StructTestFunction);
		var s = TestHelper.RunExpression<Struct<MyStruct>>(cflat, source, out var a).value;
		a.AssertSuccessCall();
		Assert.Equal(x, s.x);
		Assert.Equal(y, s.y);
		Assert.Equal(z, s.z);
	}

	public struct MyNestingStruct : IStruct
	{
		public Struct<MyStruct> a;
		public bool b;

		public void Marshal<M>(ref M marshaler) where M : IMarshaler
		{
			marshaler.Marshal(ref a, nameof(a));
			marshaler.Marshal(ref b, nameof(b));
		}
	}

	[Theory]
	[InlineData("MyNestingStruct{a=MyStruct{x=0,y=0,z=0},b=true}", 0, 0, 0, true)]
	[InlineData("MyNestingStruct{a=MyStruct{x=1,y=2,z=3},b=false}", 1, 2, 3, false)]
	public void MarshalNestingStructTest(string source, int x, int y, int z, bool b)
	{
		var cflat = new CFlat();
		cflat.AddStruct<MyNestingStruct>();
		var s = TestHelper.RunExpression<Struct<MyNestingStruct>>(cflat, source, out var a).value;
		a.AssertSuccessCall();
		Assert.Equal(x, s.a.value.x);
		Assert.Equal(y, s.a.value.y);
		Assert.Equal(z, s.a.value.z);
		Assert.Equal(b, s.b);
	}

	public sealed class MyClass
	{
		public int boxed;
	}

	[Fact]
	public void AddClassTwiceTest()
	{
		var cflat = new CFlat();
		cflat.AddClass<MyClass>();
		cflat.AddClass<MyClass>();
	}

	public static Class<MyClass> CreateClassTestFunction(VirtualMachine vm, Int n)
	{
		return new MyClass { boxed = n };
	}

	public static Unit ModifyClassTestFunction(VirtualMachine vm, Class<MyClass> c)
	{
		c.value.boxed += 1;
		return default;
	}

	[Theory]
	[InlineData("fn f():MyClass{let c=CreateClassTestFunction(1) ModifyClassTestFunction(c) c}", 2)]
	[InlineData("fn f():MyClass{let c=CreateClassTestFunction(4) ModifyClassTestFunction(c) c}", 5)]
	public void ClassIoTest(string source, int n)
	{
		var cflat = new CFlat();
		cflat.AddClass<MyClass>();
		cflat.AddFunction<Int, Class<MyClass>>(nameof(CreateClassTestFunction), CreateClassTestFunction);
		cflat.AddFunction<Class<MyClass>, Unit>(nameof(ModifyClassTestFunction), ModifyClassTestFunction);
		var c = TestHelper.Run<Class<MyClass>>(cflat, source, out var a).value;
		a.AssertSuccessCall();
		Assert.Equal(n, c.boxed);
	}

	public struct MyStructWithClass : IStruct
	{
		public Class<MyClass> a;

		public void Marshal<M>(ref M marshaler) where M : IMarshaler
		{
			marshaler.Marshal(ref a, nameof(a));
		}
	}

	[Theory]
	[InlineData("fn f():MyStructWithClass{MyStructWithClass{a=CreateClassTestFunction(2)}}", 2)]
	[InlineData("fn f():MyStructWithClass{MyStructWithClass{a=CreateClassTestFunction(5)}}", 5)]
	public void MyStructWithClassIoTest(string source, int n)
	{
		var cflat = new CFlat();
		cflat.AddClass<MyClass>();
		cflat.AddStruct<MyStructWithClass>();
		cflat.AddFunction<Int, Class<MyClass>>(nameof(CreateClassTestFunction), CreateClassTestFunction);
		var s = TestHelper.Run<Struct<MyStructWithClass>>(cflat, source, out var a).value;
		a.AssertSuccessCall();
		Assert.Equal(n, s.a.value.boxed);
	}

	public static Int FunctionTestFunction(VirtualMachine vm)
	{
		var n = someFunction.Call(vm, Tuple.New(new Int(6)));
		return n;
	}

	[Fact]
	public void FunctionCallTest()
	{
		var source = "fn some_function(a:int):int{a+1} fn f():int{FunctionTestFunction()}";
		var cflat = new CFlat();
		cflat.AddFunction<Int>(nameof(FunctionTestFunction), FunctionTestFunction);

		var compileErrors = cflat.CompileSource("tests", source, TestHelper.CompilerMode);
		if (compileErrors.count > 0)
			throw new CompileErrorException(cflat.GetFormattedCompileErrors(1));

		cflat.Load();
		someFunction = cflat.GetFunction<Tuple<Int>, Int>("some_function").value;

		var n = cflat.GetFunction<Empty, Int>("f").value.Call(cflat, new Empty());
		Assert.False(cflat.GetError().isSome);
		Assert.Equal(7, n.value);
	}

	[Theory]
	[InlineData("fn f():int{0}")]
	[InlineData("fn f(a:bool){}")]
	public void FunctionNotFoundErrors(string source)
	{
		Assert.Throws<FunctionNotFoundException>(() =>
		{
			TestHelper.Run<Unit>(source, out var a);
		});
	}

	public static Unit ArrayTestFunction(VirtualMachine vm, Array<Int> a)
	{
		var length = a.Length;
		for (var i = 0; i < length; i++)
			a[i] += 1;
		return default;
	}

	[Theory]
	[InlineData("{let mut a=[0:3] set a[1]=1 set a[2]=2 ArrayTestFunction(a) a}", 1, 2, 3)]
	[InlineData("{let mut a=[10:3] set a[1]=11 set a[2]=12 ArrayTestFunction(a) a}", 11, 12, 13)]
	public void ArrayIoTest(string source, int e0, int e1, int e2)
	{
		var cflat = new CFlat();
		cflat.AddFunction<Array<Int>, Unit>(nameof(ArrayTestFunction), ArrayTestFunction);
		var array = TestHelper.RunExpression<Array<Int>>(cflat, source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(3, array.Length);
		Assert.Equal(e0, array[0].value);
		Assert.Equal(e1, array[1].value);
		Assert.Equal(e2, array[2].value);
	}

	public static Unit RefTestFunction(VirtualMachine vm, MutRef<Int> r)
	{
		r.Value += 1;
		return default;
	}

	[Theory]
	[InlineData("{let mut a=0 RefTestFunction(&mut a) a}", 1)]
	[InlineData("{let mut a=15 RefTestFunction(&mut a) a}", 16)]
	public void RefIoTest(string source, int n)
	{
		var cflat = new CFlat();
		cflat.AddFunction<MutRef<Int>, Unit>(nameof(RefTestFunction), RefTestFunction);
		var v = TestHelper.RunExpression<Int>(cflat, source, out var a);
		a.AssertSuccessCall();
		Assert.Equal(n, v.value);
	}

}