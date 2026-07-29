using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Parlot;

namespace Parlot.SourceGeneration;

/// <summary>
/// Helper class for tracking lambda pointers during source generation.
/// 
/// When lambdas are rewritten by the source generator, they set <see cref="CurrentPointer"/>
/// at the start of their execution. The <see cref="LambdaRegistry.Register"/> method
/// then reads this value to associate the delegate with its source code.
/// </summary>
public static class LambdaPointer
{
    /// <summary>
    /// Thread-local storage for the current lambda pointer.
    /// Set by rewritten lambdas just before their original body executes.
    /// Read by <see cref="LambdaRegistry.Register"/> to identify the lambda.
    /// </summary>
    /// <remarks>
    /// Using thread-local storage ensures thread-safety in multi-threaded scenarios,
    /// though source generation typically runs single-threaded.
    /// </remarks>
    [ThreadStatic]
    private static int _currentPointer;

    /// <summary>
    /// Gets or sets the current lambda pointer.
    /// This is set by rewritten lambdas at the start of their execution.
    /// </summary>
    public static int CurrentPointer
    {
        get => _currentPointer;
        set => _currentPointer = value;
    }

    /// <summary>
    /// Resets the current pointer to the default (no pointer) state.
    /// </summary>
    public static void Reset()
    {
        _currentPointer = -1;
    }

    /// <summary>
    /// The prefix used for lambda pointer fields (legacy, kept for compatibility).
    /// </summary>
    public const string Prefix = "__lambda_pointer_";
}

/// <summary>
/// Keeps track of user-provided delegates (for example from <c>Then</c>) that must be available
/// to generated source code as fields or constructor parameters.
/// </summary>
public sealed class LambdaRegistry
{
    private readonly Dictionary<int, int> _pointerToId = new();
    private readonly Dictionary<int, string?> _pointerToSource = new();
    private readonly List<(int Id, Delegate Delegate, int Pointer)> _registrations = new();
    private int _nextId;

    /// <summary>
    /// Sets the source code map from lambda pointers to their source code.
    /// This should be called before executing the parser factory method.
    /// </summary>
    public void SetSourceCodeMap(Dictionary<int, string> pointerToSource)
    {
        _pointerToSource.Clear();
        foreach (var kvp in pointerToSource)
        {
            _pointerToSource[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Registers a delegate and returns its stable identifier.
    /// The delegate is invoked with default arguments to trigger pointer registration
    /// (for rewritten lambdas that set LambdaPointer.CurrentPointer at the start).
    /// </summary>
    public int Register(Delegate @delegate)
    {
        ThrowHelper.ThrowIfNull(@delegate, nameof(@delegate));

        // Reset the current pointer before invoking
        LambdaPointer.CurrentPointer = -1;
        
        // Try to invoke the delegate with default arguments to trigger pointer registration
        // The rewritten lambda sets LambdaPointer.CurrentPointer at the start of its body
        var pointer = TryInvokeForPointer(@delegate);
        
        if (pointer >= 0)
        {
            // This is a rewritten lambda - check if we've seen this pointer before
            if (_pointerToId.TryGetValue(pointer, out var existingId))
            {
                return existingId;
            }

            // New pointer - assign an ID and record the registration
            var id = _nextId++;
            _pointerToId[pointer] = id;
            _registrations.Add((id, @delegate, pointer));
            return id;
        }

        // Could not extract pointer - this might be a lambda we couldn't rewrite
        // Use a negative pointer to indicate "no source available"
        var newId = _nextId++;
        _registrations.Add((newId, @delegate, -1));
        return newId;
    }

    /// <summary>
    /// Tries to invoke a delegate with default arguments to extract its pointer.
    /// Returns the pointer value or -1 if extraction failed.
    /// </summary>
    private static int TryInvokeForPointer(Delegate @delegate)
    {
        try
        {
            var method = @delegate.Method;
            var parameters = method.GetParameters();
            
            // Build default arguments for each parameter
            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                args[i] = GetDefaultValue(paramType);
            }
            
            // Invoke the delegate - this will set LambdaPointer.CurrentPointer
            // We ignore the return value and any exceptions from the original body
            try
            {
                @delegate.DynamicInvoke(args);
            }
            catch
            {
                // Ignore exceptions from the original lambda body
                // The pointer should still be set even if the body fails
            }
            
            // Read the pointer that was set by the rewritten lambda
            return LambdaPointer.CurrentPointer;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Gets a default value for the given type that can be used for lambda invocation.
    /// </summary>
    private static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType)
        {
            return Activator.CreateInstance(type);
        }
        
        // For reference types, try some common cases
        if (type == typeof(string))
        {
            return string.Empty;
        }
        if (type == typeof(TextSpan))
        {
            return new TextSpan(string.Empty);
        }
        
        return null;
    }

    /// <summary>
    /// Sets the source code for a registered lambda by pointer.
    /// </summary>
    public void SetSourceCode(int pointer, string sourceCode)
    {
        _pointerToSource[pointer] = sourceCode;
    }

    /// <summary>
    /// Gets the source code for a registered lambda by ID, or null if not available.
    /// </summary>
    public string? GetSourceCode(int id)
    {
        // Find the registration with this ID
        foreach (var (regId, _, pointer) in _registrations)
        {
            if (regId == id && pointer >= 0 && _pointerToSource.TryGetValue(pointer, out var source))
            {
                return source;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets the pointer for a given ID, or -1 if not found.
    /// </summary>
    public int GetPointer(int id)
    {
        foreach (var (regId, _, pointer) in _registrations)
        {
            if (regId == id)
            {
                return pointer;
            }
        }
        return -1;
    }

    /// <summary>
    /// Returns a conventional field name for a given delegate identifier.
    /// </summary>
    public static string GetFieldName(int id) => $"_lambda{id}";

    /// <summary>
    /// Enumerates all registered delegates and their identifiers.
    /// </summary>
    public IEnumerable<(int Id, Delegate Delegate)> Enumerate() =>
        _registrations.Select(static r => (r.Id, r.Delegate));

    /// <summary>
    /// Gets information about a delegate's method for source code matching.
    /// </summary>
    public static (string? TypeName, string MethodName, int MetadataToken) GetDelegateInfo(Delegate @delegate)
    {
        var method = @delegate.Method;
        return (method.DeclaringType?.FullName, method.Name, method.MetadataToken);
    }
}
