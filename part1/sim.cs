using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public enum OperationType
{
	Mov = 0,
	None
};

public enum ModeType
{
	MemoryNoDisplacement,
	Memory8BitDisplacement,
	Memory16BitDisplacement,
	Register
};

class Instruction
{
	public OperationType type = OperationType.None;
	// MOD field
	public ModeType modeType = ModeType.MemoryNoDisplacement;

	// D field (1 = REG is destination, 0 = REG is source)
	public bool bUseRegFieldAsDestination = false;
	// W field (1 = word, 0 = byte)
	public bool bIsWordOperation = false;
}

class InstructionBuilder
{
	// first byte
	const byte movMask    = 0b1000_1000;
	const byte DFieldMask = 0b0000_0010;
	const byte WFieldMask = 0b0000_0001;
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
				Build(bytes[0], ref instruction);

				if (instruction.type == OperationType.Mov)
				{
					numBytesToRead += 1;
					int bytesRead = filestream.Read(bytes, numBytesRead, numBytesToRead);
					if (bytesRead == 1)
					{
						ParseSecondByte(bytes[1], ref instruction);
					}

					program.AddInstruction(instruction);
				}

				ClearBytes();
				numBytesToRead = 1;
				numBytesRead = 0;
			}
		}
	}

	void ClearBytes()
	{
		for (int i = 0; i < bytes.Length; i++)
		{
			bytes[i] = 0;
		}
	}

	public void Build(byte firstByte, ref Instruction instruction)
	{
		if (instruction == null)
		{
			return;
		}

		instruction.bUseRegFieldAsDestination = (firstByte & DFieldMask) != 0;
		instruction.bIsWordOperation = (firstByte & WFieldMask) != 0;

		if ((firstByte & movMask) == movMask)
		{
			instruction.type = OperationType.Mov;
		}
	}

	public void ParseSecondByte(byte secondByte, ref Instruction instruction)
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

	void PrintInstruction(Instruction instruction)
	{
		if (instruction.type == OperationType.Mov)
		{
			// TODO: properly print operands
			Console.WriteLine("mov x, x");
		}
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
