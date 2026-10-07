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
	}
}
