struct Point {
	x:int
	y:int
}

struct A {
	a:int
	p:Point
}

fn main() {
	mut a = A{a=5 p=Point{x=0 y=0}}
	a.p.x = 7
	print a.p.y
}
