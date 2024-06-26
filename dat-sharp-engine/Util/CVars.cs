using System.Data;

namespace dat_sharp_engine.Util;

/// <summary>
/// A system for simple global variables. CVars provide well defined and globally accessible values of specific types
/// that allow for easy configuration and use
/// <para/>
/// CVars can be created by defining a static <see cref="CVar{T}"/> variable of your chosen type. This will automatically
/// register your CVar and make it available to access via <see cref="GetCVar{T}"/> (Or a more efficient specific
/// accessor method).
/// </summary>
/// <example>
/// This shows how to define a new Bool CVar
/// <code>
/// private static readonly CVar&lt;bool&gt; boolCVar = new("Bool Name", "Description for CVar", true, CVarCategory.Misc, CVarFlags.None);
/// </code>
/// </example>
/// <seealso cref="CVar{T}"/>
public sealed class CVars {
    /// <summary>Storage for String CVars</summary>
    private readonly Dictionary<string, CVar<string>> _stringCVars = new();
    /// <summary>Storage for Integer CVars</summary>
    private readonly Dictionary<string, CVar<int>> _intCVars = new();
    /// <summary>Storage for Unsigned Integer CVars</summary>
    private readonly Dictionary<string, CVar<uint>> _uintCVars = new();
    /// <summary>Storage for Float CVars</summary>
    private readonly Dictionary<string, CVar<float>> _floatCVars = new();
    /// <summary>Storage for Boolean CVars</summary>
    private readonly Dictionary<string, CVar<bool>> _boolCVars = new();
    /// <summary>
    /// Storage for Object CVars
    /// <para/>
    /// This is a catch all for any unrecognised type, behaviour is not guaranteed!
    /// </summary>
    private readonly Dictionary<string, object> _objectCVars = new();

    /// <summary>Singleton instance</summary>
    public static CVars instance { get; } = new();

    public void Initialise() {
        // TODO: Load in CVars from files, update registered cvars, create map of remaining ones to apply to newly registered CVars
    }

    private CVars() {}

    /// <summary>
    /// Get a CVar of the parameter type
    /// <para/>
    /// It should be preferred to use a specialised function, as this generic function has to work out which cvar
    /// storage to use at runtime
    /// </summary>
    /// <param name="cVarName">The name of the CVar being fetched</param>
    /// <typeparam name="T">The type of the CVar Value</typeparam>
    /// <returns>The CVar with the given type and name, or null if it doesn't exist</returns>
    /// <seealso cref="GetStringCVar"/>
    /// <seealso cref="GetIntCVar"/>
    /// <seealso cref="GetUintCVar"/>
    /// <seealso cref="GetFloatCVar"/>
    /// <seealso cref="GetBoolCVar"/>
    /// <seealso cref="GetObjectCVar{T}"/>
    internal CVar<T>? GetCVar<T>(string cVarName) {
        return Type.GetTypeCode(typeof(T)) switch {
            TypeCode.String => GetStringCVar(cVarName) as CVar<T>,
            TypeCode.Int32 => GetIntCVar(cVarName) as CVar<T>,
            TypeCode.UInt32 => GetUintCVar(cVarName) as CVar<T>,
            TypeCode.Decimal => GetFloatCVar(cVarName) as CVar<T>,
            TypeCode.Boolean => GetBoolCVar(cVarName) as CVar<T>,
            _ => GetObjectCVar<T>(cVarName)
        };
    }

    /// <summary>
    /// Register a new CVar
    /// <para/>
    /// This method should be automatically called when defining a CVar, do not call it manually
    /// </summary>
    /// <param name="cVar">The CVar to register</param>
    /// <typeparam name="T">The type of CVar being registered</typeparam>
    internal void RegisterCVar<T>(CVar<T> cVar) {
        switch (cVar) {
            case CVar<string> stringCVar:
                RegisterStringCVar(stringCVar);
                break;
            case CVar<int> intCVar:
                RegisterIntCVar(intCVar);
                break;
            case CVar<uint> uintCVar:
                RegisterUintCVar(uintCVar);
                break;
            case CVar<float> floatCVar:
                RegisterFloatCVar(floatCVar);
                break;
            case CVar<bool> boolCVar:
                RegisterBoolCVar(boolCVar);
                break;
            default:
                RegisterObjectCVar(cVar);
                break;
        }
    }

    /// <summary>
    /// Register a new String CVar
    /// <para/>
    /// This method should be automatically called when defining a CVar, do not call it manually
    /// </summary>
    /// <param name="cVar">The CVar to register</param>
    private void RegisterStringCVar(CVar<string> cVar) {
        _stringCVars[cVar.name] = cVar;
    }

    /// <summary>
    /// Get a String CVar
    /// </summary>
    /// <param name="cVarName">The name of the CVar to get</param>
    /// <returns>The String CVar, or null if it doesn't exist</returns>
    public CVar<string>? GetStringCVar(string cVarName) {
        return _stringCVars.TryGetValue(cVarName, out var value) ? value : null;
    }

    /// <summary>
    /// Register a new Int CVar
    /// <para/>
    /// This method should be automatically called when defining a CVar, do not call it manually
    /// </summary>
    /// <param name="cVar">The CVar to register</param>
    private void RegisterIntCVar(CVar<int> cVar) {
        _intCVars[cVar.name] = cVar;
    }

    /// <summary>
    /// Get a Int CVar
    /// </summary>
    /// <param name="cVarName">The name of the CVar to get</param>
    /// <returns>The Int CVar, or null if it doesn't exist</returns>
    public CVar<int>? GetIntCVar(string cVarName) {
        return _intCVars.TryGetValue(cVarName, out var value) ? value : null;
    }

    /// <summary>
    /// Register a new UInt CVar
    /// <para/>
    /// This method should be automatically called when defining a CVar, do not call it manually
    /// </summary>
    /// <param name="cVar">The CVar to register</param>
    private void RegisterUintCVar(CVar<uint> cVar) {
        _uintCVars[cVar.name] = cVar;
    }

    /// <summary>
    /// Get a UInt CVar
    /// </summary>
    /// <param name="cVarName">The name of the CVar to get</param>
    /// <returns>The UInt CVar, or null if it doesn't exist</returns>
    public CVar<uint>? GetUintCVar(string cVarName) {
        return _uintCVars.TryGetValue(cVarName, out var value) ? value : null;
    }

    /// <summary>
    /// Register a new Float CVar
    /// <para/>
    /// This method should be automatically called when defining a CVar, do not call it manually
    /// </summary>
    /// <param name="cVar">The CVar to register</param>
    private void RegisterFloatCVar(CVar<float> cVar) {
        _floatCVars[cVar.name] = cVar;
    }

    /// <summary>
    /// Get a Float CVar
    /// </summary>
    /// <param name="cVarName">The name of the CVar to get</param>
    /// <returns>The Float CVar, or null if it doesn't exist</returns>
    public CVar<float>? GetFloatCVar(string cVarName) {
        return _floatCVars.TryGetValue(cVarName, out var value) ? value : null;
    }

    /// <summary>
    /// Register a new Boolean CVar
    /// <para/>
    /// This method should be automatically called when defining a CVar, do not call it manually
    /// </summary>
    /// <param name="cVar">The CVar to register</param>
    private void RegisterBoolCVar(CVar<bool> cVar) {
        _boolCVars[cVar.name] = cVar;
    }

    /// <summary>
    /// Get a Boolean CVar
    /// </summary>
    /// <param name="cVarName">The name of the CVar to get</param>
    /// <returns>The Boolean CVar, or null if it doesn't exist</returns>
    public CVar<bool>? GetBoolCVar(string cVarName) {
        return _boolCVars.TryGetValue(cVarName, out var value) ? value : null;
    }

    /// <summary>
    /// Register a new Object CVar
    /// <para/>
    /// This method should be automatically called when defining a CVar, do not call it manually
    /// </summary>
    /// <param name="cVar">The CVar to register</param>
    private void RegisterObjectCVar<T>(CVar<T> cVar) {
        _objectCVars[cVar.name] = cVar;
    }

    /// <summary>
    /// Get an Object CVar
    /// <para/>
    /// This is a catch all for any unrecognised type, behaviour is not guaranteed!
    /// </summary>
    /// <param name="cVarName">The name of the CVar to get</param>
    /// <returns>The Object CVar, or null if it doesn't exist</returns>
    public CVar<T>? GetObjectCVar<T>(string cVarName) {
        return _objectCVars.TryGetValue(cVarName, out var value) ? value as CVar<T> : null;
    }

    /// <summary>
    /// Save Dirty CVars to disk
    /// </summary>
    public void Save() {
        // TODO: Save
    }
}

/// <summary>
/// A CVar object
/// </summary>
/// <typeparam name="T">The type of the CVar</typeparam>
public class CVar<T> {
    /// <summary>The initial value of the CVar, used for resetting</summary>
    public T initial { get; }

    /// <summary>The actual value of the CVar</summary>
    private T _value;

    /// <summary>The value of the CVar</summary>
    /// <exception cref="ReadOnlyException">Thrown when trying to set the value of a ReadOnly CVar</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when trying to set the value and the new value is denied and not corrected by the
    /// <see cref="CVarValidator{T}"/>
    /// </exception>
    public T value {
        get => _value;
        set {
            if ((flags & CVarFlags.ReadOnly) != 0) throw new ReadOnlyException();
            _value = _validator != null ? _validator(value) : value;
            OnChangeEvent?.Invoke(null, this);
        }
    }

    /// <summary>The name of the CVar</summary>
    public string name { get; }
    /// <summary>A friendly description for the CVar</summary>
    public string description { get; }

    /// <summary>The Category the CVar belongs to, used for sorting when storing</summary>
    public CVarCategory category { get; }
    /// <summary>The CVar Flags</summary>
    public CVarFlags flags { get; }

    /// <summary>A method used to validate values assigned to the CVar</summary>
    private readonly CVarValidator<T>? _validator;

    /// <summary>An event fired when successfully changing the CVar value</summary>
    public event EventHandler<CVar<T>>? OnChangeEvent;

    /// <param name="name">The name of the CVar</param>
    /// <param name="description">A human readable description of the CVar</param>
    /// <param name="value">The initial value of the CVar</param>
    /// <param name="category">The category of the CVar</param>
    /// <param name="flags">Flags that apply to the CVar</param>
    /// <param name="validator">
    ///     An optional validator that can either modify a value before it is applied, or deny it
    /// </param>
    /// <param name="onChangeHandler">A method that is called when the CVar is updated</param>
    public CVar(string name, string description, T value, CVarCategory category, CVarFlags flags, CVarValidator<T>? validator = null, EventHandler<CVar<T>>? onChangeHandler = null) {
        initial = value;
        _value = value;
        this.name = name;
        this.description = description;
        this.flags = flags;
        this.category = category;
        _validator = validator;

        if (onChangeHandler != null) OnChangeEvent += onChangeHandler;

        CVars.instance.RegisterCVar(this);
    }
}

/// <summary>
/// Flags that define the behaviour of a CVar
/// </summary>
[Flags]
public enum CVarFlags {
    None = 0b00000000,
    /// <summary>Enforces that the CVar cannot change value</summary>
    ReadOnly = 0b00000001,
    /// <summary>The CVar is for debug purposes</summary>
    Debug = 0b00000010,
    /// <summary>The CVar should be replicated from server to client</summary>
    Replicated = 0b00000100,
    /// <summary>The CVar can be written to disk on change with a call to <see cref="CVar{T}.Save()"/></summary>
    Persistent = 0b00001000,
    /// <summary>Changes to the CVar will not be reflected until after a restart, force enables <see cref="Persistent"/> flag</summary>
    RequiresRestart = 0b00011000
}

/// <summary>
/// Categories for sorting CVars
/// </summary>
public enum CVarCategory {
    /// <summary>CVars important to the core functioning of the engine</summary>
    Core,
    /// <summary>CVars for the Graphics/Window System</summary>
    Graphics,
    /// <summary>CVars for the Input System</summary>
    Input,
    /// <summary>CVars for the Networking System</summary>
    Networking,
    /// <summary>CVars that don't fit into any other category</summary>
    Misc,
}

/// <summary>
/// A validation method that checks the value being applied to the CVar is valid.
/// <para/>
/// When the value is invalid, the validator can choose to correct the value by returning a new value, or to fail by
/// throwing an <see cref="ArgumentException"/>.
/// </summary>
/// <typeparam name="T">The type of the CVar</typeparam>
/// <exception cref="ArgumentException">Thrown when the <paramref name="newValue"/> is invalid</exception>
public delegate T CVarValidator<T>(T newValue);