using System;
using System.CommandLine;

namespace HaversineRelease
{
	class Program
	{
		static int Main(string[] args)
		{
			Argument<string> inputFile = new("inputFile")
			{
				Description = "Input file (JSON format)"
			};

			Argument<string> answerFile = new("answerFile")
			{
				Description = "Answer file (binary format)",
				DefaultValueFactory = _ => ""
			};

			RootCommand rootCommand = new();
			rootCommand.Arguments.Add(inputFile);
			rootCommand.Arguments.Add(answerFile);

			rootCommand.SetAction(parseResult =>
			{
				return 0;
			});

			ParseResult parseResult = rootCommand.Parse(args);

			return parseResult.Invoke();
		}
	}
}
