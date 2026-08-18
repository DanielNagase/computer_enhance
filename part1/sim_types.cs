using System;

enum FormatType
{
	None = 0,
	TwoBytesWithDisplacement,
	TwoBytesWithDisplacementAndImmediate,
	OneByteWithImmediate,
	OneByteWithIncrementToIP
};

enum OperationType
{
	MovRegMemToFromRegMask = 0,
	MovImmediateToRegMem,
	MovImmediateToReg,
	AddRegMemWithRegToEither,
	AddImmediateToRegMem,
	AddImmediateToAccumulator,
	SubRegMemAndRegToEither,
	SubImmediateFromRegMem,
	SubImmediateFromAccumulator,
	CmpRegMemAndReg,
	CmpImmediateWithRegMem,
	CmpImmediateWithAccumulator,
	IncompleteNeedsOpcodeExtension,
	JumpOnEqual_Zero,
	JumpOnLess_NotGreaterOrEqual,
	JumpOnLessOrEqual_NotGreater,
	JumpOnBelow_NotAboveOrEqual,
	JumpOnBelowOrEqual_NotAbove,
	JumpOnParity_ParityEven,
	JumpOnOverflow,
	JumpOnSign,
	JumpOnNotEqual_NotZero,
	JumpOnNotLess_GreaterOrEqual,
	JumpOnNotLessOrEqual_Greater,
	JumpOnNotBelow_AboveOrEqual,
	JumpOnNotBelowOrEqual_Above,
	JumpOnNotPar_ParOdd,
	JumpOnNotOverflow,
	JumpOnNotSign,
	LoopCxTimes,
	LoopWhileZero_Equal,
	LoopWhileNotZero_Equal,
	JumpOnCxZero,
	None
};

enum ModeType
{
	MemoryNoDisplacement,
	Memory8BitDisplacement,
	Memory16BitDisplacement,
	Register
};

// Note: These will get converted to strings in
// ConvertRegisterToString, so keep them as two-letter codes.
enum RegisterType
{
	AL = 0, BL, CL, DL, AH, BH, CH, DH,
	AX, BX, CX, DX, SP, BP, SI, DI, None
};

struct RegisterAccess
{
	public RegisterType Index;
	public ushort Offset;
	// the count for the number of bytes we will access (always 1 or 2)
	public ushort Count;

	public RegisterAccess(RegisterType index, ushort offset, ushort count)
	{
		Index = index;
		Offset = offset;
		Count = count;
	}

	public static ushort GetCountForRegister(RegisterType index)
	{
		ushort count = 2;

		switch(index)
		{
			case RegisterType.AL:
			case RegisterType.BL:
			case RegisterType.CL:
			case RegisterType.DL:
			case RegisterType.AH:
			case RegisterType.BH:
			case RegisterType.CH:
			case RegisterType.DH:
				count = 1;
				break;
			default:
				break;
		}

		return count;
	}
}

struct EffectiveAddressTerm
{
	public RegisterAccess Register;
	public uint Scale;
}

struct EffectiveAddressExpression
{
	public EffectiveAddressTerm TermOne;
	public EffectiveAddressTerm TermTwo;
	public uint ExplicitSegment;
	public short Displacement;
	public uint Flags;
}

enum ImmediateFlag
{
	RelativeJumpDisplacement = 0x1
}

struct Immediate
{
	public short Value;
	public ushort Flags;
}

enum OperandType
{
	None,
	Register,
	Memory,
	Immediate
}

class InstructionOperand
{
	public OperandType Type = OperandType.None;

	// Ideally these would be combined into a union,
	// but this isn't supported in C# yet.
	public EffectiveAddressExpression Address = new EffectiveAddressExpression();
	public RegisterAccess Register = new RegisterAccess();
	public Immediate Immediate = new Immediate();

	public void SetAsRegister(RegisterType index)
	{
		SetAsRegister(index, RegisterAccess.GetCountForRegister(index));
	}

	public void SetAsRegister(RegisterType index, ushort count)
	{
		Type = OperandType.Register;
		Register.Offset = 0;
		Register.Count = count;
		Register.Index = index;
	}

	public void SetAsImmediate(short inValue, ushort inFlags = 0)
	{
		Type = OperandType.Immediate;
		Immediate.Value = inValue;
		Immediate.Flags = inFlags;
	}

	public void SetAsEffectiveAddress(RegisterAccess termOne, RegisterAccess termTwo,
									  short displacement)
	{
		Type = OperandType.Memory;
		Address.TermOne.Register = termOne;
		Address.TermOne.Scale = 1;
		Address.TermTwo.Register = termTwo;
		Address.TermTwo.Scale = 1;
		Address.Displacement = displacement;
	}
}
