using System;

namespace HaversineRelease
{
	class JSON
	{
		public static void PrettyPrint(Object obj, int indentLevel = 0,
									   bool bPrintComma = false)
		{
			string indent = new string(' ', indentLevel);
			string comma = bPrintComma ? "," : "";

			if (obj is List<Object> list)
			{
				Console.WriteLine($"{indent}[");

				for (int i = 0; i < list.Count; i++)
				{
					bool bAddComma = i < (list.Count - 1);
					PrettyPrint(list[i], 2 + indentLevel, bAddComma);
				}

				Console.WriteLine($"{indent}]{comma}");
			}
			else if (obj is JSONObject jsonObject)
			{
				Console.WriteLine("{");
				int jsonObjectIndent = 2 + indentLevel;
				string jsonIndent = new string(' ', jsonObjectIndent);
				int i = 0;

				foreach (var pair in jsonObject)
				{
					bool bAddComma = i < (jsonObject.Count - 1);
					Console.Write($"{jsonIndent}\"{pair.Key}\" : ");
					PrettyPrint(pair.Value, jsonObjectIndent, bAddComma);
					i++;
				}

				Console.WriteLine($"{indent}}}{comma}");
			}
			else if (obj is string stringObject)
			{
				Console.WriteLine($"\"{stringObject}\"{comma}");
			}
			else
			{
				Console.WriteLine($"{obj}{comma}");
			}
		}
	}
}
