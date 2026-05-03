using System;
using UnityEngine;

/// <summary>
/// Interface for accessing IsUsed property on any parameter type.
/// </summary>
public interface IMBParam
{
    bool IsUsed { get; set; }
}

/// <summary>
/// Wrapper for a module.ini parameter that tracks whether it was explicitly set.
/// </summary>
[Serializable]
public class MBModuleIniParameter<T> : IMBParam
{
    [SerializeField] private T _value;
    [SerializeField] private bool _isUsed;
    
    /// <summary>
    /// The parameter value.
    /// </summary>
    public T Value
    {
        get => _value;
        set
        {
            _value = value;
            _isUsed = true;
        }
    }
    
    /// <summary>
    /// Whether this parameter was explicitly set (from file or programmatically).
    /// </summary>
    public bool IsUsed
    {
        get => _isUsed;
        set => _isUsed = value;
    }
    
    /// <summary>
    /// Sets value without marking as used (for defaults).
    /// </summary>
    public void SetDefault(T defaultValue)
    {
        _value = defaultValue;
        _isUsed = false;
    }
    
    /// <summary>
    /// Sets value and marks as used (for imports).
    /// </summary>
    public void SetFromFile(T value)
    {
        _value = value;
        _isUsed = true;
    }
    
    /// <summary>
    /// Resets to default state.
    /// </summary>
    public void Reset(T defaultValue)
    {
        _value = defaultValue;
        _isUsed = false;
    }
    
    public MBModuleIniParameter()
    {
        _value = default;
        _isUsed = false;
    }
    
    public MBModuleIniParameter(T defaultValue)
    {
        _value = defaultValue;
        _isUsed = false;
    }
    
    // Implicit conversion to T for ease of use
    public static implicit operator T(MBModuleIniParameter<T> param) => param._value;
    
    public override string ToString() => _isUsed ? $"{_value} (set)" : $"{_value} (default)";
}

// Concrete implementations for Unity serialization (generic classes don't serialize well)
[Serializable]
public class MBParamString : MBModuleIniParameter<string>
{
    public MBParamString() : base("") { }
    public MBParamString(string defaultValue) : base(defaultValue) { }
}

[Serializable]
public class MBParamInt : MBModuleIniParameter<int>
{
    public MBParamInt() : base(0) { }
    public MBParamInt(int defaultValue) : base(defaultValue) { }
}

[Serializable]
public class MBParamFloat : MBModuleIniParameter<float>
{
    public MBParamFloat() : base(0f) { }
    public MBParamFloat(float defaultValue) : base(defaultValue) { }
}

[Serializable]
public class MBParamBool : MBModuleIniParameter<bool>
{
    public MBParamBool() : base(false) { }
    public MBParamBool(bool defaultValue) : base(defaultValue) { }
}