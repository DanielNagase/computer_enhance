using System;
using System.IO;
using System.Text;

class Sim
{
	static void Main(string[] args)
    {
		if (args.Length != 1)
		{
			Console.WriteLine("Please provide exactly one argument!");

			return;
		}

		string inputFilename = args[0];

		if (!File.Exists(inputFilename))
		{
			Console.WriteLine($"Filename {inputFilename} does not exist!");

			return;
		}

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
