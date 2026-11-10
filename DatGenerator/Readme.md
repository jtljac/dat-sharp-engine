This module holds the Roslyn code for automatically generating code.

## [CVarGenerator](CVarGenerator.cs)
This file handles automatic generation and registration of CVars, you can add an annotation to any public static
instantiation of a CVar<T> and it will wire it into the registry.

## [BrutalNumericsJsonGenerator](BrutalNumericsJsonGenerator.cs)
This file Generates a [JSONConverter](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/converters-how-to)
for each complete class in [Brutal Numerics library](https://www.nuget.org/packages/Ahwoo.BRUTAL.Numerics). 

This takes advantage of the fact that all the serializable fields are marked with `[XMLAttribute]`.