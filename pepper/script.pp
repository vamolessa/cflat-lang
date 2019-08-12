struct XY {
	x:int
	y:int
}

struct Point {
	xy:XY
	xy2:XY
}

fn main() {
	let p = Point{xy=XY{x=3 y=9} xy2=XY{x=11 y=13}}
	print p.xy2.x
}
