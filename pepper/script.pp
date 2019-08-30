struct S {
	b:bool,
	i:int
}

fn b(t:S):int {
	let s=t
	s.b
	s.i
}

fn main() {
	b(S{b=true,i=3})
}