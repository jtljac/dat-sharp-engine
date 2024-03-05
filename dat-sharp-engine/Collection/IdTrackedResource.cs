using System.Collections;
using System.Runtime.InteropServices;

namespace dat_sharp_engine.Collection;

/// <summary>
/// A Structure for tracking resources using an ID
/// </summary>
/// <typeparam name="T">The type of the resource being tracked</typeparam>
public class IdTrackedResource<T> : IEnumerable<KeyValuePair<ulong, T>> {
    private ulong _nextKey = 1L;
    private readonly Dictionary<ulong, T> _resourceMap = new();

    public ulong Insert(T resource) {
        _resourceMap[_nextKey] = resource;
        return _nextKey++;
    }

    public ref T Get(ulong key) {
        return ref CollectionsMarshal.GetValueRefOrNullRef(_resourceMap, key);
    }

    public void Remove(ulong key) {
        _resourceMap.Remove(key);
    }

    public void Clear() {
        _nextKey = 1;
        _resourceMap.Clear();
    }

    public IEnumerator<KeyValuePair<ulong, T>> GetEnumerator() {
        return _resourceMap.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}