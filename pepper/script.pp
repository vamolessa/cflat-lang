struct XY {
	x:int
	y:int
}

struct Point {
	xy:XY
	z:int
}

fn main() {
	let p = Point{xy=XY{x=3 y=9} z=7}
	print p.xy
}
