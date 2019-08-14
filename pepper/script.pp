fn point(): struct{x:int s:struct{a:int b:int}} {
	struct{x=4 s=struct{a=1 b=9}}
}

fn main() {
	mut p = point()
	p.s.a = 13
	print p
}
