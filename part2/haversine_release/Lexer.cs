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

		public Token(Symbol inType, int inLineNumber, int inCharacterNumber)
		{
			Type = inType;
			LineNumber = inLineNumber;
			CharacterNumber = inCharacterNumber;
		}
	}

	class Lexer
	{
		int lineNumber;
		int characterNumber;

		public void ReadFile(string path)
		{
		}
	}
}
