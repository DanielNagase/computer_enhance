using System;

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

			if (type == Symbol.String || type == Symbol.Number ||
				type == Symbol.True || type == Symbol.False || type == Symbol.Null)
			{
				return Consume().Value;
			}

			throw new Exception($"unexpected {type} : {currentToken.ToString()}");
		}

		public Object Parse()
		{
			Object o = Value();
			Expect(Symbol.EOF);

			return o;
		}
	}
}
