namespace DatGenerator.Util;

/// <summary>
/// A record representing library metadata for caching
/// <para>
/// This, when used as an IncrementalValueProvider, can allow a quick cache reference for
/// generators that only act on library code once
/// </para>
/// </summary>
/// <param name="version">The version of the library, only used for cache comparison</param>
internal record LibraryMetadata(string version);

/// <summary>
/// A record holding the relevant types for Code generation
/// </summary>
/// <param name="types">A list of the types (classes, structs, records, etc) found inside a library</param>
internal record LibrarySchema(EquatableArray<TypeSchema> types);

/// <summary>
/// A record storing metadata about a single type, plus the relevant members inside that type
/// </summary>
/// <param name="name">The name of the type</param>
/// <param name="nameSpace">The display name of the namespace that holds the type</param>
/// <param name="members">The members inside the type</param>
internal record TypeSchema(string name, string nameSpace, EquatableArray<MemberSchema> members);

/// <summary>
/// A record storing metadata about a single type member
/// </summary>
/// <param name="name">The name of the member</param>
/// <param name="type">The type of the member</param>
internal record MemberSchema(string name, string type);