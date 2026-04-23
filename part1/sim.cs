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

		byte[] bytes = new byte[1024];

		using (FileStream filestream = File.OpenRead(inputFilename))
		{
			int numBytesToRead = (int)filestream.Length;
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
			}

			Console.WriteLine(numBytesRead);
		}
    }
}
