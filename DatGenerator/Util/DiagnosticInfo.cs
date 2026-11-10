// Code Here was made with the help of
// https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/#7-consider-using-registerimplementationsourceoutput-instead-of-registersourceoutput
// It is used and modified under the MIT Licence:
// The MIT License (MIT)
//
// Copyright (c) 2019 andrewlock
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace DatGenerator.Util;

/// <summary>
/// A record pairing an arbitrary value with a list of failures that need reporting
/// <para>
/// This allows building your data model inside the generator, whilst still being able to report any errors or warnings
/// discovered during parsing
/// </para>
/// </summary>
/// <param name="value">The data model</param>
/// <param name="diagnostics">A list of issues to report</param>
/// <typeparam name="T">The type of the actual data</typeparam>
internal sealed record Result<T>(T value, EquatableArray<DiagnosticInfo> diagnostics) where T : IEquatable<T>? {
    public Result(T value, IEnumerable<DiagnosticInfo> diagnosticInfos) : this(value, new EquatableArray<DiagnosticInfo>(
        [.. diagnosticInfos]
    )) {}
}

/// <summary>
/// A record representing a diagnostic encountered during transformation
/// <para>
/// This allows caching by separating the relevant information from the ephemeral context
/// </para>
/// </summary>
/// <param name="descriptor">The diagnostic message</param>
/// <param name="location">The location of the issue</param>
internal sealed record DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location) {
    public DiagnosticInfo(DiagnosticDescriptor descriptor, Location? location)
        : this(descriptor, LocationInfo.Create(location)) {}

    /// <summary>
    /// For laziness, implicitly convert into a diagnostic
    /// </summary>
    public static implicit operator Diagnostic(DiagnosticInfo info) {
        return Diagnostic.Create(info.descriptor, info.location);
    }
}

/// <summary>
/// A cacheable representation of a diagnostic location target
/// </summary>
/// <param name="filePath">The path of the source document</param>
/// <param name="textSpan">The location within the syntax tree this location is associated with</param>
/// <param name="lineSpan">The line and position information</param>
internal sealed record LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan) {
    public static LocationInfo? Create(Location? location) {
        return location?.SourceTree is null
            ?  null
            : new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
    }

    /// <summary>
    /// For Laziness, implicitly convert into a Location
    /// </summary>
    public static implicit operator Location?(LocationInfo? locationInfo) => locationInfo == null
        ? null
        : Location.Create(locationInfo.filePath, locationInfo.textSpan, locationInfo.lineSpan);
}