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

class Instruction
{
	public FormatType format = FormatType.None;

	public OperationType type = OperationType.None;
	// MOD field
	public ModeType modeType = ModeType.MemoryNoDisplacement;

	public bool IsArithmeticInstruction()
	{
		return type == OperationType.AddRegMemWithRegToEither ||
			type == OperationType.AddImmediateToRegMem ||
			type == OperationType.AddImmediateToAccumulator ||
			type == OperationType.SubRegMemAndRegToEither ||
			type == OperationType.SubImmediateFromRegMem ||
			type == OperationType.SubImmediateFromAccumulator ||
			type == OperationType.CmpRegMemAndReg ||
			type == OperationType.CmpImmediateWithRegMem ||
			type == OperationType.CmpImmediateWithAccumulator;
	}

	public bool IsMemoryModeEnabled()
	{
		return modeType == ModeType.MemoryNoDisplacement ||
			modeType == ModeType.Memory8BitDisplacement ||
			modeType == ModeType.Memory16BitDisplacement;
	}

	public bool UsesAccumulator()
	{
		return type == OperationType.AddImmediateToAccumulator ||
			type == OperationType.SubImmediateFromAccumulator ||
			type == OperationType.CmpImmediateWithAccumulator;
	}

	public int NumberOfImmediateBytes()
	{
		int numberOfBytes = 0;
		int numberOfBytesBasedOnWord = bIsWordOperation ? 2 : 1;

		switch(type)
		{
			case OperationType.MovImmediateToRegMem:
			case OperationType.MovImmediateToReg:
			case OperationType.AddImmediateToAccumulator:
			case OperationType.SubImmediateFromAccumulator:
			case OperationType.CmpImmediateWithAccumulator:
				numberOfBytes = numberOfBytesBasedOnWord;
				break;
			case OperationType.AddImmediateToRegMem:
			case OperationType.SubImmediateFromRegMem:
			case OperationType.CmpImmediateWithRegMem:
				numberOfBytes = bUseSignExtensionForImmediate ? 1 : numberOfBytesBasedOnWord;
				break;
			default:
				numberOfBytes = 0;
				break;
		}

		return numberOfBytes;
	}

	public bool ShouldParseOpcodeExtension()
	{
		// TODO: while this format is the only one that we need to
		// handle for the homework, we may need to revise it in the
		// future
		return format == FormatType.TwoBytesWithDisplacementAndImmediate &&
			type == OperationType.IncompleteNeedsOpcodeExtension;
	}

	public bool CanUseRegField()
	{
		return NumberOfImmediateBytes() == 0;
	}

	public InstructionOperand operandOne = new InstructionOperand();
	public InstructionOperand operandTwo = new InstructionOperand();

	public InstructionOperand GetLastOperand()
	{
		InstructionOperand lastOperand = operandOne;

		if (lastOperand.Type != OperandType.None)
		{
			lastOperand = operandTwo;
		}

		return lastOperand;
	}

	// 8-bit or 16-bit immediate value. may not be used
	public short immediateValue = 0;

	// 8-bit increment value. may not be used
	public sbyte incrementValue = 0;

	// D field (1 = REG is destination, 0 = REG is source)
	public bool bUseRegFieldAsDestination = false;
	// W field (1 = word, 0 = byte)
	public bool bIsWordOperation = false;

	// S field (0 = no sign extension, 1 = sign extend 8-bit immediate
	// value to 16 bits if W field is 1)
	public bool bUseSignExtensionForImmediate = false;
}

class OpcodeLibrary
{
	class OpcodeDefinition
	{
		byte mask = 0;
		byte sequence = 0;
		OperationType type = OperationType.None;
		FormatType format = FormatType.None;

		public OperationType Type { get => type; }
		public FormatType Format { get => format; }

		public OpcodeDefinition(byte inMask, byte inSequence,
								OperationType inType, FormatType inFormat)
		{
			mask = inMask;
			sequence = inSequence;
			type = inType;
			format = inFormat;
		}

		public bool Matches(byte inputByte)
		{
			return (byte)(inputByte & mask) == sequence;
		}
	}

	const byte firstSixBitsMask = 0b1111_1100;
	const byte firstSevenBitsMask = 0b1111_1110;
	const byte allEightBitsMask = 0b1111_1111;

	OpcodeDefinition[] definitionList = new OpcodeDefinition[] {
		new OpcodeDefinition(firstSixBitsMask, 0b1000_1000, OperationType.MovRegMemToFromRegMask,
							 FormatType.TwoBytesWithDisplacement),
		new OpcodeDefinition(firstSevenBitsMask, 0b1100_0110, OperationType.MovImmediateToRegMem,
							 FormatType.TwoBytesWithDisplacementAndImmediate),
		new OpcodeDefinition(0b1111_0000, 0b1011_0000, OperationType.MovImmediateToReg,
							 FormatType.OneByteWithImmediate),
		new OpcodeDefinition(firstSixBitsMask, 0b0000_0000, OperationType.AddRegMemWithRegToEither,
							 FormatType.TwoBytesWithDisplacement),
		new OpcodeDefinition(firstSixBitsMask, 0b0010_1000, OperationType.SubRegMemAndRegToEither,
							 FormatType.TwoBytesWithDisplacement),
		new OpcodeDefinition(firstSixBitsMask, 0b0011_1000, OperationType.CmpRegMemAndReg,
							 FormatType.TwoBytesWithDisplacement),
		// The same byte sequence (the second parameter) is shared
		// among several operations such as add, sub, and cmp, so we
		// can't determine the operation type from just the first
		// byte.  Instead, we set the operation type to a placeholder
		// value and then set it correctly after we decode the opcode
		// extension in the second byte.
		new OpcodeDefinition(firstSixBitsMask, 0b1000_0000, OperationType.IncompleteNeedsOpcodeExtension,
							 FormatType.TwoBytesWithDisplacementAndImmediate),
		new OpcodeDefinition(firstSevenBitsMask, 0b0000_0100, OperationType.AddImmediateToAccumulator,
							 FormatType.OneByteWithImmediate),
		new OpcodeDefinition(firstSevenBitsMask, 0b0010_1100, OperationType.SubImmediateFromAccumulator,
							 FormatType.OneByteWithImmediate),
		new OpcodeDefinition(firstSevenBitsMask, 0b0011_1100, OperationType.CmpImmediateWithAccumulator,
							 FormatType.OneByteWithImmediate),
		new OpcodeDefinition(allEightBitsMask, 0b0111_0100, OperationType.JumpOnEqual_Zero,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_1100, OperationType.JumpOnLess_NotGreaterOrEqual,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_1110, OperationType.JumpOnLessOrEqual_NotGreater,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_0010, OperationType.JumpOnBelow_NotAboveOrEqual,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_0110, OperationType.JumpOnBelowOrEqual_NotAbove,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_1010, OperationType.JumpOnParity_ParityEven,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_0000, OperationType.JumpOnOverflow,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_1000, OperationType.JumpOnSign,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_0101, OperationType.JumpOnNotEqual_NotZero,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_1101, OperationType.JumpOnNotLess_GreaterOrEqual,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_1111, OperationType.JumpOnNotLessOrEqual_Greater,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_0011, OperationType.JumpOnNotBelow_AboveOrEqual,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_0111, OperationType.JumpOnNotBelowOrEqual_Above,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_1011, OperationType.JumpOnNotPar_ParOdd,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_0001, OperationType.JumpOnNotOverflow,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b0111_1001, OperationType.JumpOnNotSign,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b1110_0010, OperationType.LoopCxTimes,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b1110_0001, OperationType.LoopWhileZero_Equal,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b1110_0000, OperationType.LoopWhileNotZero_Equal,
							 FormatType.OneByteWithIncrementToIP),
		new OpcodeDefinition(allEightBitsMask, 0b1110_0011, OperationType.JumpOnCxZero,
							 FormatType.OneByteWithIncrementToIP),
	};

	void SetInstructionFromDefinition(ref Instruction instruction, OpcodeDefinition definition)
	{
		instruction.type = definition.Type;
		instruction.format = definition.Format;
	}

	public void LookupTypeAndFormat(byte firstByte, ref Instruction instruction)
	{
		bool bDidLookup = false;

		for (int i = 0; i < definitionList.Length; i++)
		{
			OpcodeDefinition definition = definitionList[i];

			if (definition == null || !definition.Matches(firstByte))
			{
				continue;
			}

			SetInstructionFromDefinition(ref instruction, definition);
			bDidLookup = true;

			break;
		}

		if (!bDidLookup)
		{
			throw new Exception("Couldn't look up instruction type!");
		}
	}
}
