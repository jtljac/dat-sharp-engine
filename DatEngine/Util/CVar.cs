using System.Collections.Concurrent;

namespace DatEngine.Util;

/// <summary>
/// Attribute for autoregistering CVars at compile time
/// </summary>
/// <param name="name">The name of the CVar for lookups</param>
/// <param name="description">A brief description of the CVar</param>
/// <param name="flags">Flags that set the behaviour of the CVar</param>
/// <seealso cref="CVar{T}"/>
[AttributeUsage(AttributeTargets.Field)]
public class CVarAttribute(string name, string description = "", CVarFlags flags = CVarFlags.None)
    : Attribute {
    /// <summary>
    /// The name of the CVar for lookup and use in the console
    /// </summary>
    public readonly string name = name;
    /// <summary>
    /// A brief description of the CVar
    /// </summary>
    public readonly string description = description;
    /// <summary>
    /// Flags that change the behaviour of the CVar
    /// </summary>
    public readonly CVarFlags flags = flags;
}

/// <summary>
/// Flags that set the behaviour of the CVars
/// </summary>
[Flags]
public enum CVarFlags {
    None = 0,
    /// <summary>
    /// Prevents the value of the CVar from being changed
    /// </summary>
    ReadOnly = 1,
    /// <summary>
    /// Marks a CVar as intended for use in debugging
    /// </summary>
    Debug =  1 << 1,
    /// <summary>
    /// Prevents the CVar from appearing in the console
    /// </summary>
    Hidden = 1 << 2,
    /// <summary>
    ///  Saves the CVar the disk after setting it, and loads it back after restarting
    /// </summary>
    Persistent = 1 << 3,
    /// <summary>
    /// Mark the CVar as requiring a reboot before any changes are reflected
    /// </summary>
    RequiresRestart = 1 << 4 | Persistent,
}

/// <summary>
/// Interface for generic CVars
/// </summary>
/// <seealso cref="CVar{T}"/>
/// <seealso cref="CVarAttribute"/>
public interface ICVar {
    /// <summary>
    /// The name of the CVar for lookup and use in the console
    /// </summary>
    string name { get; }

    /// <summary>
    /// A brief description of the CVar
    /// </summary>
    string description { get; }
    /// <summary>
    /// Flags that change the behaviour of the CVar
    /// </summary>
    CVarFlags flags { get; }
    /// <summary>
    /// Get the value of the CVar as a string for serialisation and for the console
    /// </summary>
    /// <returns>The Value of the CVar as a string</returns>
    string GetStringValue();
    /// <summary>
    /// Set the value of the CVar from a string
    /// <para>
    /// This will fail if the <see cref="CVarFlags.ReadOnly"/> flag is set
    /// </para>
    /// </summary>
    /// <param name="value">A string representing the new value</param>
    /// <returns><c>true</c> if successful</returns>
    bool SetStringValue(string value);

    /// <summary>
    /// Reset the CVar back to its initial value
    /// <para>
    /// This will fail if the <see cref="CVarFlags.ReadOnly"/> flag is set
    /// </para>
    /// </summary>
    /// <returns><c>true</c> if successful</returns>
    bool Reset();
}

/// <summary>
/// Definition for a CVar, use with <see cref="CVarAttribute"/> to register with the CVar Manager.
/// <example>
/// For example:
/// <code>
/// [CVar("iMaxWindowWidth", "The maximum width of the window, ")]
/// CVar&lt;int&gt; maxWidth = new(1920);
/// </code>
/// </example>
/// </summary>
/// <seealso cref="CVarAttribute" />
/// <typeparam name="T">The Type the CVar is storing</typeparam>
public class CVar<T>: ICVar where T: IComparable<T>, IFormattable {
    /// <inheritdoc/>
    public string name { get; }
    /// <inheritdoc/>
    public string description { get; }
    /// <inheritdoc/>
    public CVarFlags flags { get; }

    /// <summary>
    /// The value stored inside the CVar
    /// </summary>
    /// <seealso cref="_defaultValue"/>
    public T value {
        get;
        set {
            if (value.CompareTo(field) == 0) return;
            field = value;
            OnChange?.Invoke(this, value);
        }
    }

    /// <summary>
    /// The original value set during the definition of theCVar
    /// </summary>
    /// <seealso cref="value"/>
    private readonly T _defaultValue;

    /// <summary>
    /// Event fired on changing the <see cref="value"/>
    /// </summary>
    public event EventHandler<T>? OnChange;

    public CVar(T value) {
        this.value = value;
        _defaultValue = value;
        description = "";
        flags = CVarFlags.None;
    }

    public CVar(T value, string name, string description, CVarFlags flags) {
        this.value = value;
        this.name = name;
        _defaultValue = value;
        this.description = description;
        this.flags = flags;
    }

    /// <inheritdoc/>
    public string GetStringValue() {
        return value.ToString() ?? "";
    }
    /// <inheritdoc/>
    public bool SetStringValue(string newValueString) {
        if (flags.HasFlag(CVarFlags.ReadOnly)) return false;

        try {
            value = (T) Convert.ChangeType(newValueString, typeof(T));
            return true;
        } catch (InvalidCastException) {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool Reset() {
        if (flags.HasFlag(CVarFlags.ReadOnly)) {
            return false;
        }

        value = _defaultValue;
        return true;
    }

    public static implicit operator T(CVar<T> cVar) {
        return cVar.value;
    }
}

public class CVarManager {
    private ConcurrentDictionary<string, ICVar> _cVars = [];

    private CVarManager() {
        Logger.EngineLogger.Trace("Initializing CVars");
        CVarAutoInitialiser.InitialiseAll(this);

        // Load persistent from disk
    }

    public CVar<T> Register<T>(string name, T defaultValue, string description, CVarFlags flags)
        where T : IComparable<T>, IFormattable {
        var cVar = new CVar<T>(defaultValue, name, description, flags);

        return !_cVars.TryAdd(name, cVar)
            ? throw new ArgumentException($"Duplicate CVar name: {name}")
            : cVar;
    }
}