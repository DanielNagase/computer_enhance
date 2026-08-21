using System;
using System.IO;
using System.Collections.Generic;

class Program
{
	List<Instruction> instructions;

	// maps instruction pointer values to indices into the instructions List
	Dictionary<ushort, int> IPMapping;

	// size in bytes of all instructions
	ushort size = 0;

	public Program()
	{
		instructions = new List<Instruction>(10);
		IPMapping = new Dictionary<ushort, int>(10);
	}

	public void AddInstruction(Instruction instruction)
	{
		if (instruction.size == 0 || instructions == null)
		{
			return;
		}

		// We assume that we will never remove entries from the
		// instructions list, so it's enough to build it here.
		IPMapping.Add(size, instructions.Count);
		instructions.Add(instruction);
		size += instruction.size;
	}

	public bool GetInstructionForIPValue(ushort IPValue, ref Instruction instruction)
	{
		int index = 0;
		bool bDidFind = IPMapping.TryGetValue(IPValue, out index);

		if (bDidFind)
		{
			instruction = instructions[index];
		}

		return bDidFind;
	}

	public List<Instruction> Instructions { get => instructions; }

	public string Filename { get; set; }

	public ushort Size { get => size; }
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
		bool bDoesFileHaveFormatError = false;

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
				}
				else if (instruction.format == FormatType.OneByteWithImmediate)
				{
					ReadImmediateValue(filestream, ref numBytesRead, ref instruction);
				}
				else if (instruction.format == FormatType.OneByteWithIncrementToIP)
				{
					ReadIncrementValue(filestream, ref numBytesRead, ref instruction);
				}
				else
				{
					bDoesFileHaveFormatError = true;

					break;
				}

				instruction.size = (ushort)numBytesRead;
				program.AddInstruction(instruction);

				ClearBytes();
				numBytesToRead = 1;
				numBytesRead = 0;
			}
		}

		if (bDoesFileHaveFormatError)
		{
			throw new Exception($"Format error encountered in the file '{inputFilename}'!");
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
			source = $"{(ushort)instruction.operandTwo.Immediate.Value}";

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
			source = $"{(ushort)instruction.operandTwo.Immediate.Value}";
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
