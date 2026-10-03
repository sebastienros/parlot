using System;
using System.Collections.Generic;
using Parlot;

namespace Parlot.SourceGeneration;

/// <summary>
/// Represents the context of a source-generation phase, coordinating all the parsers involved.
/// </summary>
public sealed class SourceGenerationContext
{
    private int _number;
    private int _staticFieldNumber;

    public SourceGenerationContext(string parseContextName, string? methodNamePrefix)
        : this(parseContextName, methodNamePrefix, targetFramework: null, csharpLanguageMajorVersion: 0)
    {
    }

    public SourceGenerationContext(string parseContextName = "context", string? methodNamePrefix = null, TargetFrameworkInfo? targetFramework = null)
        : this(parseContextName, methodNamePrefix, targetFramework, csharpLanguageMajorVersion: 0)
    {
    }

    public SourceGenerationContext(
        string parseContextName,
        string? methodNamePrefix,
        TargetFrameworkInfo? targetFramework,
        int csharpLanguageMajorVersion)
    {
        ParseContextName = parseContextName ?? throw new ArgumentNullException(nameof(parseContextName));
        MethodNamePrefix = methodNamePrefix ?? "";
        TargetFramework = targetFramework ?? TargetFrameworkInfo.Unknown;
        CSharpLanguageMajorVersion = csharpLanguageMajorVersion;

        Helpers = new ParserHelperRegistry(this);
    }

    /// <summary>
    /// Sets the source code map from lambda pointers to their original source code.
    /// This should be called before executing the parser factory method.
    /// </summary>
    public void SetLambdaSourceMap(Dictionary<int, string> pointerToSource)
    {
        Lambdas.SetSourceCodeMap(pointerToSource);
    }

    /// <summary>
    /// Name of the <c>ParseContext</c> parameter in the generated methods.
    /// </summary>
    public string ParseContextName { get; }

    /// <summary>
    /// Name of the cached cursor variable in the generated method.
    /// This is initialized once at the start of the method and reused throughout.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static - keep as instance for API consistency
    public string CursorName => "cursor";
#pragma warning restore CA1822

    /// <summary>
    /// Name of the cached scanner variable in the generated method.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static - keep as instance for API consistency
    public string ScannerName => "scanner";
#pragma warning restore CA1822

    /// <summary>
    /// Prefix for generated lambda field names to ensure uniqueness across multiple parsers in the same class.
    /// </summary>
    public string MethodNamePrefix { get; }

    /// <summary>
    /// Target framework for the current compilation.
    /// </summary>
    public TargetFrameworkInfo TargetFramework { get; }

    /// <summary>
    /// Major C# language version of the consuming project (e.g. 9, 10, 11...).
    /// A value of 0 means "unknown".
    /// </summary>
    public int CSharpLanguageMajorVersion { get; }

    /// <summary>
    /// Whether the consuming project supports C# 9 switch pattern syntax (relational and <c>or</c> patterns).
    /// </summary>
    public bool SupportsCSharp9SwitchPatterns => CSharpLanguageMajorVersion >= 9;

    /// <summary>
    /// Global locals (as code lines) for the generated root method.
    /// </summary>
    public IList<string> GlobalLocals { get; } = new List<string>();

    /// <summary>
    /// Global body statements for the generated root method.
    /// </summary>
    public IList<string> GlobalBody { get; } = new List<string>();

    /// <summary>
    /// Static field declarations that should appear at class level.
    /// Each entry is a complete field declaration (e.g., "private static readonly SearchValues&lt;char&gt; _field = ...;").
    /// </summary>
    public IList<string> StaticFields { get; } = new List<string>();

    /// <summary>
    /// Registry of user-provided delegates used by parsers such as <c>Then</c>.
    /// </summary>
    public LambdaRegistry Lambdas { get; } = new();

    /// <summary>
    /// Registry of deferred parsers that should become separate helper methods.
    /// </summary>
    public DeferredRegistry Deferred { get; } = new();

    /// <summary>
    /// Registry of helper parser methods (e.g., OneOf buckets) to emit once per descriptor and result mode.
    /// </summary>
    public ParserHelperRegistry Helpers { get; }

    /// <summary>
    /// Gets or sets whether the current source-generation phase should ignore the results of the parsers.
    /// </summary>
    /// <remarks>
    /// When set to true, the generated statements don't need to record and define the result value.
    /// This is done to optimize generated parsers that are usually used for pattern matching only (e.g., Capture).
    /// Inputs consumed by callbacks must still be generated with this flag set to false.
    /// </remarks>
    public bool DiscardResult { get; set; }

    /// <summary>
    /// Gets or sets whether the generated code reads a compacting stream buffer, for a <c>TextReader</c> entry point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cursor then only holds a window of the input. Parsers reading the cursor directly (tokens) must generate their
    /// code through <see cref="GenerateToken(object, Type, Func{SourceResult})"/>, and parsers moving the cursor back to
    /// read it again must keep their position buffered with <see cref="Pin(SourceResult)"/>, see <c>docs/streaming.md</c>.
    /// </para>
    /// <para>It is <see langword="false"/> while the code of a token is generated, and for <c>string</c> entry points.</para>
    /// </remarks>
    public bool IsCompacting { get; set; }

    /// <summary>
    /// Generates the code of a token, a parser that reads the cursor directly, when <see cref="IsCompacting"/> is set.
    /// </summary>
    /// <remarks>
    /// The code from <paramref name="generateBody"/> becomes a helper method, generated with <see cref="IsCompacting"/>
    /// unset. The returned code calls it, and when the input is streamed calls it again from the same position with
    /// more text when it read to the end of the buffer, like <c>ParseContext.ParseToken</c>.
    /// </remarks>
    /// <param name="parser">The token parser.</param>
    /// <param name="valueType">The type of the parser's value.</param>
    /// <param name="generateBody">Generates the code of the token for a string.</param>
    /// <returns>The code invoking the token.</returns>
    public SourceResult GenerateToken(object parser, Type valueType, Func<SourceResult> generateBody)
    {
        ThrowHelper.ThrowIfNull(parser, nameof(parser));
        ThrowHelper.ThrowIfNull(valueType, nameof(valueType));
        ThrowHelper.ThrowIfNull(generateBody, nameof(generateBody));

        var valueTypeName = GetTypeName(valueType);
        var helperName = Helpers.GetOrCreate(
            new TokenKey(parser),
            $"{MethodNamePrefix}_Token",
            valueTypeName,
            () =>
            {
                IsCompacting = false;

                try
                {
                    return generateBody();
                }
                finally
                {
                    IsCompacting = true;
                }
            }).MethodName;

        var result = CreateResult(valueType);
        var ctx = ParseContextName;
        var outTarget = DiscardResult ? "_" : result.ValueVariable;
        var startName = $"tokenStart{NextNumber()}";

        result.Body.Add($"if ({ctx}.IsCompacting)");
        result.Body.Add("{");
        result.Body.Add($"    var {startName} = {ctx}.BeginToken();");
        result.Body.Add("    try");
        result.Body.Add("    {");
        result.Body.Add("        while (true)");
        result.Body.Add("        {");
        result.Body.Add("            try");
        result.Body.Add("            {");
        result.Body.Add($"                {result.SuccessVariable} = {helperName}({ctx}, out {outTarget});");
        result.Body.Add("            }");
        result.Body.Add($"            catch (global::Parlot.ParseException) when ({ctx}.Scanner.Cursor.HitEnd)");
        result.Body.Add("            {");
        result.Body.Add($"                {result.SuccessVariable} = false;");
        result.Body.Add("            }");
        result.Body.Add($"            if (!{ctx}.RetryToken({startName}))");
        result.Body.Add("            {");
        result.Body.Add("                break;");
        result.Body.Add("            }");
        result.Body.Add("        }");
        result.Body.Add("    }");
        result.Body.Add("    finally");
        result.Body.Add("    {");
        result.Body.Add($"        {ctx}.EndToken();");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out {outTarget});");
        result.Body.Add("}");

        return result;
    }

    /// <summary>
    /// Keeps the current position buffered when <see cref="IsCompacting"/> is set, such that the generated code can move the
    /// cursor back to it and read it again. Emits nothing otherwise.
    /// </summary>
    /// <param name="result">The result to emit the code in.</param>
    /// <returns>The variable to pass to <see cref="Unpin(SourceResult, string?, string)"/> and <see cref="MovePin(SourceResult, string?, string)"/>, or <see langword="null"/>.</returns>
    public string? Pin(SourceResult result)
    {
        ThrowHelper.ThrowIfNull(result, nameof(result));

        if (!IsCompacting)
        {
            return null;
        }

        var pinName = $"pin{NextNumber()}";
        result.Body.Add($"var {pinName} = {ParseContextName}.Pin();");
        return pinName;
    }

    /// <summary>
    /// Releases a position kept by <see cref="Pin(SourceResult)"/>.
    /// </summary>
    /// <param name="result">The result to emit the code in.</param>
    /// <param name="pinName">The value returned by <see cref="Pin(SourceResult)"/>.</param>
    /// <param name="indent">The indentation of the emitted statement.</param>
    public void Unpin(SourceResult result, string? pinName, string indent = "")
    {
        ThrowHelper.ThrowIfNull(result, nameof(result));

        if (pinName is not null)
        {
            result.Body.Add($"{indent}{ParseContextName}.Unpin({pinName});");
        }
    }

    /// <summary>
    /// Moves a position kept by <see cref="Pin(SourceResult)"/> to the current position, releasing the text before it.
    /// </summary>
    /// <param name="result">The result to emit the code in.</param>
    /// <param name="pinName">The value returned by <see cref="Pin(SourceResult)"/>.</param>
    /// <param name="indent">The indentation of the emitted statement.</param>
    public void MovePin(SourceResult result, string? pinName, string indent = "")
    {
        ThrowHelper.ThrowIfNull(result, nameof(result));

        if (pinName is not null)
        {
            result.Body.Add($"{indent}{ParseContextName}.MovePin({pinName});");
        }
    }

    // Emits a call whose position stays buffered while it runs, and returns the expression of its result
    internal string PinnedCall(SourceResult result, string call, string indent = "")
    {
        if (!IsCompacting)
        {
            return call;
        }

        var pinName = $"pin{NextNumber()}";
        var successName = $"pinned{NextNumber()}";
        result.Body.Add($"{indent}var {pinName} = {ParseContextName}.Pin();");
        result.Body.Add($"{indent}var {successName} = {call};");
        result.Body.Add($"{indent}{ParseContextName}.Unpin({pinName});");
        return successName;
    }

    private sealed class TokenKey
    {
        private readonly object _parser;

        public TokenKey(object parser)
        {
            _parser = parser;
        }

        public override bool Equals(object? obj) => obj is TokenKey other && ReferenceEquals(other._parser, _parser);

        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(_parser);
    }

    internal TResult WithDiscardResult<TResult>(bool discardResult, Func<TResult> action)
    {
        var previous = DiscardResult;
        DiscardResult = discardResult;
        try
        {
            return action();
        }
        finally
        {
            DiscardResult = previous;
        }
    }

    /// <summary>
    /// Returns a new unique number for the current compilation.
    /// </summary>
    public int NextNumber() => _number++;

    /// <summary>
    /// Registers a static field and returns its unique name.
    /// </summary>
    /// <param name="declaration">The field declaration without the field name (e.g., "private static readonly SearchValues&lt;char&gt;").</param>
    /// <param name="initializer">The initializer expression (e.g., "SearchValues.Create(\"abc\")").</param>
    /// <returns>The unique field name that was generated.</returns>
    public string RegisterStaticField(string declaration, string initializer)
    {
        ThrowHelper.ThrowIfNull(declaration, nameof(declaration));
        ThrowHelper.ThrowIfNull(initializer, nameof(initializer));

        var fieldName = $"_{MethodNamePrefix}_static{_staticFieldNumber++}";
        StaticFields.Add($"{declaration} {fieldName} = {initializer};");
        return fieldName;
    }

    /// <summary>
    /// Creates a new <see cref="SourceResult"/> with conventional success and value names.
    /// </summary>
    public SourceResult CreateResult(Type valueType, bool defaultSuccess = false, string? defaultValueExpression = null)
    {
        ThrowHelper.ThrowIfNull(valueType, nameof(valueType));

        var successName = $"success{NextNumber()}";
        var valueName = $"value{NextNumber()}";
        var valueTypeName = GetTypeName(valueType);

        var result = new SourceResult(successName, valueName, valueTypeName);

        var successInit = defaultSuccess ? "true" : "false";
        result.Locals.Add($"bool {successName} = {successInit};");

        var defaultValueExpr = defaultValueExpression ?? "default";
        result.Locals.Add($"{valueTypeName} {valueName} = {defaultValueExpr};");

        return result;
    }

    public static string GetTypeName(Type type) => TypeNameHelper.GetTypeName(type);

    public string RegisterLambda(Delegate lambda)
    {
        var id = Lambdas.Register(lambda);
        return GetLambdaFieldName(id);
    }

    /// <summary>
    /// Returns a field name for a lambda that is unique to this method context.
    /// </summary>
    public string GetLambdaFieldName(int id) => $"_{MethodNamePrefix}_lambda{id}";
}
