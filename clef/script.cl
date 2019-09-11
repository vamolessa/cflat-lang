struct S {
	a: {int,int},
	b: int,
	c: {int,int,int}
}

fn main() {
	let a = [S{a={11,12}, b=21, c={31,32,33}}:2]
	print a[0].c
}
