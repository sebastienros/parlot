using System;
using System.Linq;
using Parlot;

namespace Parlot.SourceGeneration;

public static class TypeNameHelper
{
    public static string GetTypeName(Type type)
    {
        ThrowHelper.ThrowIfNull(type, nameof(type));

        return GetTypeNameInternal(type);
    }

    private static string GetTypeNameInternal(Type type)
    {
        if (type.IsArray)
        {
            return $"{GetTypeNameInternal(type.GetElementType()!)}[]";
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            var baseName = definition.FullName ?? definition.Name;
            var tick = baseName.IndexOf('`');

            if (tick >= 0)
            {
                baseName = baseName[..tick];
            }

            baseName = baseName.Replace('+', '.');

            var arguments = type.GetGenericArguments()
                .Select(GetTypeNameInternal);

            return $"global::{baseName}<{string.Join(", ", arguments)}>";
        }

        var name = type.FullName ?? type.Name;
        name = name.Replace('+', '.');

        return $"global::{name}";
    }
}
