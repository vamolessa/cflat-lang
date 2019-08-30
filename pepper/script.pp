struct S {
	b:bool,
	i:int
}

fn b(t:S):int {
	let s=t
	s.b
	s.i
}

fn main():int {
	b(S{b=true,i=3})
}