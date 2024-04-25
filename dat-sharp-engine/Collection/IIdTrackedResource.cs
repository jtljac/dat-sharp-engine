using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace dat_sharp_engine.Collection;

/// <summary>
/// A Structure for tracking resources using an ID
/// </summary>
/// <typeparam name="T">The type of the resource being tracked</typeparam>
public interface IIdTrackedResource<T> : IEnumerable<KeyValuePair<ulong, T>> {
    /// <summary>
    /// Insert a resource into the collection
    /// </summary>
    /// <param name="resource">The resource to insert into the collection</param>
    /// <returns>The ID of the resource in the collection</returns>
    public ulong Insert(T resource);

    /// <summary>
    /// Retrieve a resource from the collection using its ID
    /// </summary>
    /// <param name="id">The ID of the resource to retrieve</param>
    /// <param name="resource">On return, contains the resource with the given ID, or null if there isn't one</param>
    /// <returns>True if there is a resource with the given Id</returns>
    public bool Get(ulong id, out T? resource);

    /// <summary>
    /// Remove a resource from the collection
    /// <para/>
    /// If there isn't a resource with the Id, this method will silently fail
    /// </summary>
    /// <param name="id">The ID of the resource to remove</param>
    /// <returns>True if the resource was successfully removed</returns>
    public bool Remove(ulong id);

    /// <summary>
    /// Remove a resource from the collection
    /// </summary>
    /// <param name="id">The ID of the resource to remove</param>
    /// <param name="value">
    ///     The resource that was removed from the collection, or null if there isn't a resource in the
    ///     collection with the <paramref name="id"/>
    /// </param>
    /// <returns>True if the resource was successfully removed</returns>
    public bool Remove(ulong id, out T? value);

    /// <summary>
    /// Empty the collection
    /// </summary>
    public void Clear();
}