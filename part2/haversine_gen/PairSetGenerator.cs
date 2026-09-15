using System;
using System.Collections.Generic;
using System.IO;

using f64 = double;

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
		Method method = Method.Clustered;
		List<Pair> pairs = new List<Pair>();

		public PairSetGenerator(Int32 Seed)
		{
			random = new Random(Seed);
		}

		public void Generate(Method inMethod, int numPairs)
		{
			if (numPairs <= 0)
			{
				throw new Exception("The number of pairs must be greater than zero!");
			}

			method = inMethod;
			pairs.EnsureCapacity(numPairs);

			if (method == Method.Clustered)
			{
				GenerateClustered(numPairs);
			}
			else if (method == Method.Uniform)
			{
				GenerateUniform(numPairs);
			}
		}

		public void WriteJSONFile(string outputFilename)
		{
			using (StreamWriter writer = new StreamWriter(outputFilename, false))
			{
				writer.WriteLine("""{"pairs":[""");
				writer.WriteLine();
				writer.WriteLine("]}");
			}
		}

		void GenerateClustered(int numPairs)
		{
		}

		void GenerateUniform(int numPairs)
		{
			Pair pair;

			for (int i = 0; i < numPairs; i++)
			{
				pair = pairs[i];
				SetToUniformRandomPoint(pair.a);
				SetToUniformRandomPoint(pair.b);
			}
		}

		void SetToUniformRandomPoint(Point point)
		{
			point.x = random.NextDouble() * (180 + 180) - 180;
			point.y = random.NextDouble() * (90 + 90) - 90;
		}
	}
}
