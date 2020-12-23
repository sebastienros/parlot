using System.Runtime.CompilerServices;

namespace Parlot
{
    public class TokenResult<T>
    {
        public Token<T> Token { get; private set; } = Token<T>.Empty;
        
        public void SetToken(Token<T> token)
        {
            Token = token;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetToken(T type, string buffer, TextPosition start, TextPosition end)
        {
            Token = new Token<T>(type, buffer, start, end);
        }
    }
}
