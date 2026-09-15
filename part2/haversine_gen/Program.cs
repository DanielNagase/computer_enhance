using System;
using System.CommandLine;

namespace HaversineGenerator
{
	class Program
	{
		static int Main(string[] args)
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

			rootCommand.SetAction(parseResult =>
			{
				Method method = parseResult.GetValue(methodOption);
				int seed = parseResult.GetValue(seedArgument);
				int numPairs = parseResult.GetValue(numPairsArgument);
				Generate(method, seed, numPairs);

				return 0;
			});

			ParseResult parseResult = rootCommand.Parse(args);

			return parseResult.Invoke();
		}

		static void Generate(Method method, int seed, int numPairs)
		{
			PairSetGenerator generator = new PairSetGenerator(seed);
			generator.Generate(method, numPairs);

			string outputFilename = $"data_{numPairs}.json";
			generator.WriteJSONFile(outputFilename);
		}
	}
}
