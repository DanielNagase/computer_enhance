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

	enum Number
	{
		Float, Int, None
	}

	class Token
	{
		public Symbol Type = Symbol.Null;

		public int LineNumber = 0;
		public int CharacterNumber = 0;

		public string StringValue = "";

		public Number NumberType = Number.None;
		public f64 FloatValue = 0.0f;
		public Int32 IntValue = 0;

		public override string ToString()
		{
			string output = $"{Type} ({LineNumber},{CharacterNumber}): ";

			if (Type == Symbol.String)
			{
				output += StringValue;
			}

			return output;
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
				while (!reader.EndOfStream)
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

			if (character == '-' || Char.IsDigit(character))
			{
				ReadNumber(reader, token);

				return token;
			}

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
				case 't':
					ReadBool(reader, token);
					break;
				case 'f':
					ReadBool(reader, token);
					break;
				case '"':
					ReadString(reader, token);
					break;
				default:
					DisplayError($"Unexpected '{character}' at line {lineNumber}, position {characterNumber}");
					break;
			}

			return token;
		}

		int Advance(StreamReader reader)
		{
			characterNumber++;

			return reader.Read();
		}

		void ReadBool(StreamReader reader, Token token)
		{
			char character = (char)reader.Peek();
			int count = 0;

			if (character == 't')
			{
				count = 4;
			}
			else if (character == 'f')
			{
				count = 5;
			}

			char[] buffer = new char[5];
			int charactersRead = reader.ReadBlock(buffer, 0, count);
			characterNumber += charactersRead;
			string inputString = new String(buffer, 0, charactersRead);

			if (inputString.Equals("true"))
			{
				token.Type = Symbol.True;
			}
			else if (inputString.Equals("false"))
			{
				token.Type = Symbol.False;
			}
		}

		void ReadNumber(StreamReader reader, Token token)
		{
			token.Type = Symbol.Number;
			StringBuilder builder = new StringBuilder("");

			bool bDidReadInteger = ReadInteger(reader, builder);
			bool bDidReadFloat = ReadFraction(reader, builder) && ReadExponent(reader, builder);

			bool bDidParseString = false;
			f64 floatValue = 0.0f;
			int intValue = 0;

			if (bDidReadFloat)
			{
				bDidParseString = f64.TryParse(builder.ToString(), out floatValue);
				token.NumberType = Number.Float;
				token.FloatValue = floatValue;
			}
			else
			{
				bDidParseString = Int32.TryParse(builder.ToString(), out intValue);
				token.NumberType = Number.Int;
				token.IntValue = intValue;
			}
		}

		bool ReadInteger(StreamReader reader, StringBuilder builder)
		{
			return (ReadChar(reader, builder, '-') && ReadOneNine(reader, builder) && ReadDigits(reader, builder)) ||
				(ReadChar(reader, builder, '-') && ReadDigit(reader, builder)) ||
				(ReadOneNine(reader, builder) && ReadDigits(reader, builder)) ||
				(ReadDigit(reader, builder));
		}

		bool ReadFraction(StreamReader reader, StringBuilder builder)
		{
			return (ReadChar(reader, builder, '.') && ReadDigits(reader, builder)) || true;
		}

		bool ReadExponent(StreamReader reader, StringBuilder builder)
		{
			return (ReadChar(reader, builder, 'e') && ReadSign(reader, builder) && ReadDigits(reader, builder)) ||
			(ReadChar(reader, builder, 'E') && ReadSign(reader, builder) && ReadDigits(reader, builder)) ||
				true;
		}

		bool ReadSign(StreamReader reader, StringBuilder builder)
		{
			return ReadChar(reader, builder, '-') || ReadChar(reader, builder, '+') || true;
		}

		bool ReadDigits(StreamReader reader, StringBuilder builder)
		{
			return (ReadDigit(reader, builder) && ReadDigits(reader, builder)) ||
				ReadDigit(reader, builder);
		}

		bool ReadDigit(StreamReader reader, StringBuilder builder)
		{
			return ReadChar(reader, builder, '0') || ReadOneNine(reader, builder);
		}

		bool ReadOneNine(StreamReader reader, StringBuilder builder)
		{
			char c = (char)reader.Peek();

			if (c != '0' && Char.IsDigit(c))
			{
				builder.Append((char)Advance(reader));

				return true;
			}

			return false;
		}

		bool ReadChar(StreamReader reader, StringBuilder builder, char expectedCharacter)
		{
			char c = (char)reader.Peek();

			if (c == expectedCharacter)
			{
				builder.Append((char)Advance(reader));

				return true;
			}

			return false;
		}

		void ReadString(StreamReader reader, Token token)
		{
			token.Type = Symbol.String;

			char c;
			int peekValue;
			int quoteCount = 0;
			StringBuilder builder = new StringBuilder("");

			while((peekValue = reader.Peek()) >= 0)
			{
				c = (char)peekValue;

				if (c == '"')
				{
					Advance(reader);
					quoteCount++;

					if (quoteCount == 2)
					{
						break;
					}
				}
				else
				{
					c = (char)Advance(reader);
					builder.Append(c);
				}
			}

			token.StringValue = builder.ToString();
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
