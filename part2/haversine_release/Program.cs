using System;
using System.CommandLine;
using System.IO;
using System.Text;

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

			CalculateDistances(ref data, answerFilePath,
							   out f64 distance, out int pairCount);
		}

		static void CalculateDistances(ref Object data, string answerFilePath,
									   out f64 averageDistance, out int pairCount)
		{
			JSONObject jsonObject = (JSONObject)data;
			List<Object> pairs = (List<Object>)jsonObject["pairs"];
			f64[] distances = new f64[pairs.Count];

			f64[] answerDistances = new f64[pairs.Count];
			f64 answerAverageDistance = 0.0f;
			bool bHasAnswer = false;

			if (!String.IsNullOrEmpty(answerFilePath))
			{
				bHasAnswer = ReadAnswerFile(answerFilePath, ref answerDistances,
											ref answerAverageDistance);
			}

			f64 sumOfDistances = 0.0f;
			const f64 EarthRadius = 6372.8f;

			f64 x0, y0, x1, y1;

			for (int i = 0; i < pairs.Count; i++)
			{
				if (pairs[i] is JSONObject pair)
				{
					x0 = (f64)pair["x0"];
					y0 = (f64)pair["y0"];
					x1 = (f64)pair["x1"];
					y1 = (f64)pair["y1"];

					distances[i] =
						HaversineFormula.ReferenceHaversine(x0, y0,
															x1, y1, EarthRadius);
					sumOfDistances += distances[i];
				}
			}

			pairCount = pairs.Count;
			averageDistance = sumOfDistances / pairCount;
			Console.WriteLine($"Pair count: {pairs.Count}");
			Console.WriteLine($"Haversine sum: {averageDistance}");

			if (bHasAnswer)
			{
				Console.WriteLine("Validation");
				Console.WriteLine($"Reference sum: {answerAverageDistance}");

				f64 difference = averageDistance - answerAverageDistance;
				Console.WriteLine($"Difference: {difference}");
			}
		}

		static bool ReadAnswerFile(string answerFilePath, ref f64[] answerDistances,
								   ref f64 answerAverageDistance)
		{
			bool bDidRead = false;

			using (var stream = File.Open(answerFilePath, FileMode.Open))
			{
				using (var reader = new BinaryReader(stream, Encoding.UTF8))
				{
					for (int i = 0; i < answerDistances.Length; i++)
					{
						answerDistances[i] = reader.ReadDouble();
					}

					answerAverageDistance = reader.ReadDouble();
					bDidRead = true;
				}
			}

			return bDidRead;
		}
	}
}
