using System;

namespace HaversineRelease
{
	class JSON
	{
		public static void PrettyPrint(Object obj, int indentLevel = 0)
		{
			string indent = new string(' ', indentLevel);

			if (obj is List<Object> list)
			{
				Console.WriteLine($"{indent}[");

				foreach(var item in list)
				{
					PrettyPrint(item, 2 + indentLevel);
				}

				Console.WriteLine($"{indent}]");
			}
			else if (obj is JSONObject jsonObject)
			{
				Console.WriteLine($"{indent}{{");
				int jsonObjectIndent = 2 + indentLevel;
				string jsonIndent = new string(' ', jsonObjectIndent);

				foreach(var pair in jsonObject)
				{
					Console.Write($"{jsonIndent}\"{pair.Key}\" : ");
					PrettyPrint(pair.Value, jsonObjectIndent);
				}

				Console.WriteLine($"{indent}}}");
			}
			else if (obj is string stringObject)
			{
				Console.WriteLine($"{indent}\"{stringObject}\"");
			}
			else
			{
				Console.WriteLine($"{obj}");
			}
		}
	}
}
