// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Style", "IDE0290:Use primary constructor", Justification = "I don't like how it looks", Scope = "module")]
[assembly: SuppressMessage("Performance", "CA1510:Use ArgumentNullException throw helper", Justification = "Not available in all target frameworks", Scope = "module")]
