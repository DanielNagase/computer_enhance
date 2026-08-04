using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

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

class InstructionBuilder
{
	byte[] bytes = new byte[6];
	OpcodeLibrary library = new OpcodeLibrary();

	public void ReadFile(string inputFilename, Program program)
	{
		if (program == null)
		{
			return;
		}

		program.Filename = inputFilename;

		using (FileStream filestream = File.OpenRead(inputFilename))
		{
			int numBytesToRead = 1;
			int numBytesRead = 0;

			while (numBytesToRead > 0)
			{
				int numCurrentBytesRead = filestream.Read(bytes, numBytesRead, numBytesToRead);

				if (numCurrentBytesRead == 0)
				{
					break;
				}

				numBytesRead += numCurrentBytesRead;
				numBytesToRead -= numCurrentBytesRead;
				Instruction instruction = new Instruction();
				DecodeFirstByteOfInstruction(bytes[0], ref instruction);

				if (instruction.format == FormatType.TwoBytesWithDisplacement ||
					instruction.format == FormatType.TwoBytesWithDisplacementAndImmediate)
				{
					int additionalBytesToRead = 1;
					int additionalBytesRead = filestream.Read(bytes, numBytesRead, additionalBytesToRead);
					numBytesRead += additionalBytesRead;
					bool bHasDirectAddress = false;
					DecodeSecondByteOfInstruction(bytes[1], ref instruction,
												  out bHasDirectAddress);
					ReadDisplacementValue(filestream, ref numBytesRead, ref instruction,
										  bHasDirectAddress);

					if (instruction.NumberOfImmediateBytes() > 0)
					{
						ReadImmediateValue(filestream, ref numBytesRead, ref instruction);
					}

					program.AddInstruction(instruction);
				}
				else if (instruction.format == FormatType.OneByteWithImmediate)
				{
					ReadImmediateValue(filestream, ref numBytesRead, ref instruction);
					program.AddInstruction(instruction);
				}
				else if (instruction.format == FormatType.OneByteWithIncrementToIP)
				{
					ReadIncrementValue(filestream, ref numBytesRead, ref instruction);
					program.AddInstruction(instruction);
				}

				ClearBytes();
				numBytesToRead = 1;
				numBytesRead = 0;
			}
		}
	}

	private void ReadDisplacementValue(FileStream filestream, ref int numBytesRead,
									   ref Instruction instruction, bool bHasDirectAddress)
	{
		int additionalBytesToRead = 0;

		if (instruction.modeType == ModeType.Memory8BitDisplacement)
		{
			additionalBytesToRead = 1;
		}
		else if (instruction.modeType == ModeType.Memory16BitDisplacement ||
				 bHasDirectAddress)
		{
			additionalBytesToRead = 2;
		}

		if (additionalBytesToRead > 0)
		{
			ReadByteValue(filestream, additionalBytesToRead, ref numBytesRead,
						  out short byteValue);

			InstructionOperand operand =
				(instruction.operandOne.Type == OperandType.Memory) ?
				instruction.operandOne : instruction.operandTwo;
			operand.Address.Displacement = byteValue;
		}
	}

	void ReadIncrementValue(FileStream filestream, ref int numBytesRead, ref Instruction instruction)
	{
		int additionalBytesToRead = 1;
		ReadByteValue(filestream, additionalBytesToRead, ref numBytesRead,
					  out short byteValue);
		// While we read the value into a short, the increment value
		// is a signed 8-bit integer, so we must cast it to an SByte
		instruction.incrementValue = (sbyte)byteValue;

		InstructionOperand lastOperand = instruction.GetLastOperand();
		lastOperand.SetAsImmediate(byteValue, (ushort)ImmediateFlag.RelativeJumpDisplacement);
	}

	private void ReadImmediateValue(FileStream filestream, ref int numBytesRead, ref Instruction instruction)
	{
		int additionalBytesToRead = instruction.NumberOfImmediateBytes();
		ReadByteValue(filestream, additionalBytesToRead, ref numBytesRead,
					  out short byteValue);
		instruction.immediateValue = byteValue;

		InstructionOperand lastOperand = instruction.GetLastOperand();
		lastOperand.SetAsImmediate(byteValue);
	}

	private void ReadByteValue(FileStream filestream, int additionalBytesToRead,
							   ref int numBytesRead, out short byteValue)
	{
		byteValue = 0;

		if (filestream == null || !filestream.CanRead)
		{
			return;
		}

		int additionalBytesRead = filestream.Read(bytes, numBytesRead, additionalBytesToRead);

		if ((additionalBytesRead > 0) && (additionalBytesRead == additionalBytesToRead))
		{
			ReadOnlySpan<byte> valueBytes = new ReadOnlySpan<byte>(bytes, numBytesRead, additionalBytesRead);
			numBytesRead += additionalBytesRead;
			byteValue = ParseByteValue(valueBytes);
		}
	}

	// Read an 8-bit value from one byte or a 16-bit value from two
	// bytes. Some of the mov variants use this type of byte sequence
	// to store values like displacement values (DISP-LO and DISP-HI)
	// or immediate constants.
	static short ParseByteValue(ReadOnlySpan<byte> bytes)
	{
		short numberValue = 0;

		if (bytes.Length == 1)
		{
			numberValue = (short)bytes[0];
		}
		else if (bytes.Length == 2)
		{
			// On the 8086, the order of displacement is DISP-LO to
			// DISP-HI, and similarly DATA-LO to DATA-HI for immediate
			// constants. In other words, it's little-endian.
			if (!BitConverter.IsLittleEndian)
			{
				Span<byte> reversedBytes = new Span<byte>(bytes.ToArray());
				reversedBytes.Reverse();
				numberValue = BitConverter.ToInt16(reversedBytes);
			}
			else
			{
				numberValue = BitConverter.ToInt16(bytes);
			}
		}

		return numberValue;
	}

	void ClearBytes()
	{
		for (int i = 0; i < bytes.Length; i++)
		{
			bytes[i] = 0;
		}
	}

	public void DecodeFirstByteOfInstruction(byte firstByte, ref Instruction instruction)
	{
		if (instruction == null)
		{
			return;
		}

		library.LookupTypeAndFormat(firstByte, ref instruction);
		byte WFieldMask = 0b0000_0001;

		if (instruction.format == FormatType.TwoBytesWithDisplacement)
		{
			const byte DFieldMask = 0b0000_0010;
			instruction.bUseRegFieldAsDestination = (firstByte & DFieldMask) != 0;
			instruction.bIsWordOperation = (firstByte & WFieldMask) != 0;
		}
		else if (instruction.format == FormatType.TwoBytesWithDisplacementAndImmediate)
		{
			instruction.bIsWordOperation = (firstByte & WFieldMask) != 0;

			if (instruction.ShouldParseOpcodeExtension())
			{
				const byte SFieldMask = 0b0000_0010;
				instruction.bUseSignExtensionForImmediate = (firstByte & SFieldMask) != 0;
			}
		}
		else if (instruction.format == FormatType.OneByteWithImmediate)
		{
			if (instruction.type == OperationType.MovImmediateToReg)
			{
				WFieldMask = 0b0000_1000;
				instruction.bIsWordOperation = (firstByte & WFieldMask) != 0;

				const byte regFieldMask = 0b0000_0111;
				byte regValue = (byte)(firstByte & regFieldMask);
				RegisterType regField = ParseRegValue(regValue, ref instruction);
				instruction.operandOne.SetAsRegister(regField);
			}
			else
			{
				instruction.bIsWordOperation = (firstByte & WFieldMask) != 0;

				if (instruction.UsesAccumulator())
				{
					RegisterType accumulator =
						instruction.bIsWordOperation ? RegisterType.AX : RegisterType.AL;
					instruction.operandOne.SetAsRegister(accumulator);
				}
			}
		}
		else if (instruction.format == FormatType.OneByteWithIncrementToIP)
		{
			// no actual work to do
		}
	}

	public void DecodeSecondByteOfInstruction(byte secondByte, ref Instruction instruction,
											  out bool bHasDirectAddress)
	{
		bHasDirectAddress = false;

		if (instruction == null)
		{
			return;
		}

		ParseModValue(secondByte, ref instruction);
		// Within the second byte, the reg value is bits three through
		// five. After extracting the value, we shift it right by
		// three bits so it occupies the three least significant bits
		// of a byte.
		const byte regMask = 0b0011_1000;
		byte regValue = (byte)((secondByte & regMask) >> 3);
		RegisterType regField = ParseRegValue(regValue, ref instruction);

		if (instruction.ShouldParseOpcodeExtension())
		{
			instruction.type = ParseOpcodeExtension(regValue);
		}

		const byte rmMask  = 0b0000_0111;
		byte rmValue = (byte)(secondByte & rmMask);

		bool bIsMemoryModeEnabled = instruction.IsMemoryModeEnabled();
		InstructionOperand regOperand = instruction.operandTwo;
		InstructionOperand modOperand = instruction.operandOne;

		if (instruction.bUseRegFieldAsDestination)
		{
			regOperand = instruction.operandOne;
			modOperand = instruction.operandTwo;
		}

		if (instruction.modeType == ModeType.Register)
		{
			RegisterType rmField = ParseRegValue(rmValue, ref instruction);
			regOperand.SetAsRegister(regField);
			modOperand.SetAsRegister(rmField);
		}
		else if (bIsMemoryModeEnabled)
		{
			if (instruction.CanUseRegField())
			{
				regOperand.SetAsRegister(regField);
			}

			ParseMemValue(rmValue, instruction.modeType, ref modOperand,
						  out bHasDirectAddress);
		}
	}

	void ParseModValue(byte secondByte, ref Instruction instruction)
	{
		if (instruction == null)
		{
			return;
		}

		const byte modMask = 0b1100_0000;
		byte modValue = (byte)(secondByte & modMask);

		switch(modValue)
		{
			case 0b0000_0000:
				instruction.modeType = ModeType.MemoryNoDisplacement;
				break;
			case 0b0100_0000:
				instruction.modeType = ModeType.Memory8BitDisplacement;
				break;
			case 0b1000_0000:
				instruction.modeType = ModeType.Memory16BitDisplacement;
				break;
			case 0b1100_0000:
				instruction.modeType = ModeType.Register;
				break;
		}
	}

	// Parse the least three significant bits as an opcode extension
	// value.
	OperationType ParseOpcodeExtension(byte opcodeExtension)
	{
		OperationType operationType = OperationType.None;

		switch(opcodeExtension)
		{
			case 0b0000_0000:
				operationType = OperationType.AddImmediateToRegMem;
				break;
			case 0b0000_0101:
				operationType = OperationType.SubImmediateFromRegMem;
				break;
			case 0b0000_0111:
				operationType = OperationType.CmpImmediateWithRegMem;
				break;
			default:
				break;
		}

		return operationType;
	}

	// Parse the least three significant bits as a registry value.
	// The REG and R/M fields use the same three-bit field encoding.
	// The first parameter (regValue) is a three-bit sequence from
	// either of those fields, shifted so that sequence occupies the
	// three least significant bits.
	RegisterType ParseRegValue(byte regValue, ref Instruction instruction)
	{
		RegisterType register = RegisterType.None;

		if (instruction == null)
		{
			return register;
		}

		if (instruction.bIsWordOperation)
		{
			switch(regValue)
			{
				case 0b0000_0000:
					register = RegisterType.AX;
					break;
				case 0b0000_0001:
					register = RegisterType.CX;
					break;
				case 0b0000_0010:
					register = RegisterType.DX;
					break;
				case 0b0000_0011:
					register = RegisterType.BX;
					break;
				case 0b0000_0100:
					register = RegisterType.SP;
					break;
				case 0b0000_0101:
					register = RegisterType.BP;
					break;
				case 0b0000_0110:
					register = RegisterType.SI;
					break;
				case 0b0000_0111:
					register = RegisterType.DI;
					break;
				default:
					break;
			}
		}
		else
		{
			switch(regValue)
			{
				case 0b0000_0000:
					register = RegisterType.AL;
					break;
				case 0b0000_0001:
					register = RegisterType.CL;
					break;
				case 0b0000_0010:
					register = RegisterType.DL;
					break;
				case 0b0000_0011:
					register = RegisterType.BL;
					break;
				case 0b0000_0100:
					register = RegisterType.AH;
					break;
				case 0b0000_0101:
					register = RegisterType.CH;
					break;
				case 0b0000_0110:
					register = RegisterType.DH;
					break;
				case 0b0000_0111:
					register = RegisterType.BH;
					break;
				default:
					break;
			}
		}

		return register;
	}

	// Parse memValue into an effective address calculation. The
	// memValue parameter is a three-bit sequence that occupies the
	// three least significant bits.
	void ParseMemValue(byte memValue, ModeType modeType,
					   ref InstructionOperand modOperand, out bool bHasDirectAddress)
	{
		bHasDirectAddress = false;

		if (modeType == ModeType.Register)
		{
			modOperand.SetAsEffectiveAddress(new RegisterAccess(RegisterType.None, 0, 2),
											 new RegisterAccess(RegisterType.None, 0, 2),
											 0);

			return;
		}

		RegisterType termOne = RegisterType.None;
		RegisterType termTwo = RegisterType.None;

		RegisterType[] termOneValues = new RegisterType[] {
			RegisterType.BX, RegisterType.BX, RegisterType.BP, RegisterType.BP,
			RegisterType.SI, RegisterType.DI, RegisterType.BP, RegisterType.BX};

		RegisterType[] termTwoValues = new RegisterType[] {
			RegisterType.SI, RegisterType.DI, RegisterType.SI, RegisterType.DI,
			RegisterType.None, RegisterType.None, RegisterType.None, RegisterType.None};

		if (memValue < termOneValues.Length)
		{
			termOne = termOneValues[memValue];
		}

		if (memValue < termTwoValues.Length)
		{
			termTwo = termTwoValues[memValue];
		}

		if ((memValue == 0b0000_0110) &&
			(modeType == ModeType.MemoryNoDisplacement))
		{
			// direct address
			bHasDirectAddress = true;
			termOne = RegisterType.None;
			termTwo = RegisterType.None;
		}

		// displacement will get filled in later, so set to zero here
		modOperand.SetAsEffectiveAddress(new RegisterAccess(termOne, 0, 2),
										 new RegisterAccess(termTwo, 0, 2),
										 0);
	}
}

class Program
{
	List<Instruction> instructions;

	public Program()
	{
		instructions = new List<Instruction>(10);
	}

	public void AddInstruction(Instruction instruction)
	{
		instructions?.Add(instruction);
	}

	public List<Instruction> Instructions { get => instructions; }

	public string Filename { get; set; }
}

class Decoder
{
	public void Print(Program program)
	{
		Console.WriteLine($"; {program.Filename} disassembly:");
		Console.WriteLine("bits 16");
		List<Instruction> instructions = program.Instructions;

		foreach (Instruction instruction in instructions)
		{
			Console.WriteLine(InstructionFormatter.ConvertInstructionToString(instruction));
		}
	}
}

class Simulator
{
	const int registerCount = 8;
	short[] registers = new short[registerCount];

	struct FlagSet
	{
		public bool bSignFlag;
		public bool bZeroFlag;

		public void Initialize()
		{
			bSignFlag = false;
			bZeroFlag = false;
		}
	}

	FlagSet flags;

	struct FlagSetUpdate
	{
		public FlagSet previousValue;
		public FlagSet newValue;
	}

	FlagSetUpdate lastFlagsUpdate;

	class RegisterUpdate
	{
		public RegisterType register = RegisterType.None;
		public int previousValue = 0;
		public int newValue = 0;
	}

	RegisterUpdate lastUpdate = new RegisterUpdate();

	int GetIndex(RegisterType register)
	{
		// TODO: handle values below AX (low / high)
		int index = (int)register - (int)RegisterType.AX;
		index = Math.Clamp(index, 0, registerCount);

		return index;
	}

	short GetRegisterValue(RegisterType register)
	{
		return registers[GetIndex(register)];
	}

	void SetRegisterValue(RegisterType register, short newValue)
	{
		RecordUpdate(register, newValue);
		registers[GetIndex(register)] = newValue;
	}

	void RecordUpdate(RegisterType register, short newValue)
	{
		lastUpdate.register = register;
		lastUpdate.previousValue = GetRegisterValue(register);
		lastUpdate.newValue = newValue;
	}

	void SetFlags(FlagSet newFlags)
	{
		RecordFlagsUpdate(newFlags);
		flags = newFlags;
	}

	void RecordFlagsUpdate(FlagSet newFlags)
	{
		lastFlagsUpdate.previousValue = flags;
		lastFlagsUpdate.newValue = newFlags;
	}

	short GetOperandValue(InstructionOperand operand)
	{
		short operandValue = 0;

		if (operand.Type == OperandType.Register)
		{
			if (operand.Register.Index != RegisterType.None)
			{
				operandValue = GetRegisterValue(operand.Register.Index);
			}
		}
		else if (operand.Type == OperandType.Immediate)
		{
			operandValue = operand.Immediate.Value;
		}
		// TODO: handle OperandType.Memory

		return operandValue;
	}

	void PerformInstruction(Instruction instruction)
	{
		short sourceValue = 0;

		if (instruction.format == FormatType.OneByteWithImmediate)
		{
			sourceValue = instruction.operandTwo.Immediate.Value;
		}

		if (instruction.type == OperationType.MovImmediateToReg)
		{
			SetRegisterValue(instruction.operandOne.Register.Index, sourceValue);
		}
		else if (instruction.type == OperationType.MovRegMemToFromRegMask)
		{
			// note: only reg to reg moves are handled right now
			if (instruction.modeType == ModeType.Register)
			{
				if (instruction.operandTwo.Type == OperandType.Register &&
					instruction.operandTwo.Register.Index != RegisterType.None)
				{
					sourceValue = GetRegisterValue(instruction.operandTwo.Register.Index);
				}

				SetRegisterValue(instruction.operandOne.Register.Index, sourceValue);
			}
		}
	}

	void InitializeRegisters()
	{
		for (int i = 0; i < registerCount; i++)
		{
			registers[i] = 0;
		}
	}

	public void Execute(Program program)
	{
		InitializeRegisters();
		flags.Initialize();
		Console.WriteLine($"--- {program.Filename} execution ---");

		List<Instruction> instructions = program.Instructions;

		foreach (Instruction instruction in instructions)
		{
			PerformInstruction(instruction);
			string instructionString =
				InstructionFormatter.ConvertInstructionToString(instruction);
			Console.WriteLine(instructionString + " ; " + GetLastUpdateString());
		}
	}

	public void PrintState()
	{
		Console.WriteLine("");
		Console.WriteLine("Final registers:");
		string registerName = "";
		int index = 0, registerValue = 0;

		for (int i = 0; i < registerCount; i++)
		{
			index = (int)RegisterType.AX + i;
			registerValue = registers[i];
			registerName = InstructionFormatter.ConvertRegisterToString((RegisterType)index);
			string formatString = $"  {registerName}: 0x{registerValue:x4} ({registerValue})";
			Console.WriteLine(formatString);
		}
	}

	string GetLastUpdateString()
	{
		string destination = InstructionFormatter.ConvertRegisterToString(lastUpdate.register);
		return $"{destination}:0x{lastUpdate.previousValue:x4}->0x{lastUpdate.newValue:x4}";
	}
}

class InstructionFormatter
{
	static string CreateByteOrWordPrefix(bool bIsWord)
	{
		return bIsWord ? "word " : "byte ";
	}

	static string ConvertEffectiveAddressToString(Instruction instruction)
	{
		InstructionOperand operand =
				(instruction.operandOne.Type == OperandType.Memory) ?
				instruction.operandOne : instruction.operandTwo;

		if (operand.Type != OperandType.Memory)
		{
			return "";
		}

		string addressString = "";
		string displacementString = "";
		string separator = "";
		EffectiveAddressTerm[] terms = new EffectiveAddressTerm[2] {
			operand.Address.TermOne,
			operand.Address.TermTwo
		};

		for (int i = 0; i < terms.Length; i++)
		{
			EffectiveAddressTerm term = terms[i];
			RegisterAccess register = term.Register;

			if (register.Index != RegisterType.None)
			{
				addressString += separator;

				if (term.Scale != 1)
				{
					addressString += $"{term.Scale}*";
				}

				addressString += ConvertRegisterToString(register.Index);
				separator = " + ";
			}
		}

		if (operand.Address.Displacement != 0)
		{
			displacementString = operand.Address.Displacement.ToString(" + ##; - ##");
		}

		string output = $"[{addressString}{displacementString}]";

		return output;
	}

	static string ConvertIPIncrementToString(sbyte increment)
	{
		string incrementString = "";
		int effectiveValue = increment + 2; // instruction size is 2

		if (effectiveValue > 0)
		{
			incrementString = $"+{effectiveValue}+0";
		}
		else if (effectiveValue == 0)
		{
			incrementString = $"+0";
		}
		else
		{
			incrementString = $"{effectiveValue}+0";
		}

		return incrementString;
	}

	public static string ConvertRegisterToString(RegisterType register)
	{
		return Enum.GetName(typeof(RegisterType), register).ToLower();
	}

	static string ConvertOperationTypeToString(OperationType operation)
	{
		string output = "";

		switch(operation)
		{
			case OperationType.MovRegMemToFromRegMask:
			case OperationType.MovImmediateToReg:
			case OperationType.MovImmediateToRegMem:
				output = "mov";
				break;
			case OperationType.AddRegMemWithRegToEither:
			case OperationType.AddImmediateToRegMem:
			case OperationType.AddImmediateToAccumulator:
				output = "add";
				break;
			case OperationType.SubRegMemAndRegToEither:
			case OperationType.SubImmediateFromRegMem:
			case OperationType.SubImmediateFromAccumulator:
				output = "sub";
				break;
			case OperationType.CmpRegMemAndReg:
			case OperationType.CmpImmediateWithRegMem:
			case OperationType.CmpImmediateWithAccumulator:
				output = "cmp";
				break;
			case OperationType.JumpOnEqual_Zero:
				output = "je";
				break;
			case OperationType.JumpOnLess_NotGreaterOrEqual:
				output = "jl";
				break;
			case OperationType.JumpOnLessOrEqual_NotGreater:
				output = "jle";
				break;
			case OperationType.JumpOnBelow_NotAboveOrEqual:
				output = "jb";
				break;
			case OperationType.JumpOnBelowOrEqual_NotAbove:
				output = "jbe";
				break;
			case OperationType.JumpOnParity_ParityEven:
				output = "jp";
				break;
			case OperationType.JumpOnOverflow:
				output = "jo";
				break;
			case OperationType.JumpOnSign:
				output = "js";
				break;
			case OperationType.JumpOnNotEqual_NotZero:
				output = "jne";
				break;
			case OperationType.JumpOnNotLess_GreaterOrEqual:
				output = "jnl";
				break;
			case OperationType.JumpOnNotLessOrEqual_Greater:
				output = "jnle";
				break;
			case OperationType.JumpOnNotBelow_AboveOrEqual:
				output = "jnb";
				break;
			case OperationType.JumpOnNotBelowOrEqual_Above:
				output = "jnbe";
				break;
			case OperationType.JumpOnNotPar_ParOdd:
				output = "jnp";
				break;
			case OperationType.JumpOnNotOverflow:
				output = "jno";
				break;
			case OperationType.JumpOnNotSign:
				output = "jns";
				break;
			case OperationType.LoopCxTimes:
				output = "loop";
				break;
			case OperationType.LoopWhileZero_Equal:
				output = "loopz";
				break;
			case OperationType.LoopWhileNotZero_Equal:
				output = "loopnz";
				break;
			case OperationType.JumpOnCxZero:
				output = "jcxz";
				break;
			default:
				break;
		}

		return output;
	}

	public static string ConvertInstructionToString(Instruction instruction)
	{
		string output = "";
		string operation = ConvertOperationTypeToString(instruction.type);
		string source = "";
		string destination = "";

		if (instruction.format == FormatType.TwoBytesWithDisplacement)
		{
			if (instruction.modeType == ModeType.Register)
			{
				destination = ConvertRegisterToString(instruction.operandOne.Register.Index);
				source = ConvertRegisterToString(instruction.operandTwo.Register.Index);
			}
			else if (instruction.IsMemoryModeEnabled())
			{
				string address = ConvertEffectiveAddressToString(instruction);

				if (instruction.bUseRegFieldAsDestination)
				{
					destination = ConvertRegisterToString(instruction.operandOne.Register.Index);
					source = address;
				}
				else
				{
					source = ConvertRegisterToString(instruction.operandTwo.Register.Index);
					destination = address;
				}
			}
		}
		else if (instruction.format == FormatType.TwoBytesWithDisplacementAndImmediate)
		{
			source = $"{instruction.operandTwo.Immediate.Value}";

			if (instruction.modeType == ModeType.Register)
			{
				destination = ConvertRegisterToString(instruction.operandOne.Register.Index);
			}
			else if (instruction.IsMemoryModeEnabled())
			{
				// TODO: see if we need the prefix for other cases
				destination = CreateByteOrWordPrefix(instruction.bIsWordOperation);
				destination += ConvertEffectiveAddressToString(instruction);
			}
		}
		else if (instruction.format == FormatType.OneByteWithImmediate)
		{
			destination = ConvertRegisterToString(instruction.operandOne.Register.Index);
			source = $"{instruction.operandTwo.Immediate.Value}";
		}

		output = $"{operation} {destination}, {source}";

		if (instruction.format == FormatType.OneByteWithIncrementToIP)
		{
			destination = ConvertIPIncrementToString(instruction.incrementValue);
			output = $"{operation} ${destination} ; {instruction.incrementValue}";
		}

		return output;
	}
}

class Application
{
	enum OperationMode
	{
		Decode = 0,
		Execute
	};

	static void ExitProgramWithError(int exitCode, string errorMessage)
	{
		Console.Error.WriteLine(errorMessage);
		System.Environment.Exit(exitCode);
	}

	static void CheckArguments(string[] args, out string inputFilename,
							   out OperationMode operationMode)
	{
		const int argumentsErrorExitCode = 1;

		if (args.Length < 1)
		{
			ExitProgramWithError(argumentsErrorExitCode,
								 "Missing filename argument!");
		}

		inputFilename = args[0];
		operationMode = OperationMode.Decode;

		const string executeModeString = "-exec";

		if (args[0] == executeModeString)
		{
			if (args.Length == 2)
			{
				inputFilename = args[1];
				operationMode = OperationMode.Execute;
			}
			else if (args.Length == 1)
			{
				ExitProgramWithError(argumentsErrorExitCode,
									 "Missing filename argument!");
			}
		}

		if (!File.Exists(inputFilename))
		{
			ExitProgramWithError(argumentsErrorExitCode,
								 $"Filename '{inputFilename}' does not exist!");
		}
	}

	static void Main(string[] args)
    {
		string inputFilename;
		OperationMode operationMode = OperationMode.Decode;
		CheckArguments(args, out inputFilename, out operationMode);

		InstructionBuilder builder = new InstructionBuilder();
		Program program = new Program();
		builder.ReadFile(inputFilename, program);

		if (operationMode == OperationMode.Decode)
		{
			Decoder decoder = new Decoder();
			decoder.Print(program);
		}
		else if (operationMode == OperationMode.Execute)
		{
			Simulator simulator = new Simulator();
			simulator.Execute(program);
			simulator.PrintState();
		}
    }
}
