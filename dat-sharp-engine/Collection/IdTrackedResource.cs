using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace dat_sharp_engine.Collection;

/// <summary>
/// An implementation for <see cref="IIdTrackedResource{T}"/> that is backed with a <see cref="Dictionary{TKey,TValue}"/>.
/// </summary>
/// <typeparam name="T">The type of the resource being tracked</typeparam>
public class IdTrackedResource<T> : IIdTrackedResource<T> {
    /// <summary>
    /// The map of resource Ids to resources
    /// </summary>
    private readonly Dictionary<ulong, T> _resourceMap = new();
    /// <summary>
    /// The ID to use for the next stored resource
    /// </summary>
    private ulong _nextId = 1L;

    /// <inheritdoc cref="IIdTrackedResource{T}.Insert"/>
    public ulong Insert(T resource) {
        _resourceMap.Add(_nextId, resource);
        return _nextId++;
    }

    /// <inheritdoc cref="IIdTrackedResource{T}.Get"/>
    public bool Get(ulong key, out T? resource) {
        return _resourceMap.TryGetValue(key, out resource);
    }

    /// <inheritdoc cref="IIdTrackedResource{T}.Remove(ulong)"/>
    public bool Remove(ulong id) {
        return _resourceMap.Remove(id);
    }

    /// <inheritdoc cref="IIdTrackedResource{T}.Remove(ulong, out T)"/>
    public bool Remove(ulong id, out T? value) {
        return _resourceMap.Remove(id, out value);
    }

    /// <inheritdoc cref="IIdTrackedResource{T}.Clear"/>
    public void Clear() {
        _nextId = 0;
        _resourceMap.Clear();
    }

    /// <summary>
    /// Get an enumerator for the values in the collection
    /// </summary>
    /// <returns>An Enumerator that iterates through the resources in the collection</returns>
    public IEnumerator<KeyValuePair<ulong, T>> GetEnumerator() {
        return _resourceMap.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}