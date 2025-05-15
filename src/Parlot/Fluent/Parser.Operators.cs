using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

public abstract partial class Parser<T>
{
    public static Parser<T> operator >>>(Parser<T> left, Parser<T> right) => left.Or(right);
    public static Parser<string> operator >>(Parser<T> left,  string right) => left.Then(right);
    public static Parser<T> operator <<(Parser<T> left, T right) => left.Then(right);
    public static Parser<T> operator *(Parser<T> left, Parser<T> right) => left.Or(right);
    public static Parser<T> operator /(Parser<T> left, Parser<T> right) => left.Or(right);
    public static Parser<T> operator >=(Parser<T> left, Parser<T> right) => left.Or(right);
    public static Parser<T> operator <=(Parser<T> left, Parser<T> right) => left.Or(right);
    public static Parser<T> operator >(Parser<T> left, Parser<T> right) => left.SkipAnd(right);
    public static Parser<T> operator <(Parser<T> left, Parser<T> right) => left.AndSkip(right);

    public static Parser<T> operator |(Parser<T> left, Parser<T> right) => left.Or(right);
    public static Parser<T> operator &(Parser<T> left, Parser<T> right) => left.SkipAnd(right);
    public static Sequence<T, T> operator +(Parser<T> left, Parser<T> right) => left.And(right);
    public static Parser<T> operator -(Parser<T> left, T right) => left.Else(right);
    public static Parser<T> operator ^(Parser<T> left, string name) => left.Named(name);
    public static Parser<T> operator %(Parser<T> left, string name) => left.Named(name);

    // Unary operators
    public static Parser<T> operator !(Parser<T> left) => Parsers.Not(left);
    public static Parser<T> operator ^(Parser<T> left) => Parsers.Not(left);
    public static Parser<T> operator %(Parser<T> left) => Parsers.Not(left);
    public static Parser<IReadOnlyList<T>> operator +(Parser<T> left) => left.OneOrMany();
    public static Parser<IReadOnlyList<T>> operator -(Parser<T> left) => left.ZeroOrMany();
    public static Parser<IReadOnlyList<T>> operator ~(Parser<T> left) => left.Optional();
}
