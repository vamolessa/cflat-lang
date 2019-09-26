struct T {
	x: float
}

struct S {
	a: [mut T],
	b: bool
}

fn getS(ts: [mut T]): S {
	S{a=ts, b=true}
}

fn getT(): T {
	T{x=6.0}
}

fn main2() {
	let ts = [T{x=0.0}, 1]
	set getS(mut ts).a[0].x = 1.0
	print ts[0]
	print getT().x
}