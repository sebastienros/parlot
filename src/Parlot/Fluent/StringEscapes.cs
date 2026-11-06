using System;

namespace Parlot.Fluent;

/// <summary>
/// The escape sequences a string literal accepts, how it decodes them, and the raw chars it rejects.
/// </summary>
/// <remarks>
/// <para>
/// Each flag adds a family of escape sequences, so a language that isn't one of the presets combines its own, like
/// <c>StringEscapes.Default | StringEscapes.AnyChar</c>. Whenever a flag adds escape sequences that start with a backslash,
/// <c>\\</c> is a backslash too. Without any of them, as with <see cref="None"/> or <see cref="Sql"/>, a backslash is an
/// ordinary char.
/// </para>
/// <para>
/// A sequence that a flag claims but that isn't complete, like <c>\u12</c> with <see cref="Unicode"/>, fails the string,
/// even with <see cref="AnyChar"/> or <see cref="KeepUnknown"/>, which only apply to the chars no flag claims.
/// </para>
/// </remarks>
[Flags]
public enum StringEscapes
{
    /// <summary>
    /// No escape sequence: the string ends at the first quote, and a backslash is an ordinary char.
    /// </summary>
    None = 0,

    /// <summary>
    /// A backslash followed by the quote that delimits the string, whichever it is, stands for that quote.
    /// </summary>
    EscapedQuote = 1 << 0,

    /// <summary>
    /// <c>\'</c> and <c>\"</c>, whichever quote delimits the string.
    /// </summary>
    Quotes = 1 << 1,

    /// <summary>
    /// <c>\n</c>, <c>\r</c> and <c>\t</c>.
    /// </summary>
    NewLineAndTab = 1 << 2,

    /// <summary>
    /// <c>\b</c> (U+0008) and <c>\f</c> (U+000C).
    /// </summary>
    BackspaceAndFormFeed = 1 << 3,

    /// <summary>
    /// <c>\v</c> (U+000B).
    /// </summary>
    VerticalTab = 1 << 4,

    /// <summary>
    /// <c>\a</c> (U+0007).
    /// </summary>
    Alert = 1 << 5,

    /// <summary>
    /// <c>\0</c>, the null char. With <see cref="Octal"/>, <c>\0</c> starts an octal escape instead.
    /// </summary>
    Null = 1 << 6,

    /// <summary>
    /// <c>\e</c> (U+001B).
    /// </summary>
    Escape = 1 << 7,

    /// <summary>
    /// <c>\/</c>, as in JSON.
    /// </summary>
    Slash = 1 << 8,

    /// <summary>
    /// <c>\?</c>, as in C.
    /// </summary>
    QuestionMark = 1 << 9,

    /// <summary>
    /// <c>\s</c>, a space, as in Java.
    /// </summary>
    Space = 1 << 10,

    /// <summary>
    /// <c>\u</c> followed by exactly four hex digits.
    /// </summary>
    Unicode = 1 << 11,

    /// <summary>
    /// <c>\U</c> followed by exactly eight hex digits, a code point up to U+10FFFF, decoded to a surrogate pair above U+FFFF.
    /// </summary>
    LongUnicode = 1 << 12,

    /// <summary>
    /// <c>\u{</c> followed by one or more hex digits and <c>}</c>, a code point up to U+10FFFF, decoded to a surrogate
    /// pair above U+FFFF.
    /// </summary>
    BracedUnicode = 1 << 13,

    /// <summary>
    /// <c>\x</c> followed by one to four hex digits, as in C#: <c>\x41B</c> is U+041B.
    /// </summary>
    Hex = 1 << 14,

    /// <summary>
    /// <c>\x</c> followed by exactly two hex digits: <c>\x41B</c> is <c>AB</c>. It takes precedence over <see cref="Hex"/>.
    /// </summary>
    TwoDigitHex = 1 << 15,

    /// <summary>
    /// A backslash followed by octal digits, a char up to <c>\377</c>: <c>\0</c> to <c>\3</c> followed by up to two more
    /// octal digits, <c>\4</c> to <c>\7</c> by up to one, as in JavaScript (sloppy mode) and Java.
    /// </summary>
    Octal = 1 << 16,

    /// <summary>
    /// A backslash followed by a char no other flag claims stands for that char, as <c>\$</c> for <c>$</c>.
    /// </summary>
    AnyChar = 1 << 17,

    /// <summary>
    /// A backslash followed by a char no other flag claims is kept as is, backslash included, as in Python. It takes
    /// precedence over <see cref="AnyChar"/>.
    /// </summary>
    KeepUnknown = 1 << 18,

    /// <summary>
    /// A backslash followed by a line terminator (<c>\n</c>, <c>\r\n</c>, <c>\r</c>, U+2028 or U+2029) continues the string
    /// on the next line: both are removed from the value.
    /// </summary>
    LineContinuation = 1 << 19,

    /// <summary>
    /// Two quotes that delimit the string stand for one, as <c>'it''s'</c> in SQL. This doesn't need a backslash.
    /// </summary>
    DoubledQuote = 1 << 20,

    /// <summary>
    /// A raw <c>\r</c> or <c>\n</c> fails the string. A line continuation is still allowed.
    /// </summary>
    SingleLine = 1 << 21,

    /// <summary>
    /// A raw control char, U+0000 to U+001F, fails the string, as in JSON.
    /// </summary>
    NoControlChars = 1 << 22,

    /// <summary>
    /// The escape sequences of <c>String()</c> when none are given: <c>\0 \\ \a \b \f \n \r \t \v \' \"</c>, an escaped
    /// quote, <c>\u</c> followed by four hex digits and <c>\x</c> followed by one to four hex digits. Raw new lines are allowed.
    /// </summary>
    Default = EscapedQuote | Quotes | NewLineAndTab | BackspaceAndFormFeed | VerticalTab | Alert | Null | Unicode | Hex,

    /// <summary>
    /// The escape sequences of C# regular string literals: <see cref="Default"/> with <c>\U</c> and <c>\e</c>, on a single line.
    /// </summary>
    CSharp = Default | LongUnicode | Escape | SingleLine,

    /// <summary>
    /// The escape sequences of JavaScript string literals, legacy octal escapes and line continuations included, on a single
    /// line. Template literals, which can span lines, can use it without <see cref="SingleLine"/>.
    /// </summary>
    JavaScript = EscapedQuote | Quotes | NewLineAndTab | BackspaceAndFormFeed | VerticalTab | Null | Unicode | BracedUnicode | TwoDigitHex | Octal | AnyChar | LineContinuation | SingleLine,

    /// <summary>
    /// The escape sequences of JSON strings, which can't contain raw control chars: <c>\" \\ \/ \b \f \n \r \t</c> and
    /// <c>\u</c> followed by four hex digits.
    /// </summary>
    Json = EscapedQuote | Slash | NewLineAndTab | BackspaceAndFormFeed | Unicode | NoControlChars,

    /// <summary>
    /// The escape sequences of Python string literals, on a single line: unknown escape sequences are kept as is. Octal
    /// escapes go up to <c>\377</c>, and <c>\N{...}</c> isn't supported.
    /// </summary>
    Python = EscapedQuote | Quotes | NewLineAndTab | BackspaceAndFormFeed | VerticalTab | Alert | Octal | TwoDigitHex | Unicode | LongUnicode | LineContinuation | KeepUnknown | SingleLine,

    /// <summary>
    /// The escape sequences of Java string literals, on a single line.
    /// </summary>
    Java = EscapedQuote | Quotes | NewLineAndTab | BackspaceAndFormFeed | Space | Octal | Unicode | SingleLine,

    /// <summary>
    /// Standard SQL string literals: a doubled quote stands for one, and a backslash is an ordinary char.
    /// </summary>
    Sql = DoubledQuote,
}
