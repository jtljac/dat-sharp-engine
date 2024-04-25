using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace dat_sharp_engine.Collection;

/// <summary>
/// A concurrent implementation for <see cref="IIdTrackedResource{T}"/> that is backed with a <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
/// <typeparam name="T">The type of the resource being tracked</typeparam>
public class ConcurrentIdTrackedResource<T> : IIdTrackedResource<T> {
    /// <summary>
    /// The map of resource Ids to resources
    /// </summary>
    private readonly ConcurrentDictionary<ulong, T> _resourceMap = new();
    /// <summary>
    /// The ID to use for the next stored resource
    /// <para/>
    /// This is stored this way
    /// </summary>
    private ulong _nextId = 1L;

    /// <inheritdoc cref="IIdTrackedResource{T}.Insert"/>
    public ulong Insert(T resource) {
        // Gotta do this dance to keep it concurrent
        var id = Interlocked.Increment(ref _nextId) - 1;
        _resourceMap.TryAdd(id, resource);
        return id;
    }

    /// <inheritdoc cref="IIdTrackedResource{T}.Get"/>
    public bool Get(ulong key, [NotNullWhen(true)] out T resource) {
        return _resourceMap.TryGetValue(key, out resource!);
    }

    /// <inheritdoc cref="IIdTrackedResource{T}.Remove(ulong)"/>
    public bool Remove(ulong id) {
        return _resourceMap.TryRemove(id, out _);
    }

    /// <inheritdoc cref="IIdTrackedResource{T}.Remove(ulong, out T)"/>
    public bool Remove(ulong id, out T? value) {
        return _resourceMap.TryRemove(id, out value);
    }

    /// <inheritdoc cref="IIdTrackedResource{T}.Clear"/>
    public void Clear() {
        // This could be a bug when clear is called in one thread and insert is called in another, but quite frankly
        // that logic sounds like a bigger bug
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