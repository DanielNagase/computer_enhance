using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HaversineGenerator
{
	enum Method
	{
		Clustered,
		Uniform
	}

	struct Point
	{
		public f64 x = 0.0f;
		public f64 y = 0.0f;

		public Point()
		{
			x = 0.0f;
			y = 0.0f;
		}

		public Point(f64 inX, f64 inY)
		{
			x = inX;
			y = inY;
		}
	}

	class Pair
	{
		public Point a;
		public Point b;

		public Pair()
		{
			a = new Point();
			b = new Point();
		}

		public Pair(Point inA, Point inB)
		{
			a = inA;
			b = inB;
		}
	}

	class PairSetGenerator
	{
		Random random;
		Int32 seed = 0;

		Method method = Method.Clustered;
		Pair[] pairs = new Pair[10];

		f64[] distances = new f64[10];
		f64 sumOfDistances;

		static readonly CompositeFormat pairFormat =
					CompositeFormat.Parse("""{{"x0":{0:f16}, "y0":{1:f16}, "x1":{2:f16}, "y1":{3:f16}}}{4}""");

		public PairSetGenerator(Int32 Seed)
		{
			random = new Random(Seed);
			seed = Seed;
		}

		public void Generate(Method inMethod, int numPairs)
		{
			if (numPairs <= 0)
			{
				throw new Exception("The number of pairs must be greater than zero!");
			}

			method = inMethod;
			ResizePairsArray(numPairs);

			if (method == Method.Clustered)
			{
				GenerateClustered(numPairs);
			}
			else if (method == Method.Uniform)
			{
				GenerateUniform(numPairs);
			}

			CalculateDistances();
		}

		void ResizePairsArray(int numPairs)
		{
			Array.Resize(ref pairs, numPairs);

			for (int i = 0; i < pairs.Length; i++)
			{
				pairs[i] = new Pair();
			}

			distances = new f64[numPairs];
		}

		public void WriteJSONFile(string outputFilename)
		{
			using (StreamWriter writer = new StreamWriter(outputFilename, false))
			{
				writer.WriteLine("""{"pairs":[""");
				bool bIncludeComma = false;

				for (int i = 0; i < pairs.Length; i++)
				{
					bIncludeComma = i < (pairs.Length - 1);
					writer.WriteLine(FormatPair(pairs[i], bIncludeComma));
				}

				writer.WriteLine("]}");
			}
		}

		public void WriteAnswerFile(string outputFilename)
		{
			using (var stream = File.Open(outputFilename, FileMode.Create))
			{
				using (var writer = new BinaryWriter(stream, Encoding.UTF8, false))
				{
					foreach (f64 distance in distances)
					{
						writer.Write(distance);
					}

					writer.Write(sumOfDistances);
				}
			}
		}

		public void PrintSummary()
		{
			Console.WriteLine($"Method: {method}");
			Console.WriteLine($"Random seed: {seed}");
			Console.WriteLine($"Pair count: {pairs.Length}");
			Console.WriteLine($"Expected sum: {sumOfDistances}");
		}

		string FormatPair(Pair pair, bool bIncludeComma)
		{
			return String.Format(null, pairFormat, pair.a.x, pair.a.y,
								 pair.b.x, pair.b.y, bIncludeComma ? "," : "");
		}

		void GenerateClustered(int numPairs)
		{
			if (pairs.Length != numPairs)
			{
				throw new Exception($"The pairs array size ({pairs.Length}) is not equal to the number of pairs ({numPairs})!");
			}

			const int numClusters = 64;
			Point[] clusterCenters = new Point[numClusters];

			for (int i = 0; i < clusterCenters.Length; i++)
			{
				clusterCenters[i] = new Point();
				SetToUniformRandomPoint(ref clusterCenters[i]);
			}

			int clusterIndex = 0;
			const f64 maxDistance = 20.0f;
			Pair pair;

			for (int i = 0; i < pairs.Length; i++)
			{
				clusterIndex = i % numClusters;
				pair = pairs[i];
				SetToRandomPointInCluster(ref pair.a, clusterCenters[clusterIndex], maxDistance);
				SetToRandomPointInCluster(ref pair.b, clusterCenters[clusterIndex], maxDistance);
			}
		}

		void SetToRandomPointInCluster(ref Point point, Point clusterCenter, f64 maximumDistance)
		{
			f64 XOffset = random.NextDouble() * (2.0f * maximumDistance) - maximumDistance;
			f64 YOffset = random.NextDouble() * (2.0f * maximumDistance) - maximumDistance;
			point.x = clusterCenter.x + XOffset;
			point.y = clusterCenter.y + YOffset;
		}

		void GenerateUniform(int numPairs)
		{
			if (pairs.Length != numPairs)
			{
				throw new Exception($"The pairs array size ({pairs.Length}) is not equal to the number of pairs ({numPairs})!");
			}

			Pair pair;

			for (int i = 0; i < numPairs; i++)
			{
				pair = pairs[i];
				SetToUniformRandomPoint(ref pair.a);
				SetToUniformRandomPoint(ref pair.b);
			}
		}

		void SetToUniformRandomPoint(ref Point point)
		{
			point.x = random.NextDouble() * (180.0f + 180.0f) - 180.0f;
			point.y = random.NextDouble() * (90.0f + 90.0f) - 90.0f;
		}

		void CalculateDistances()
		{
			if (distances.Length != pairs.Length)
			{
				throw new Exception($"The pairs array and distances array must have the same size!");
			}

			Pair pair;
			sumOfDistances = 0.0f;
			const f64 EarthRadius = 6372.8f;

			for (int i = 0; i < distances.Length; i++)
			{
				pair = pairs[i];
				distances[i] =
					HaversineFormula.ReferenceHaversine(pair.a.x, pair.a.y,
														pair.b.x, pair.b.y, EarthRadius);
				sumOfDistances += distances[i];
			}
		}
	}
}
