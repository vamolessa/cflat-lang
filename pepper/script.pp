fn otherFoo() {
	print "OH NO"
}

fn someFoo() {
	print "YEAH"
}

fn getThatFoo(): fn() {
	fn(){testFunction()}
}

fn main() {
	getThatFoo()()
}
