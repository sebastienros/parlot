using System;

namespace Parlot.Fluent
{
    [Flags]
    public enum NumberOptions
    {
        /// <summary>
        /// Indicates that no style elements, such as leading sign, thousands
        /// separators, decimal separator or exponent, can be present in the parsed string.
        /// The string to be parsed must consist of integral decimal digits only.        
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates that the numeric string can have a leading sign. Valid leading sign
        /// characters are plus (+) and minus (-).
        /// </summary>
        AllowLeadingSign = 1,

        /// <summary>
        /// Indicates that the numeric string can have a decimal separator. By default it uses dot (.) as the separator.
        /// </summary>
        AllowDecimalSeparator = 2,

        /// <summary>
        /// Indicates that the numeric string can have group separators, such as symbols
        /// that separate hundreds from thousands. the default group separator is comma (,).
        /// </summary>
        AllowGroupSeparators = 4,

        /// <summary>
        /// Indicates that the numeric string can be in exponential notation. It
        /// allows the parsed string to contain an exponent that begins with the "E"
        /// or "e" character and that is followed by an optional positive or negative sign
        /// and an integer.
        /// </summary>
        AllowExponent = 8,

        /// <summary>
        /// Indicates that the <see cref="AllowLeadingSign"/>
        /// style is used. This is a composite number style.
        /// </summary>
        Integer = AllowLeadingSign,

        /// <summary>
        /// Indicates that the <see cref="AllowLeadingSign"/>, <see cref="AllowDecimalSeparator"/>, <see cref="AllowGroupSeparators"/>
        /// styles are used. This is a composite number style.
        /// </summary>
        Number = AllowLeadingSign | AllowDecimalSeparator | AllowGroupSeparators,

        /// <summary>
        /// Indicates that the <see cref="AllowLeadingSign"/>, <see cref="AllowDecimalSeparator"/>, <see cref="AllowExponent"/>
        /// styles are used. This is a composite number style.
        /// </summary>
        Float = AllowLeadingSign | AllowDecimalSeparator | AllowExponent,

        /// <summary>
        /// Indicates that all options are used.
        /// This is a composite number style.
        /// </summary>
        Any = AllowLeadingSign | AllowDecimalSeparator |AllowGroupSeparators | AllowExponent,
    }
}
