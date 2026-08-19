using System;
using System.Collections.Generic;

class Simulator
{
	const int registerCount = 8;
	ushort[] registers = new ushort[registerCount];

	ushort instructionPointer = 0;

	ushort instructionPointerLimit = 0;

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

	void InitializeInstructionPointer(ushort limit)
	{
		instructionPointer = 0;
		instructionPointerLimit = limit;
	}

	void IncrementInstructionPointer(ushort instructionSize)
	{
		instructionPointer += instructionSize;
	}

	bool CanTerminateExecution()
	{
		return instructionPointer >= instructionPointerLimit;
	}

	public void Execute(Program program)
	{
		InitializeRegisters();
		InitializeInstructionPointer(program.Size);
		lastUpdate.Initialize();
		flags.Initialize();
		Console.WriteLine($"--- {program.Filename} execution ---");

		List<Instruction> instructions = program.Instructions;

		foreach (Instruction instruction in instructions)
		{
			IncrementInstructionPointer(instruction.size);
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
