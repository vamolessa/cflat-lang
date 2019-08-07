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

fn main() {
	let v = newVector(31, 17)
	print v
}
