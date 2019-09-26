struct S{x:int,y:int}

fn main() {
	let mut a = S{x=0,y=0}
	set a = S{x=1,y=1}
	set a = S{x=2,y=2}
	set a = S{x=3,y=3}

	print a
}