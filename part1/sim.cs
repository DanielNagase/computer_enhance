using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

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
	ushort[] registers = new ushort[registerCount];

	struct FlagSet
	{
		public bool bSignFlag;
		public bool bZeroFlag;

		public void Initialize()
		{
			bSignFlag = false;
			bZeroFlag = false;
		}

		public bool IsAnyFlagSet()
		{
			return bSignFlag || bZeroFlag;
		}

		public override string ToString()
		{
			string output = "";

			if (bSignFlag)
			{
				output += "S";
			}

			if (bZeroFlag)
			{
				output += "Z";
			}

			return output;
		}
	}

	FlagSet flags;

	struct FlagSetUpdate
	{
		public FlagSet previousValue;
		public FlagSet newValue;

		public bool DidChange()
		{
			return !previousValue.Equals(newValue);
		}
	}

	FlagSetUpdate lastFlagsUpdate;

	struct RegisterUpdate
	{
		public RegisterType register;
		public ushort previousValue;
		public ushort newValue;

		public void Initialize()
		{
			register = RegisterType.None;
			previousValue = 0;
			newValue = 0;
		}

		public bool DidChange()
		{
			return (register != RegisterType.None) &&
				(previousValue != newValue);
		}
	}

	RegisterUpdate lastUpdate = new RegisterUpdate();

	int GetIndex(RegisterType register)
	{
		// TODO: handle values below AX (low / high)
		int index = (int)register - (int)RegisterType.AX;
		index = Math.Clamp(index, 0, registerCount);

		return index;
	}

	ushort GetRegisterValue(RegisterType register)
	{
		return registers[GetIndex(register)];
	}

	void SetRegisterValue(RegisterType register, ushort newValue)
	{
		RecordUpdate(register, newValue);
		registers[GetIndex(register)] = newValue;
	}

	void RecordUpdate(RegisterType register, ushort newValue)
	{
		lastUpdate.register = register;
		lastUpdate.previousValue = GetRegisterValue(register);
		lastUpdate.newValue = newValue;
	}

	void SetFlags(ushort newValue)
	{
		FlagSet newFlags = new FlagSet();
		newFlags.bZeroFlag = (newValue == 0);

		const int signBitMask = 0x8000;
		newFlags.bSignFlag = (newValue & signBitMask) != 0;

		RecordFlagsUpdate(newFlags);
		flags = newFlags;
	}

	void RecordFlagsUpdate(FlagSet newFlags)
	{
		lastFlagsUpdate.previousValue = flags;
		lastFlagsUpdate.newValue = newFlags;
	}

	ushort GetOperandValue(InstructionOperand operand)
	{
		ushort operandValue = 0;

		if (operand.Type == OperandType.Register)
		{
			if (operand.Register.Index != RegisterType.None)
			{
				operandValue = GetRegisterValue(operand.Register.Index);
			}
		}
		else if (operand.Type == OperandType.Immediate)
		{
			operandValue = (ushort)operand.Immediate.Value;
		}
		// TODO: handle OperandType.Memory

		return operandValue;
	}

	ushort PerformArithmeticInstruction(OperationType operation,
									   ushort sourceOperand, ushort destinationOperand,
									   out bool bShouldStore)
	{
		ushort result = 0;
		bShouldStore = true;

		switch(operation)
		{
			case OperationType.AddRegMemWithRegToEither:
			case OperationType.AddImmediateToRegMem:
			case OperationType.AddImmediateToAccumulator:
				result = (ushort)(sourceOperand + destinationOperand);
				break;
			case OperationType.SubRegMemAndRegToEither:
			case OperationType.SubImmediateFromRegMem:
			case OperationType.SubImmediateFromAccumulator:
			case OperationType.CmpRegMemAndReg:
			case OperationType.CmpImmediateWithRegMem:
			case OperationType.CmpImmediateWithAccumulator:
				result = (ushort)(destinationOperand - sourceOperand);
				break;
		}

		if (operation == OperationType.CmpRegMemAndReg ||
			operation == OperationType.CmpImmediateWithRegMem ||
			operation == OperationType.CmpImmediateWithAccumulator)
		{
			bShouldStore = false;
		}

		return result;
	}

	void PerformInstruction(Instruction instruction)
	{
		ushort sourceValue = 0;

		if (instruction.type == OperationType.MovImmediateToReg)
		{
			sourceValue = GetOperandValue(instruction.operandTwo);
			SetRegisterValue(instruction.operandOne.Register.Index, sourceValue);
		}
		else if (instruction.type == OperationType.MovRegMemToFromRegMask)
		{
			// note: only reg to reg moves are handled right now
			if (instruction.modeType == ModeType.Register)
			{
				sourceValue = GetOperandValue(instruction.operandTwo);
				SetRegisterValue(instruction.operandOne.Register.Index, sourceValue);
			}
		}
		else if (instruction.IsArithmeticInstruction())
		{
			ushort destinationValue = GetOperandValue(instruction.operandOne);
			sourceValue = GetOperandValue(instruction.operandTwo);
			bool bShouldStoreResult = true;
			ushort result = PerformArithmeticInstruction(instruction.type,
														sourceValue, destinationValue,
														out bShouldStoreResult);
			SetFlags(result);

			// TODO: handle memory as a destination
			if (bShouldStoreResult && instruction.operandOne.Type == OperandType.Register)
			{
				SetRegisterValue(instruction.operandOne.Register.Index, result);
			}
			else
			{
				lastUpdate.Initialize();
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
		lastUpdate.Initialize();
		flags.Initialize();
		Console.WriteLine($"--- {program.Filename} execution ---");

		List<Instruction> instructions = program.Instructions;

		foreach (Instruction instruction in instructions)
		{
			PerformInstruction(instruction);
			string instructionString =
				InstructionFormatter.ConvertInstructionToString(instruction);
			bool bShouldPrintFlags = instruction.IsArithmeticInstruction();
			Console.WriteLine(instructionString + " ; " + GetLastUpdateString(bShouldPrintFlags));
		}
	}

	public void PrintState()
	{
		Console.WriteLine("");
		Console.WriteLine("Final registers:");
		string registerName = "";
		int index = 0;
		ushort registerValue = 0;

		for (int i = 0; i < registerCount; i++)
		{
			index = (int)RegisterType.AX + i;
			registerValue = registers[i];

			if (registerValue == 0)
			{
				continue;
			}

			registerName = InstructionFormatter.ConvertRegisterToString((RegisterType)index);
			string formatString = $"      {registerName}: 0x{registerValue:x4} ({registerValue})";
			Console.WriteLine(formatString);
		}

		if (flags.IsAnyFlagSet())
		{
			Console.WriteLine($"   flags: {flags.ToString()}");
		}
	}

	string GetLastUpdateString(bool bShouldPrintFlags)
	{
		string destination = InstructionFormatter.ConvertRegisterToString(lastUpdate.register);
		string output = "";

		if (lastUpdate.DidChange())
		{
			output = $"{destination}:0x{lastUpdate.previousValue:x}->0x{lastUpdate.newValue:x}";
		}

		if (bShouldPrintFlags && lastFlagsUpdate.DidChange())
		{
			if (output.Length > 0)
			{
				output += " ";
			}

			output += $"flags:{lastFlagsUpdate.previousValue.ToString()}->{lastFlagsUpdate.newValue.ToString()}";
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
