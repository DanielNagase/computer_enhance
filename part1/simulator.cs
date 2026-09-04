using System;
using System.Collections.Generic;
using System.IO;

public enum ClocksStatisticsType
{
	None = 0,
	Basic,
	Detailed
}

struct SimulatorOptions
{
	public bool bShouldPrintIP;
	public bool bShouldDumpMemoryToFile;
	public ClocksStatisticsType clocksStatistics;

	public void Initialize()
	{
		bShouldPrintIP = false;
		bShouldDumpMemoryToFile = false;
		clocksStatistics = ClocksStatisticsType.None;
	}
}

class Memory
{
	uint Size = 0;

	byte[] Bytes;

	bool bIsInitialized = false;

	public byte[] GetBytes { get => Bytes; }

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
		ushort highByteAddress = (ushort)(address + 1);
		ValidateAddress(address);
		ValidateAddress(highByteAddress);

		// The 8086 is little endian, so the first byte goes in the
		// lowest-order byte of the destination, and the second goes
		// in the highest-order byte of the destination.
		ushort highByte = (ushort)(Bytes[highByteAddress] << 8);
		ushort lowByte = (ushort)(Bytes[address]);

		return (ushort)(highByte | lowByte);
	}

	public void StoreWord(ushort address, ushort value)
	{
		ushort highByteAddress = (ushort)(address + 1);
		ValidateAddress(address);
		ValidateAddress(highByteAddress);

		// The 8086 is little endian, so the lowest-order byte of the
		// value goes in the first address and the highest-order byte
		// goes in the second address.
		Bytes[address] = (byte)(0xff & value);
		Bytes[highByteAddress] = (byte)(value >> 8);
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

class MemoryWriter
{
	public void WriteToFile(string outputFilename, byte[] memory)
	{
		using (FileStream filestream = new FileStream(outputFilename, FileMode.Create,
													  FileAccess.Write, FileShare.None))
		{
			filestream.Write(memory, 0, memory.Length);
		}
	}
}

struct ClocksEstimate
{
	public uint TotalClocks;
	public uint BaseClocks;
	public uint EAClocks;

	public void Initialize()
	{
		TotalClocks = 0;
		BaseClocks = 0;
		EAClocks = 0;
	}
}

class ClocksLookupTable
{
	public void Lookup(Instruction instruction, ref ClocksEstimate estimate)
	{
		estimate.Initialize();

		switch(instruction.type)
		{
			case OperationType.AddRegMemWithRegToEither:
			case OperationType.AddImmediateToRegMem:
			case OperationType.AddImmediateToAccumulator:
				ProcessAdd(instruction, ref estimate);
				break;
			default:
				break;
		}
	}

	bool IsRegisterRegister(Instruction instruction)
	{
		return instruction.operandOne.Type == OperandType.Register &&
			instruction.operandTwo.Type == OperandType.Register;
	}

	bool IsRegisterMemory(Instruction instruction)
	{
		return instruction.operandOne.Type == OperandType.Register &&
			instruction.operandTwo.Type == OperandType.Memory;
	}

	bool IsMemoryRegister(Instruction instruction)
	{
		return instruction.operandOne.Type == OperandType.Memory &&
			instruction.operandTwo.Type == OperandType.Register;
	}

	bool IsRegisterImmediate(Instruction instruction)
	{
		return instruction.operandOne.Type == OperandType.Register &&
			instruction.operandTwo.Type == OperandType.Immediate;
	}

	bool IsMemoryImmediate(Instruction instruction)
	{
		return instruction.operandOne.Type == OperandType.Memory &&
			instruction.operandTwo.Type == OperandType.Immediate;
	}

	bool IsAccumulatorImmediate(Instruction instruction)
	{
		bool bIsAccumulator =
			instruction.operandOne.Register.Index == RegisterType.AL ||
			instruction.operandOne.Register.Index == RegisterType.AX;
		return IsRegisterImmediate(instruction) && bIsAccumulator;
	}

	void ProcessAdd(Instruction instruction, ref ClocksEstimate estimate)
	{
		bool bHasEffectiveAddress = false;

		if (IsRegisterRegister(instruction))
		{
			estimate.TotalClocks = 3;
		}
		if (IsRegisterMemory(instruction))
		{
			estimate.BaseClocks = 9;
			bHasEffectiveAddress = true;
		}
		if (IsMemoryRegister(instruction))
		{
			estimate.BaseClocks = 16;
			bHasEffectiveAddress = true;
		}
		// IsAccumulatorImmediate includes IsRegisterImmediate, but is more
		// specific, so must be tested first
		else if (IsAccumulatorImmediate(instruction) || IsRegisterImmediate(instruction))
		{
			estimate.TotalClocks = 4;
		}
		if (IsMemoryImmediate(instruction))
		{
			estimate.BaseClocks = 17;
			bHasEffectiveAddress = true;
		}

		if (bHasEffectiveAddress)
		{
			GetEffectiveAddressAndSumClocks(instruction, ref estimate);
		}
	}

	void GetEffectiveAddressAndSumClocks(Instruction instruction, ref ClocksEstimate estimate)
	{
		EffectiveAddressExpression address;

		if (instruction.operandOne.Type == OperandType.Memory)
		{
			address = instruction.operandOne.Address;
		}
		else if (instruction.operandTwo.Type == OperandType.Memory)
		{
			address = instruction.operandTwo.Address;
		}
		else
		{
			return;
		}

		ProcessEffectiveAddress(address, ref estimate);
		estimate.TotalClocks = estimate.BaseClocks + estimate.EAClocks;
	}

	bool IsBase(EffectiveAddressTerm term)
	{
		return term.Register.Index == RegisterType.BX ||
			term.Register.Index == RegisterType.BP;
	}

	bool IsIndex(EffectiveAddressTerm term)
	{
		return term.Register.Index == RegisterType.SI ||
			term.Register.Index == RegisterType.DI;
	}

	bool IsBasePlusIndexVariantOne(EffectiveAddressExpression address)
	{
		return (address.TermOne.Register.Index == RegisterType.BP &&
				address.TermTwo.Register.Index == RegisterType.DI) ||
			(address.TermOne.Register.Index == RegisterType.BX &&
			 address.TermTwo.Register.Index == RegisterType.SI);
	}

	bool IsBasePlusIndexVariantTwo(EffectiveAddressExpression address)
	{
		return (address.TermOne.Register.Index == RegisterType.BP &&
				address.TermTwo.Register.Index == RegisterType.SI) ||
			(address.TermOne.Register.Index == RegisterType.BX &&
			 address.TermTwo.Register.Index == RegisterType.DI);
	}

	void ProcessEffectiveAddress(EffectiveAddressExpression address, ref ClocksEstimate estimate)
	{
		bool bHasDisplacement = address.Displacement > 0;
		bool bIsTermOneBase = IsBase(address.TermOne);
		bool bIsTermOneIndex = IsIndex(address.TermOne);
		bool bIsTermTwoBase = IsBase(address.TermTwo);
		bool bIsTermTwoIndex = IsIndex(address.TermTwo);

		bool bIsTermOneBaseOrIndex = bIsTermOneBase || bIsTermOneIndex;
		bool bIsTermTwoBaseOrIndex = bIsTermTwoBase || bIsTermTwoIndex;

		bool bIsTermOneNone = address.TermOne.Register.Index == RegisterType.None;
		bool bIsTermTwoNone = address.TermTwo.Register.Index == RegisterType.None;

		bool bIsDisplacementOnly =
			bHasDisplacement && bIsTermOneNone && bIsTermTwoNone;
		bool bIsBaseOrIndexOnly = !bHasDisplacement &&
			((bIsTermOneBaseOrIndex && bIsTermTwoNone) ||
			 (bIsTermTwoBaseOrIndex && bIsTermOneNone));
		bool bIsDisplacementPlusBaseOrIndex = bHasDisplacement &&
			((bIsTermOneBaseOrIndex && bIsTermTwoNone) ||
			 (bIsTermTwoBaseOrIndex && bIsTermOneNone));

		bool bIsBasePlusIndexVariantOne = IsBasePlusIndexVariantOne(address);
		bool bIsBasePlusIndexVariantTwo = IsBasePlusIndexVariantTwo(address);

		if (bIsDisplacementOnly)
		{
			estimate.EAClocks = 6;
		}
		else if (bIsBaseOrIndexOnly)
		{
			estimate.EAClocks = 5;
		}
		else if (bIsDisplacementPlusBaseOrIndex)
		{
			estimate.EAClocks = 9;
		}
		else if (!bHasDisplacement && bIsBasePlusIndexVariantOne)
		{
			estimate.EAClocks = 7;
		}
		else if (!bHasDisplacement && bIsBasePlusIndexVariantTwo)
		{
			estimate.EAClocks = 8;
		}
		else if (bHasDisplacement && bIsBasePlusIndexVariantOne)
		{
			estimate.EAClocks = 11;
		}
		else if (bHasDisplacement && bIsBasePlusIndexVariantTwo)
		{
			estimate.EAClocks = 12;
		}
	}
}

class ClocksStatisticsTabulator
{
	uint totalClocks = 0;
	ClocksStatisticsType statistics = ClocksStatisticsType.Basic;
	ClocksLookupTable lookupTable = new ClocksLookupTable();
	ClocksEstimate estimate = new ClocksEstimate();

	public ClocksStatisticsTabulator(ClocksStatisticsType inStatistics)
	{
		totalClocks = 0;
		statistics = inStatistics;
	}

	public string AddClocks(Instruction instruction)
	{
		lookupTable.Lookup(instruction, ref estimate);
		totalClocks += estimate.TotalClocks;
		string output = $"Clocks: +{estimate.TotalClocks} = {totalClocks}";

		if (statistics == ClocksStatisticsType.Detailed)
		{
			output += $" ({estimate.BaseClocks} + {estimate.EAClocks}ea)";
		}

		return output;
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

	ClocksStatisticsTabulator tabulator = null;

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
		else if (CanJump(instruction.type))
		{
			sbyte increment = Instruction.GetRelativeJumpDisplacement(instruction.operandOne);
			SetInstructionPointer((ushort)(instructionPointer + increment));
		}
	}

	string GetClocksStatistics(Instruction instruction)
	{
		if (tabulator == null)
		{
			return "";
		}

		return tabulator.AddClocks(instruction);
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

	// This is for when we need to set the instruction pointer
	// directly for a jump. Unlike IncrementInstructionPointer, we
	// update the new value *without* recording the previous value.
	void SetInstructionPointer(ushort newInstructionPointer)
	{
		instructionPointer = newInstructionPointer;
		lastIPUpdate.newValue = newInstructionPointer;
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

	public void Execute(Program program, SimulatorOptions inOptions)
	{
		options = inOptions;

		InitializeMainMemory();
		InitializeRegisters();
		InitializeInstructionPointer(program.Size);
		lastUpdate.Initialize();
		flags.Initialize();

		if (options.clocksStatistics != ClocksStatisticsType.None)
		{
			tabulator = new ClocksStatisticsTabulator(inOptions.clocksStatistics);
		}

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
			OutputUpdateString(instruction);
		}
	}

	void OutputUpdateString(Instruction instruction)
	{
		string outputString =
			InstructionFormatter.ConvertInstructionToString(instruction, formatterOptions) + " ; ";

		if (options.clocksStatistics != ClocksStatisticsType.None)
		{
			outputString += (GetClocksStatistics(instruction) + " | ");
		}

		bool bShouldPrintFlags = instruction.IsArithmeticInstruction();
		outputString += GetUpdateStringAndFlushUpdates(bShouldPrintFlags);
		Console.WriteLine(outputString);
	}

	public void DumpMemory(string outputFilename)
	{
		MemoryWriter writer = new MemoryWriter();
		writer.WriteToFile(outputFilename, mainMemory.GetBytes);
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

		if (options.bShouldPrintIP)
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

		if (options.bShouldPrintIP && lastIPUpdate.DidChange())
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
