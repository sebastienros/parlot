using System;

namespace Parlot.Fluent;

/// <summary>
/// What an identifier built with code point predicates accepts besides the chars of the text, see
/// <c>Identifier(Func&lt;int, bool&gt;, Func&lt;int, bool&gt;, IdentifierOptions)</c>.
/// </summary>
[Flags]
public enum IdentifierOptions
{
    /// <summary>
    /// Each char of the text is a code point given to the predicates, surrogates included.
    /// </summary>
    None = 0,

    /// <summary>
    /// <c>\uXXXX</c>, with four hexadecimal digits, is a char whose code point the predicates accept, as in
    /// JavaScript, Java and C#.
    /// </summary>
    UnicodeEscapes = 1,

    /// <summary>
    /// <c>\u{X...}</c>, with one or more hexadecimal digits up to <c>10FFFF</c>, is a char whose code point the
    /// predicates accept, as in JavaScript.
    /// </summary>
    CodePointEscapes = 2,

    /// <summary>
    /// A surrogate pair is a single char whose code point the predicates accept, such that identifiers can contain chars
    /// outside the Basic Multilingual Plane, like <c>𝒜</c> (U+1D49C). A lone surrogate is never accepted.
    /// </summary>
    SurrogatePairs = 4,

    /// <summary>
    /// The identifiers of JavaScript: <see cref="UnicodeEscapes"/>, <see cref="CodePointEscapes"/> and
    /// <see cref="SurrogatePairs"/>.
    /// </summary>
    JavaScript = UnicodeEscapes | CodePointEscapes | SurrogatePairs,
}
