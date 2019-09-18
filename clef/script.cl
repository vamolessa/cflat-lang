fn print_a(a:[int]) {
	print a[0]
}

fn main() {
	let a = [0:1]
	a[0] = 2
	print_a(a)
}