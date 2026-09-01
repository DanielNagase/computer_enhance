using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

class Decoder
{
	InstructionFormatterOptions formatterOptions = new InstructionFormatterOptions();

	public void Print(Program program)
	{
		formatterOptions.shouldPrintJumpIncrementAsComment = true;
		formatterOptions.useNasmJumpOutputFormat = true;
		formatterOptions.useSpaceInEffectiveAddresses = true;

		Console.WriteLine($"; {program.Filename} disassembly:");
		Console.WriteLine("bits 16");
		List<Instruction> instructions = program.Instructions;

		foreach (Instruction instruction in instructions)
		{
			Console.WriteLine(InstructionFormatter.ConvertInstructionToString(instruction, formatterOptions));
		}
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
							   out OperationMode operationMode,
							   ref SimulatorOptions options)
	{
		const int argumentsErrorExitCode = 1;

		if (args.Length < 1)
		{
			ExitProgramWithError(argumentsErrorExitCode,
								 "Missing filename argument!");
		}

		List<string> argList = new List<string>(args);

		inputFilename = "";
		operationMode = OperationMode.Decode;

		const string executeModeString = "-exec";
		const string IPOptionString = "-ip";
		const string dumpOptionString = "-dump";
		string currentArg = "";

		while (argList.Count > 0)
		{
			currentArg = argList[0];
			argList.RemoveAt(0);

			if (currentArg == executeModeString)
			{
				operationMode = OperationMode.Execute;
			}
			else if (currentArg == IPOptionString)
			{
				options.bShouldPrintIP = true;
			}
			else if (currentArg == dumpOptionString)
			{
				options.bShouldDumpMemoryToFile = true;
			}
			else
			{
				inputFilename = currentArg;
			}
		}

		if (String.IsNullOrEmpty(inputFilename))
		{
			ExitProgramWithError(argumentsErrorExitCode,
								 "Missing filename argument!");
		}
		else if (!File.Exists(inputFilename))
		{
			ExitProgramWithError(argumentsErrorExitCode,
								 $"Filename '{inputFilename}' does not exist!");
		}
	}

	static void Main(string[] args)
    {
		string inputFilename;
		OperationMode operationMode = OperationMode.Decode;
		SimulatorOptions options = new SimulatorOptions();
		options.Initialize();
		CheckArguments(args, out inputFilename, out operationMode,
					   ref options);

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
			simulator.Execute(program, options);
			simulator.PrintState();

			if (options.bShouldDumpMemoryToFile)
			{
				simulator.DumpMemory("sim_memory_0.data");
			}
		}
    }
}
