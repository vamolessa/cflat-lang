fn f(a: [int]) {
	set a[0] = 1
}

fn main() {
	let a = [2, 1]
	let b = a
	set b[0] = 1
	f(a)
}