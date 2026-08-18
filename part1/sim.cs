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
