using System;
using System.IO;
using System.Text;

namespace HaversineRelease
{
	enum Symbol
	{
		LeftBrace, RightBrace, LeftBracket, RightBracket, Colon,
		Comma, String, Number, True, False, Null, EOF
	}

	class Token
	{
		public Symbol Type = Symbol.Null;

		public int LineNumber = 0;
		public int CharacterNumber = 0;

		public override string ToString()
		{
			return $"{Type} ({LineNumber},{CharacterNumber})";
		}

		public Token() {}

		public Token(Symbol inType, int inLineNumber, int inCharacterNumber)
		{
			Type = inType;
			LineNumber = inLineNumber;
			CharacterNumber = inCharacterNumber;
		}
	}

	class Lexer
	{
		int lineNumber = 1;
		int characterNumber;
		List<Symbol> stack = new List<Symbol>();

		public void ReadFile(string path)
		{
			using (StreamReader reader = File.OpenText(path))
			{
				while (true)
				{
					Token token = GetNextToken(reader);

					if (token.Type == Symbol.EOF)
					{
						break;
					}
				}
			}
		}

		Token GetNextToken(StreamReader reader)
		{
			ReadWhitespace(reader);
			Token token = new Token(Symbol.Null, lineNumber, characterNumber);
			int peekValue = reader.Peek();

			if (peekValue == -1)
			{
				token.Type = Symbol.EOF;

				return token;
			}

			char character = (char)peekValue;

			switch(character)
			{
				case '{':
					token.Type = Symbol.LeftBrace;
					Advance(reader);
					break;
				case '}':
					token.Type = Symbol.RightBrace;
					Advance(reader);
					break;
				case '[':
					token.Type = Symbol.LeftBracket;
					Advance(reader);
					break;
				case ']':
					token.Type = Symbol.RightBracket;
					Advance(reader);
					break;
				case ':':
					token.Type = Symbol.Colon;
					Advance(reader);
					break;
				case ',':
					token.Type = Symbol.Comma;
					Advance(reader);
					break;
				default:
					break;
			}

			return token;
		}

		int Advance(StreamReader reader)
		{
			characterNumber++;

			return reader.Read();
		}

		void ReadWhitespace(StreamReader reader)
		{
			char c;
			int peekValue;

			while((peekValue = reader.Peek()) >= 0)
			{
				c = (char)peekValue;

				if (c == '\n')
				{
					lineNumber++;
					characterNumber = 0;
					Advance(reader);
				}
				else if ((c == ' ') || (c == '\r') || (c == '\t'))
				{
					Advance(reader);
				}
				else
				{
					break;
				}
			}
		}

		public void DisplayError(string errorMessage)
		{
			throw new Exception(errorMessage);
		}

		public void Advance()
		{
		}

		public Symbol Peek()
		{
			return Symbol.EOF;
		}
	}
}
