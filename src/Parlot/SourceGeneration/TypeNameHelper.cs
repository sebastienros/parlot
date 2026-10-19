using System;
using System.Text;
using Parlot;

namespace Parlot.SourceGeneration;

public static class TypeNameHelper
{
    public static string GetTypeName(Type type)
    {
        ThrowHelper.ThrowIfNull(type, nameof(type));

        var sb = new StringBuilder();
        AppendTypeName(sb, type);
        return sb.ToString();
    }

    private static void AppendTypeName(StringBuilder sb, Type type)
    {
        if (type.IsArray)
        {
            // C# writes the outermost rank first: typeof(int[,][]) is a rank-2 array of int[],
            // so the ranks are collected from the outside in before the element type is emitted.
            var element = type;
            var ranks = new StringBuilder();

            while (element.IsArray)
            {
                ranks.Append('[').Append(',', element.GetArrayRank() - 1).Append(']');
                element = element.GetElementType()!;
            }

            AppendTypeName(sb, element);
            sb.Append(ranks);
            return;
        }

        sb.Append("global::");

        if (type.IsGenericType)
        {
            // A nested generic type carries the type arguments of its declaring types as well, in
            // declaration order, so each level of nesting takes its own share of them.
            var arguments = type.GetGenericArguments();
            AppendGenericTypeName(sb, type.GetGenericTypeDefinition(), arguments);
            return;
        }

        var name = type.FullName ?? type.Name;
        sb.Append(name.Replace('+', '.'));
    }

    private static int AppendGenericTypeName(StringBuilder sb, Type definition, Type[] arguments)
    {
        var declaring = definition.DeclaringType;
        var consumed = 0;

        if (declaring is not null)
        {
            if (declaring.IsGenericTypeDefinition)
            {
                consumed = AppendGenericTypeName(sb, declaring, arguments);
            }
            else
            {
                var declaringName = declaring.FullName ?? declaring.Name;
                sb.Append(declaringName.Replace('+', '.'));
            }

            sb.Append('.');
        }
        else if (!string.IsNullOrEmpty(definition.Namespace))
        {
            sb.Append(definition.Namespace).Append('.');
        }

        var name = definition.Name;
        var tick = name.IndexOf('`');

        if (tick >= 0)
        {
            name = name.Substring(0, tick);
        }

        sb.Append(name);

        var arity = definition.IsGenericTypeDefinition ? definition.GetGenericArguments().Length : 0;
        var own = arity - consumed;

        if (own > 0)
        {
            sb.Append('<');

            for (var i = 0; i < own; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                AppendTypeName(sb, arguments[consumed + i]);
            }

            sb.Append('>');
        }

        return arity;
    }
}
