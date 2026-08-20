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
							   out OperationMode operationMode,
							   out bool shouldPrintIP)
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
		shouldPrintIP = false;

		const string executeModeString = "-exec";
		const string IPOptionString = "-ip";
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
				shouldPrintIP = true;
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
		bool shouldPrintIP = false;
		CheckArguments(args, out inputFilename, out operationMode,
					   out shouldPrintIP);

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
			SimulatorOptions options = new SimulatorOptions();
			options.shouldPrintIP = shouldPrintIP;

			Simulator simulator = new Simulator();
			simulator.Execute(program, options);
			simulator.PrintState();
		}
    }
}
