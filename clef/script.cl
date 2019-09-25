struct T {
	x: float
}

struct S {
	a: [mut T],
	b: bool
}

fn main() {
	let mut s = S{a=[T{x=0.0}, 1], b=true}
	set s.a[0].x = 1.0
	print s
	print s.a[0]
}