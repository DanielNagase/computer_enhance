using System;
using System.Collections.Generic;

struct SimulatorOptions
{
	public bool shouldPrintIP;
}

class Memory
{
	uint Size = 0;

	byte[] Bytes;

	bool bIsInitialized = false;

	public bool IsInitialized { get => bIsInitialized; }

	public bool Initialize(uint inSize)
	{
		if (inSize == 0)
		{
			return false;
		}

		Size = inSize;
		Bytes = new byte[inSize];
		bIsInitialized = true;

		return true;
	}

	public ushort LoadWord(ushort address)
	{
		ValidateAddress(address);
		ValidateAddress((ushort)(address + 1));

		// The 8086 is little endian, so the first byte goes in the
		// lowest-order byte of the destination, and the second goes
		// in the highest-order byte of the destination.
		ushort highByte = (ushort)(Bytes[address] >> 8);
		ushort lowByte = (ushort)(Bytes[address + 1]);

		return (ushort)(highByte & lowByte);
	}

	public void StoreWord(ushort address, ushort value)
	{
		ValidateAddress(address);
		ValidateAddress((ushort)(address + 1));

		// The 8086 is little endian, so the lowest-order byte of the
		// value goes in the first address and the highest-order byte
		// goes in the second address.
		byte highByte = (byte)(value >> 8);
		byte lowByte = (byte)(0xff & value);

		Bytes[address] = lowByte;
		Bytes[address + 1] = highByte;
	}

	public byte Load(ushort address)
	{
		ValidateAddress(address);

		return Bytes[address];
	}

	public void Store(ushort address, byte value)
	{
		ValidateAddress(address);
		Bytes[address] = value;
	}

	void ValidateAddress(ushort address)
	{
		if (!bIsInitialized)
		{
			throw new Exception($"Error: Uninitialized Memory object!");
		}
		
		if (address >= Size)
		{
			throw new Exception($"Address {address} is greater than or equal to the memory size of {Size}");
		}
	}
}

class Simulator
{
	const int registerCount = 8;
	ushort[] registers = new ushort[registerCount];

	ushort instructionPointer = 0;

	ushort instructionPointerLimit = 0;

	SimulatorOptions options = new SimulatorOptions();

	InstructionFormatterOptions formatterOptions = new InstructionFormatterOptions();

	Memory mainMemory = new Memory();

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

		public void Initialize()
		{
			previousValue.Initialize();
			newValue.Initialize();
		}

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
			return (previousValue != newValue);
		}
	}

	RegisterUpdate lastUpdate = new RegisterUpdate();

	RegisterUpdate lastIPUpdate = new RegisterUpdate();

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

	void SetRegisterValue(RegisterType register, ushort newValue, bool bUseWord)
	{
		ushort maskedValue = bUseWord ? newValue : (ushort)(newValue & 0xff);
		RecordUpdate(register, maskedValue);
		registers[GetIndex(register)] = maskedValue;
	}

	ushort GetValueFromRegisterAccess(RegisterAccess register)
	{
		ushort registerValue = 0;

		if (register.Index != RegisterType.None)
		{
			if (register.Count == 2)
			{
				registerValue = GetRegisterValue(register.Index);
			}
			else if (register.Count == 1)
			{
				// TODO: handle this
			}
		}

		return registerValue;
	}

	ushort ResolveEffectiveAddress(EffectiveAddressExpression expression)
	{
		ushort termOneValue = GetValueFromRegisterAccess(expression.TermOne.Register);
		ushort termTwoValue = GetValueFromRegisterAccess(expression.TermTwo.Register);
		ushort address = (ushort)(termOneValue + termTwoValue);
		// for now, we're dropping the sign of the displacement
		address += (ushort)expression.Displacement;

		return address;
	}

	ushort GetMemoryValue(EffectiveAddressExpression expression,
						  bool bUseWord)
	{
		ushort address = ResolveEffectiveAddress(expression);

		if (bUseWord)
		{
			return mainMemory.LoadWord(address);
		}
		else
		{
			return mainMemory.Load(address);
		}
	}

	void SetMemoryValue(EffectiveAddressExpression expression, ushort newValue,
						bool bUseWord)
	{
		ushort address = ResolveEffectiveAddress(expression);

		if (bUseWord)
		{
			mainMemory.StoreWord(address, newValue);
		}
		else
		{
			// TODO: revisit this as needed
			byte lowByte = (byte)(0xff & newValue);
			mainMemory.Store(address, lowByte);
		}
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

	ushort GetOperandValue(InstructionOperand operand, bool bUseWord)
	{
		ushort operandValue = 0;

		if (operand.Type == OperandType.Register)
		{
			if (operand.Register.Index != RegisterType.None)
			{
				// TODO: incorporate bUseWord
				operandValue = GetRegisterValue(operand.Register.Index);
			}
		}
		else if (operand.Type == OperandType.Immediate)
		{
			// TODO: incorporate bUseWord
			operandValue = (ushort)operand.Immediate.Value;
		}
		else if (operand.Type == OperandType.Memory)
		{
			operandValue = GetMemoryValue(operand.Address, bUseWord);
		}

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
		bool bUseWord = instruction.bIsWordOperation;

		bool bIsMovInstruction =
			instruction.type == OperationType.MovImmediateToReg ||
			instruction.type == OperationType.MovImmediateToRegMem ||
			instruction.type == OperationType.MovRegMemToFromReg;

		if (bIsMovInstruction)
		{
			sourceValue = GetOperandValue(instruction.operandTwo, bUseWord);

			if (instruction.operandOne.Type == OperandType.Register)
			{
				SetRegisterValue(instruction.operandOne.Register.Index, sourceValue, bUseWord);
			}
			else if (instruction.operandOne.Type == OperandType.Memory)
			{
				SetMemoryValue(instruction.operandOne.Address, sourceValue, bUseWord);
			}
		}
		else if (instruction.IsArithmeticInstruction())
		{
			ushort destinationValue = GetOperandValue(instruction.operandOne, bUseWord);
			sourceValue = GetOperandValue(instruction.operandTwo, bUseWord);
			bool bShouldStoreResult = true;
			ushort result = PerformArithmeticInstruction(instruction.type,
														sourceValue, destinationValue,
														out bShouldStoreResult);
			SetFlags(result);

			if (bShouldStoreResult && instruction.operandOne.Type == OperandType.Register)
			{
				SetRegisterValue(instruction.operandOne.Register.Index, result, bUseWord);
			}
			else if (bShouldStoreResult && instruction.operandOne.Type == OperandType.Memory)
			{
				SetMemoryValue(instruction.operandOne.Address, result, bUseWord);
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

	void InitializeMainMemory()
	{
		// one megabyte (1024 * 1024)
		bool bDidInitialize = mainMemory.Initialize(1048576);

		if (!bDidInitialize)
		{
			throw new Exception("Failed to initialize main memory!");
		}
	}

	void InitializeInstructionPointer(ushort limit)
	{
		instructionPointer = 0;
		instructionPointerLimit = limit;
	}

	void IncrementInstructionPointer(ushort instructionSize)
	{
		lastIPUpdate.previousValue = instructionPointer;

		instructionPointer += instructionSize;
		lastIPUpdate.newValue = instructionPointer;
		lastIPUpdate.register = RegisterType.None;
	}

	bool CanTerminateExecution()
	{
		return instructionPointer >= instructionPointerLimit;
	}

	bool CanJump(OperationType type)
	{
		bool bCanJump = false;

		switch(type)
		{
			case OperationType.JumpOnEqual_Zero:
				bCanJump = flags.bZeroFlag;
				break;
			case OperationType.JumpOnNotEqual_NotZero:
				bCanJump = !flags.bZeroFlag;
				break;
			case OperationType.JumpOnSign:
				bCanJump = flags.bSignFlag;
				break;
			case OperationType.JumpOnNotSign:
				bCanJump = !flags.bSignFlag;
				break;
				// TODO: implement the rest of the jumps needed
		}

		return bCanJump;
	}

	void CheckFlowControl(Instruction currentInstruction, Program program)
	{
		bool bShouldJump = CanJump(currentInstruction.type);

		if (bShouldJump)
		{
			sbyte increment = Instruction.GetRelativeJumpDisplacement(currentInstruction.operandOne);
			instructionPointer = (ushort)(instructionPointer + increment);
		}
	}

	public void Execute(Program program, SimulatorOptions inOptions)
	{
		options = inOptions;

		InitializeMainMemory();
		InitializeRegisters();
		InitializeInstructionPointer(program.Size);
		lastUpdate.Initialize();
		flags.Initialize();
		Console.WriteLine($"--- {program.Filename} execution ---");

		Instruction instruction = new Instruction();
		bool bDidFetchInstruction = false;

		while (!CanTerminateExecution())
		{
			bDidFetchInstruction =
				program.GetInstructionForIPValue(instructionPointer, ref instruction);

			if (!bDidFetchInstruction)
			{
				break;
			}

			IncrementInstructionPointer(instruction.size);
			PerformInstruction(instruction);
			CheckFlowControl(instruction, program);
			string instructionString =
				InstructionFormatter.ConvertInstructionToString(instruction, formatterOptions);
			bool bShouldPrintFlags = instruction.IsArithmeticInstruction();
			Console.WriteLine(instructionString + " ; " + GetUpdateStringAndFlushUpdates(bShouldPrintFlags));
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

		if (options.shouldPrintIP)
		{
			string formatString = $"      ip: 0x{instructionPointer:x4} ({instructionPointer})";
			Console.WriteLine(formatString);
		}

		if (flags.IsAnyFlagSet())
		{
			Console.WriteLine($"   flags: {flags.ToString()}");
		}
	}

	// For registers, the instruction pointer, and flags, check for
	// updates. If any update is found, convert it into a string and
	// reset the update struct. We must reset the update structs to
	// prevent printing updates multiple times.
	string GetUpdateStringAndFlushUpdates(bool bShouldPrintFlags)
	{
		string registerOutput = "";
		string flagsOutput = "";
		string IPOutput = "";
		List<string> parts = new List<string>(3);

		if (lastUpdate.DidChange())
		{
			string destination = InstructionFormatter.ConvertRegisterToString(lastUpdate.register);
			registerOutput = $"{destination}:0x{lastUpdate.previousValue:x}->0x{lastUpdate.newValue:x}";
			parts.Add(registerOutput);

			lastUpdate.Initialize();
		}

		if (options.shouldPrintIP && lastIPUpdate.DidChange())
		{
			IPOutput = GetLastInstructionPointerUpdateString();
			parts.Add(IPOutput);

			lastIPUpdate.Initialize();
		}

		if (bShouldPrintFlags && lastFlagsUpdate.DidChange())
		{
			flagsOutput = $"flags:{lastFlagsUpdate.previousValue.ToString()}->{lastFlagsUpdate.newValue.ToString()}";
			parts.Add(flagsOutput);

			lastFlagsUpdate.Initialize();
		}

		string output = String.Join(" ", parts);

		return output;
	}

	string GetLastInstructionPointerUpdateString()
	{
		if (!lastIPUpdate.DidChange())
		{
			return "";
		}

		return $"ip:0x{lastIPUpdate.previousValue:x}->0x{lastIPUpdate.newValue:x}";
	}
}
