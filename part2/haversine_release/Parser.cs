using System;
using System.Collections.Generic;

using JSONObject = System.Collections.Generic.Dictionary<string, System.Object>;

namespace HaversineRelease
{
	class Parser
	{
		Lexer lexer;
		Token currentToken;

		public Parser(Lexer Lexer)
		{
			lexer = Lexer;
			currentToken = lexer.GetNextToken();
		}

		void DisplayError(string errorMessage)
		{
			throw new Exception(errorMessage);
		}

		Symbol Peek()
		{
			return currentToken.Type;
		}

		Token Consume()
		{
			Token token = currentToken;
			currentToken = lexer.GetNextToken();

			return token;
		}

		bool Accept(Symbol symbol)
		{
			if (Peek() == symbol)
			{
				Consume();
				
				return true;
			}
			
			return false;
		}

		bool Expect(Symbol symbol)
		{
			if (Accept(symbol))
			{
				return true;
			}

			DisplayError("Expect: unexpected symbol: {currentToken}");

			return false;
		}

		Object Value()
		{
			Symbol type = currentToken.Type;

			if (type == Symbol.LeftBrace)
			{
				return Object();
			}

			if (type == Symbol.String || type == Symbol.Number ||
				type == Symbol.True || type == Symbol.False || type == Symbol.Null)
			{
				return Consume().Value;
			}

			throw new Exception($"unexpected {type} : {currentToken.ToString()}");
		}

		JSONObject Object()
		{
			Expect(Symbol.LeftBrace);

			if (Accept(Symbol.RightBrace))
			{
				return new JSONObject();
			}

			JSONObject members = Members();
			Expect(Symbol.RightBrace);

			return members;
		}

		JSONObject Members()
		{
			JSONObject member = Member();

			if (Accept(Symbol.Comma))
			{
				JSONObject members = Members();

				foreach (var pair in members)
				{
					member.Add(pair.Key, pair.Value);
				}
			}

			return member;
		}

		JSONObject Member()
		{
			string k = (string)currentToken.Value;
			Expect(Symbol.String);
			Expect(Symbol.Colon);
			JSONObject member = new JSONObject();
			member[k] = Value();

			return member;
		}

		public Object Parse()
		{
			Object o = Value();
			Expect(Symbol.EOF);

			return o;
		}
	}
}
