public enum Instruction
{
	Halt,
	Call,
	CallNative,
	Return,
	Print,
	Pop,
	PopMultiple,
	Move,
	LoadUnit,
	LoadTrue,
	LoadFalse,
	LoadLiteral,
	LoadFunction,
	LoadNativeFunction,
	SetLocal,
	LoadLocal,
	SetLocalMultiple,
	LoadLocalMultiple,
	IncrementLocalInt,
	LoadField,
	IntToFloat,
	FloatToInt,
	CreateArray,
	LoadArrayLength,
	SetArrayElement,
	LoadArrayElement,
	CreateStackReference,
	SetReference,
	LoadReference,
	NegateInt,
	NegateFloat,
	AddInt,
	AddFloat,
	SubtractInt,
	SubtractFloat,
	MultiplyInt,
	MultiplyFloat,
	DivideInt,
	DivideFloat,
	Not,
	EqualBool,
	EqualInt,
	EqualFloat,
	EqualString,
	GreaterInt,
	GreaterFloat,
	LessInt,
	LessFloat,
	JumpForward,
	JumpBackward,
	JumpForwardIfFalse,
	JumpForwardIfTrue,
	PopAndJumpForwardIfFalse,
	RepeatLoopCheck,

	DebugPushFrame,
	DebugPopFrame,
	DebugPushType,
	DebugPopType,
	DebugPopTypeMultiple,
}