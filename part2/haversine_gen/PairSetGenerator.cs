using System;

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

		public PairSetGenerator(Int32 Seed)
		{
			random = new Random(Seed);
		}

		public void Generate(Method inMethod, int numPairs)
		{
			method = inMethod;
		}
	}
}
