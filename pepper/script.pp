struct Vector
{
	x: int
	y: int
}

fn newVector(x: int, y: int): Vector {
	Vector {
		x = x
		y = y
	}
}

fn printVector(header: string, v: Vector): bool {
	print header
	print v
	true
}

fn main() {
	mut a = 3
	let v = newVector(31, 17)
	printVector("hey!", v)
	print a
	a = 10
	print a
}
