struct SS{a:int,b:int,c:int}

fn f():int{let a=[SS{a=11,b=22,c=33}:1] a[0].a}

fn main() {
	print f()
}