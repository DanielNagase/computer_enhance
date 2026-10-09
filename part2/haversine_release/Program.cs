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
				string inputFilePath = "";

				if (parseResult.GetValue(inputFile) is string parsedInputFile)
				{
					inputFilePath = parsedInputFile;
				}

				string answerFilePath = "";

				if (parseResult.GetValue(answerFile) is string parsedAnswerFile)
				{
					answerFilePath = parsedAnswerFile;
				}

				ReadInput(inputFilePath, answerFilePath);

				return 0;
			});

			ParseResult parseResult = rootCommand.Parse(args);

			return parseResult.Invoke();
		}

		static void ReadInput(string inputFilePath, string answerFilePath)
		{
			Lexer lexer = new Lexer(inputFilePath);
			Parser parser = new Parser(lexer);
			Object data = parser.Parse();
		}
	}
}
