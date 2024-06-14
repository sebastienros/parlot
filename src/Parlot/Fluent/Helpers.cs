using System.Globalization;

namespace Parlot.Fluent;

internal static class Helpers
{
    internal static NumberStyles ToNumberStyles(this NumberOptions numberOptions)
    {
        var numberStyles = NumberStyles.None;

        if (numberOptions.HasFlag(NumberOptions.AllowLeadingSign))
        {
            numberStyles |= NumberStyles.AllowLeadingSign;
        }

        if (numberOptions.HasFlag(NumberOptions.AllowDecimalSeparator))
        {
            numberStyles |= NumberStyles.AllowDecimalPoint;
        }

        if (numberOptions.HasFlag(NumberOptions.AllowGroupSeparators))
        {
            numberStyles |= NumberStyles.AllowThousands;
        }

        if (numberOptions.HasFlag(NumberOptions.AllowExponent))
        {
            numberStyles |= NumberStyles.AllowExponent;
        }

        return numberStyles;
    }
}
