using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public enum OperationType
{
	MovRegMemToFromRegMask = 0,
	MovImmediateToReg,
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
	public OperationType type = OperationType.None;
	// MOD field
	public ModeType modeType = ModeType.MemoryNoDisplacement;

	public bool IsMemoryModeEnabled()
	{
		return modeType == ModeType.MemoryNoDisplacement ||
			modeType == ModeType.Memory8BitDisplacement ||
			modeType == ModeType.Memory16BitDisplacement;
	}

	// depending on the instruction, one or both may not be used
	public RegisterType destinationRegister = RegisterType.None;
	public RegisterType sourceRegister = RegisterType.None;

	// calculated from R/M field
	public EffectiveAddressType effectiveAddress = EffectiveAddressType.None;
	// 8-bit or 16-bit displacement value. may not be used
	public short displacement = 0;

	// D field (1 = REG is destination, 0 = REG is source)
	public bool bUseRegFieldAsDestination = false;
	// W field (1 = word, 0 = byte)
	public bool bIsWordOperation = false;
}

class InstructionBuilder
{
	// second byte
	const byte modMask = 0b1100_0000;
	const byte regMask = 0b0011_1000;
	const byte rmMask  = 0b0000_0111;

	byte[] bytes = new byte[6];
	int numBytesToRead = 1;
	int numBytesRead = 0;

	public void ReadFile(string inputFilename, ref Program program)
	{
		if (program == null)
		{
			return;
		}

		using (FileStream filestream = File.OpenRead(inputFilename))
		{
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
				DecodeInstructionFromFirstByte(bytes[0], ref instruction);

				if (instruction.type == OperationType.MovRegMemToFromRegMask)
				{
					int additionalBytesToRead = 1;
					int additionalBytesRead = filestream.Read(bytes, numBytesRead, additionalBytesToRead);

					if (additionalBytesRead == 1)
					{
						numBytesRead += additionalBytesRead;
						ParseSecondByteOfMovRegMemToFromRegMask(bytes[1], ref instruction);
					}

					additionalBytesToRead = 0;

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
						additionalBytesRead = 0;
						additionalBytesRead = filestream.Read(bytes, numBytesRead, additionalBytesToRead);

						if ((additionalBytesRead > 0) && (additionalBytesRead == additionalBytesToRead))
						{
							byte[] dispBytes = new byte[2];
							dispBytes[0] = bytes[numBytesRead];
							dispBytes[1] = bytes[numBytesRead+1];
							instruction.displacement = CalculateDisplacement(dispBytes, additionalBytesRead);
							numBytesRead += additionalBytesRead;
						}
					}

					program.AddInstruction(instruction);
				}
				else if (instruction.type == OperationType.MovImmediateToReg)
				{
					// TODO: implement me
				}

				ClearBytes();
				numBytesToRead = 1;
				numBytesRead = 0;
			}
		}
	}

	static short CalculateDisplacement(byte[] bytes, int size)
	{
		short displacement = 0;

		if (size == 1)
		{
			displacement = (short)BitConverter.ToChar(bytes);
		}
		else if (size == 2)
		{
			// On the 8086, the order of displacement is DISP-LO to DISP-HI,
			// in other words, little-endian.
			if (!BitConverter.IsLittleEndian)
			{
				byte[] reversedBytes = new byte[2];
				reversedBytes[0] = bytes[1];
				reversedBytes[1] = bytes[0];
				displacement = BitConverter.ToInt16(reversedBytes);
			}
			else
			{
				displacement = BitConverter.ToInt16(bytes);
			}
		}

		return displacement;
	}

	void ClearBytes()
	{
		for (int i = 0; i < bytes.Length; i++)
		{
			bytes[i] = 0;
		}
	}

	public void DecodeInstructionFromFirstByte(byte firstByte, ref Instruction instruction)
	{
		if (instruction == null)
		{
			return;
		}

		const byte movRegMemToFromRegMask = 0b1000_1000;
		const byte movImmediateToRegMask = 0b1011_0000;
		byte WFieldMask = 0b0000_0001;

		if ((firstByte & movRegMemToFromRegMask) == movRegMemToFromRegMask)
		{
			const byte DFieldMask = 0b0000_0010;
			instruction.bUseRegFieldAsDestination = (firstByte & DFieldMask) != 0;
			instruction.bIsWordOperation = (firstByte & WFieldMask) != 0;

			instruction.type = OperationType.MovRegMemToFromRegMask;
		}
		else if ((firstByte & movImmediateToRegMask) == movImmediateToRegMask)
		{
			WFieldMask = 0b0000_1000;
			instruction.bIsWordOperation = (firstByte & WFieldMask) != 0;
		}
	}

	public void ParseSecondByteOfMovRegMemToFromRegMask(byte secondByte, ref Instruction instruction)
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
		byte regValue = (byte)((secondByte & regMask) >> 3);
		byte rmValue = (byte)(secondByte & rmMask);
		RegisterType regField = ParseRegValue(regValue, ref instruction);
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
			if (instruction.bUseRegFieldAsDestination)
			{
				instruction.destinationRegister = regField;
			}
			else
			{
				instruction.sourceRegister = regField;
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
				output = "mov";
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

		if (instruction.type == OperationType.MovRegMemToFromRegMask)
		{

			if (instruction.modeType == ModeType.Register)
			{
				destination = ConvertRegisterToString(instruction.destinationRegister);
				source = ConvertRegisterToString(instruction.sourceRegister);
			}
			else if (instruction.IsMemoryModeEnabled())
			{
				if (instruction.bUseRegFieldAsDestination)
				{
					destination = ConvertRegisterToString(instruction.destinationRegister);
				}
				else
				{
					source = ConvertRegisterToString(instruction.sourceRegister);
				}
			}
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
