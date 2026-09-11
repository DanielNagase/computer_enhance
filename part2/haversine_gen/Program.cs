using System;
using System.CommandLine;

namespace HaversineGenerator
{
	class Program
	{
		static void Main(string[] args)
		{
			Argument<int> seedArgument = new("seed")
			{
				Description = "Seed for the random number generator",
			};

			Argument<int> numPairsArgument = new("pairs")
			{
				Description = "Number of pairs to generate",
			};

			RootCommand rootCommand = new();
			rootCommand.Arguments.Add(seedArgument);
			rootCommand.Arguments.Add(numPairsArgument);
			rootCommand.Parse(args).Invoke();
		}
	}
}
