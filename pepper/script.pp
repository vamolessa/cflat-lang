fn otherFoo() {
	print "OH NO"
}

fn someFoo() {
	print "YEAH"
}

fn getThatFoo(): fn() {
	fn(){testFunction()}
	//someFoo
}

fn main() {
	print testFunction
	testFunction()
	getThatFoo()()
}
