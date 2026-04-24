using System;
using System.IO;
using System.Text;

public enum OperationType
{
	Mov = 0,
	None
};

class Instruction
{
	public OperationType type = OperationType.None;

	// D field (1 = REG is destination, 0 = REG is source)
	public bool bUseRegFieldAsDestination = false;
	// W field (1 = word, 0 = byte)
	public bool bIsWordOperation = false;
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

		byte[] bytes = new byte[6];
		const byte movMask = 0b1000_1000;

		using (FileStream filestream = File.OpenRead(inputFilename))
		{
			int numBytesToRead = 1;
			int numBytesRead = 0;

			while (numBytesToRead > 0)
			{
				int numCurrentBytesRead = filestream.Read(bytes, numBytesRead, numBytesToRead);

				if (numCurrentBytesRead == 0)
				{
					break;
				}

				numBytesRead += numCurrentBytesRead;
				numBytesToRead -= numCurrentBytesRead;

				if ((bytes[0] & movMask) == movMask)
				{
					numBytesToRead += 1;
					int bytesRead = filestream.Read(bytes, numBytesRead, numBytesToRead);

					// test output
					Console.WriteLine("mov");
					Console.WriteLine(bytes[0].ToString("x"));
					Console.WriteLine(bytes[1].ToString("x"));
				}
			}
		}
    }
}
