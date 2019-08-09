fn anotherFoo() {
	print "Whoopsies!"
}

fn someFoo() {
	print "YEAH"
}

fn getThatFoo(): fn() {
	testFunction
}

fn main() {
	print testFunction
	testFunction()
	getThatFoo()()
}
