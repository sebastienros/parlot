namespace Parlot
{
    public record ScanResult<T>
    {
        public ScanResult(bool success)
        {
            Success = success;
        }

        public ScanResult(Token<T> token)
        {
            Success = true;
            Token = token;
        }

        public Token<T> Token { get; set; } = Token<T>.Empty;
        public bool Success { get; set; }

        public static implicit operator bool(ScanResult<T> result)
        {
            return result.Success;
        }

        public static implicit operator ScanResult<T>(bool _)
        {
            return new ScanResult<T>(false);
        }
    }
}
