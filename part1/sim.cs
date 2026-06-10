using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public enum FormatType
{
	None = 0,
	TwoBytesWithDisplacement,
	TwoBytesWithDisplacementAndImmediate,
	OneByteWithImmediate,
	OneByteWithIncrementToIP
};

public enum OperationType
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
	None
};

public enum ModeType
{
	MemoryNoDisplacement,
	Memory8BitDisplacement,
	Memory16BitDisplacement,
	Register
};

// Note: These will get converted to strings in
// ConvertRegisterToString, so keep them as two-letter codes.
public enum RegisterType
{
	AL, CL, DL, BL, AH, CH, DH, BH,
	AX, CX, DX, BX, SP, BP, SI, DI, None
};

public enum EffectiveAddressType
{
	BX_plus_SI, BX_plus_DI, BP_plus_SI, BP_plus_DI, SI, DI, DirectAddress, BP, BX, None
};

class Instruction
{
	public FormatType format = FormatType.None;

	public OperationType type = OperationType.None;
	// MOD field
	public ModeType modeType = ModeType.MemoryNoDisplacement;

	public bool IsMemoryModeEnabled()
	{
		return modeType == ModeType.MemoryNoDisplacement ||
			modeType == ModeType.Memory8BitDisplacement ||
			modeType == ModeType.Memory16BitDisplacement;
	}

	public int NumberOfImmediateBytes()
	{
		int numberOfBytes = 0;

		switch(type)
		{
			case OperationType.MovImmediateToRegMem:
			case OperationType.MovImmediateToReg:
				numberOfBytes = bIsWordOperation ? 2 : 1;
				break;
			default:
				numberOfBytes = 0;
				break;
		}

		return numberOfBytes;
	}

	public bool CanUseRegField()
	{
		return NumberOfImmediateBytes() == 0;
	}

	// depending on the instruction, one or both may not be used
	public RegisterType destinationRegister = RegisterType.None;
	public RegisterType sourceRegister = RegisterType.None;

	// calculated from R/M field
	public EffectiveAddressType effectiveAddress = EffectiveAddressType.None;
	// 8-bit or 16-bit displacement value. may not be used
	public short displacement = 0;

	// 8-bit or 16-bit immediate value. may not be used
	public short immediateValue = 0;

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

	OpcodeDefinition[] definitionList = new OpcodeDefinition[] {
		new OpcodeDefinition(0b1111_1100, 0b1000_1000, OperationType.MovRegMemToFromRegMask,
							 FormatType.TwoBytesWithDisplacement),
		new OpcodeDefinition(0b1111_1110, 0b1100_0110, OperationType.MovImmediateToRegMem,
							 FormatType.TwoBytesWithDisplacementAndImmediate),
		new OpcodeDefinition(0b1111_0000, 0b1011_0000, OperationType.MovImmediateToReg,
							 FormatType.OneByteWithImmediate),
		new OpcodeDefinition(0b1111_1100, 0b0000_0000, OperationType.AddRegMemWithRegToEither,
							 FormatType.TwoBytesWithDisplacement),
		// The same byte sequence (the second parameter) is shared
		// among several operations such as add, sub, and cmp, so we
		// can't determine the operation type from just the first
		// byte.  Instead, we set the operation type to a placeholder
		// value and then set it correctly after we decode the opcode
		// extension in the second byte.
		new OpcodeDefinition(0b1111_1100, 0b1000_0000, OperationType.IncompleteNeedsOpcodeExtension,
							 FormatType.TwoBytesWithDisplacementAndImmediate),
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

	public void ReadFile(string inputFilename, ref Program program)
	{
		if (program == null)
		{
			return;
		}

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
					DecodeSecondByteOfInstruction(bytes[1], ref instruction);
					ReadDisplacementValue(filestream, ref numBytesRead, ref instruction);

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

				ClearBytes();
				numBytesToRead = 1;
				numBytesRead = 0;
			}
		}
	}

	private void ReadDisplacementValue(FileStream filestream, ref int numBytesRead, ref Instruction instruction)
	{
		int additionalBytesToRead = 0;

		if (instruction.modeType == ModeType.Memory8BitDisplacement)
		{
			additionalBytesToRead = 1;
		}
		else if (instruction.modeType == ModeType.Memory16BitDisplacement ||
				 instruction.effectiveAddress == EffectiveAddressType.DirectAddress)
		{
			additionalBytesToRead = 2;
		}

		if (additionalBytesToRead > 0)
		{
			ReadByteValue(filestream, additionalBytesToRead, ref numBytesRead,
						  out short byteValue);
			instruction.displacement = byteValue;
		}
	}

	private void ReadImmediateValue(FileStream filestream, ref int numBytesRead, ref Instruction instruction)
	{
		int additionalBytesToRead = instruction.NumberOfImmediateBytes();
		ReadByteValue(filestream, additionalBytesToRead, ref numBytesRead,
					  out short byteValue);
		instruction.immediateValue = byteValue;
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
		}
		else if (instruction.format == FormatType.OneByteWithImmediate)
		{
			WFieldMask = 0b0000_1000;
			instruction.bIsWordOperation = (firstByte & WFieldMask) != 0;

			const byte regFieldMask = 0b0000_0111;
			byte regValue = (byte)(firstByte & regFieldMask);
			RegisterType regField = ParseRegValue(regValue, ref instruction);
			instruction.destinationRegister = regField;
		}
	}

	public void DecodeSecondByteOfInstruction(byte secondByte, ref Instruction instruction)
	{
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

		const byte rmMask  = 0b0000_0111;
		byte rmValue = (byte)(secondByte & rmMask);

		bool bIsMemoryModeEnabled = instruction.IsMemoryModeEnabled();

		if (instruction.modeType == ModeType.Register)
		{
			RegisterType rmField = ParseRegValue(rmValue, ref instruction);

			if (instruction.bUseRegFieldAsDestination)
			{
				instruction.destinationRegister = regField;
				instruction.sourceRegister = rmField;
			}
			else
			{
				instruction.sourceRegister = regField;
				instruction.destinationRegister = rmField;
			}
		}
		else if (bIsMemoryModeEnabled)
		{
			if (instruction.CanUseRegField())
			{
				if (instruction.bUseRegFieldAsDestination)
				{
					instruction.destinationRegister = regField;
				}
				else
				{
					instruction.sourceRegister = regField;
				}
			}
			else
			{
				// regField is not used in this case, so don't do anything with it
			}

			instruction.effectiveAddress = ParseMemValue(rmValue, instruction.modeType);
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
	EffectiveAddressType ParseMemValue(byte memValue, ModeType modeType)
	{
		EffectiveAddressType effectiveAddress = EffectiveAddressType.None;

		if (modeType == ModeType.Register)
		{
			return EffectiveAddressType.None;
		}

		switch(memValue)
		{
			case 0b0000_0000:
				effectiveAddress = EffectiveAddressType.BX_plus_SI;
				break;
			case 0b0000_0001:
				effectiveAddress = EffectiveAddressType.BX_plus_DI;
				break;
			case 0b0000_0010:
				effectiveAddress = EffectiveAddressType.BP_plus_SI;
				break;
			case 0b0000_0011:
				effectiveAddress = EffectiveAddressType.BP_plus_DI;
				break;
			case 0b0000_0100:
				effectiveAddress = EffectiveAddressType.SI;
				break;
			case 0b0000_0101:
				effectiveAddress = EffectiveAddressType.DI;
				break;
			case 0b0000_0110:
				effectiveAddress = EffectiveAddressType.BP;
				break;
			case 0b0000_0111:
				effectiveAddress = EffectiveAddressType.BX;
				break;
			default:
				break;
		}

		if ((effectiveAddress == EffectiveAddressType.BP) &&
			(modeType == ModeType.MemoryNoDisplacement))
		{
			effectiveAddress = EffectiveAddressType.DirectAddress;
		}

		return effectiveAddress;
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

	public void Print()
	{
		Console.WriteLine("bits 16");

		foreach (Instruction instruction in instructions)
		{
			PrintInstruction(instruction);
		}
	}

	static string CreateByteOrWordPrefix(bool bIsWord)
	{
		return bIsWord ? "word " : "byte ";
	}

	static string ConvertEffectiveAddressToString(EffectiveAddressType effectiveAddress, short displacement)
	{
		if (effectiveAddress == EffectiveAddressType.None)
		{
			return "";
		}

		string addressString= "";

		switch(effectiveAddress)
		{
			case EffectiveAddressType.BX_plus_SI:
				addressString = "bx + si";
				break;
			case EffectiveAddressType.BX_plus_DI:
				addressString = "bx + di";
				break;
			case EffectiveAddressType.BP_plus_SI:
				addressString = "bp + si";
				break;
			case EffectiveAddressType.BP_plus_DI:
				addressString = "bp + di";
				break;
			case EffectiveAddressType.SI:
				addressString = "si";
				break;
			case EffectiveAddressType.DI:
				addressString = "di";
				break;
			case EffectiveAddressType.DirectAddress:
				// handled below
				break;
			case EffectiveAddressType.BP:
				addressString = "bp";
				break;
			case EffectiveAddressType.BX:
				addressString = "bx";
				break;
			default:
				break;
		}

		string displacementString = "";

		if (displacement != 0)
		{
			string separator = " + ";

			if (effectiveAddress == EffectiveAddressType.DirectAddress)
			{
				separator = "";
			}

			displacementString = separator + displacement;
		}

		string output = $"[{addressString}{displacementString}]";

		return output;
	}

	static string ConvertRegisterToString(RegisterType register)
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
			default:
				break;
		}

		return output;
	}

	void PrintInstruction(Instruction instruction)
	{
		string output = "";
		string operation = ConvertOperationTypeToString(instruction.type);
		string source = "";
		string destination = "";

		if (instruction.format == FormatType.TwoBytesWithDisplacement)
		{
			if (instruction.modeType == ModeType.Register)
			{
				destination = ConvertRegisterToString(instruction.destinationRegister);
				source = ConvertRegisterToString(instruction.sourceRegister);
			}
			else if (instruction.IsMemoryModeEnabled())
			{
				string address = ConvertEffectiveAddressToString(instruction.effectiveAddress, instruction.displacement);

				if (instruction.bUseRegFieldAsDestination)
				{
					destination = ConvertRegisterToString(instruction.destinationRegister);
					source = address;
				}
				else
				{
					source = ConvertRegisterToString(instruction.sourceRegister);
					destination = address;
				}
			}
		}
		else if (instruction.type == OperationType.MovImmediateToRegMem)
		{
			source = $"{instruction.immediateValue}";

			if (instruction.modeType == ModeType.Register)
			{
				destination = ConvertRegisterToString(instruction.destinationRegister);
			}
			else if (instruction.IsMemoryModeEnabled())
			{
				// TODO: see if we need the prefix for other cases
				destination = CreateByteOrWordPrefix(instruction.bIsWordOperation);
				destination +=
					ConvertEffectiveAddressToString(instruction.effectiveAddress,
													instruction.displacement);
			}
		}
		else if (instruction.type == OperationType.MovImmediateToReg)
		{
			destination = ConvertRegisterToString(instruction.destinationRegister);
			source = $"{instruction.immediateValue}";
		}

		output = $"{operation} {destination}, {source}";
		Console.WriteLine(output);
	}
}

class Sim
{
	static void ExitProgramWithError(int exitCode, string errorMessage)
	{
		Console.Error.WriteLine(errorMessage);
		System.Environment.Exit(exitCode);
	}

	static void CheckArguments(string[] args, out string inputFilename)
	{
		const int argumentsErrorExitCode = 1;

		if (args.Length != 1)
		{
			ExitProgramWithError(argumentsErrorExitCode,
								 "Please provide exactly one argument!");
		}

		inputFilename = args[0];

		if (!File.Exists(inputFilename))
		{
			ExitProgramWithError(argumentsErrorExitCode,
								 $"Filename '{inputFilename}' does not exist!");
		}
	}

	static void Main(string[] args)
    {
		string inputFilename;
		CheckArguments(args, out inputFilename);

		InstructionBuilder builder = new InstructionBuilder();
		Program program = new Program();
		builder.ReadFile(inputFilename, ref program);

		program.Print();
    }
}
