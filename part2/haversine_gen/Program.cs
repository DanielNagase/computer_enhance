using System;
using System.CommandLine;

enum Method
{
	Clustered,
	Uniform
}

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

			Option<Method> methodOption = new ("--method")
			{
				Description = "Method for generation",
				DefaultValueFactory = parseResult => Method.Clustered
			};

			RootCommand rootCommand = new();
			rootCommand.Arguments.Add(seedArgument);
			rootCommand.Arguments.Add(numPairsArgument);
			rootCommand.Options.Add(methodOption);
			rootCommand.Parse(args).Invoke();
		}
	}
}
