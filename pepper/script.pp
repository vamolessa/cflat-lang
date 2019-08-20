fn print_native(o: object) {
	print o
}

fn main() {
	print TestFunction(Point{x=1 y=3 z=11})
	print_native(OtherFunction())
}