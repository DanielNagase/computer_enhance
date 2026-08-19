using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

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
