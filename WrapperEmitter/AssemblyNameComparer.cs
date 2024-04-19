using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace WrapperEmitter;

/// <summary>
/// See https://github.com/dotnet/runtime/blob/dc553fe5e0b931f4d6fd481a0ba270bde644a19d/src/libraries/System.Private.CoreLib/src/System/Reflection/AssemblyName.cs#L294-L299
/// TL;DR - we need to manage our own Equality
/// https://www.dotnetframework.org/default.aspx/4@0/4@0/untmp/DEVDIV_TFS/Dev10/Releases/RTMRel/ndp/cdf/src/NetFx40/System@Activities/Microsoft/VisualBasic/Activities/AssemblyNameEqualityComparer@cs/1305376/AssemblyNameEqualityComparer@cs
/// </summary>
public class AssemblyNameComparer : IEqualityComparer<AssemblyName>
{
    public readonly static AssemblyNameComparer Instance = new();

    public bool Equals(AssemblyName? x, AssemblyName? y) 
        => ReferenceEquals(x, y)
        || ((x is not null)
            && (y is not null)
            && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
            && x.Version == y.Version
            && x.CultureName == y.CultureName);

    public int GetHashCode([DisallowNull] AssemblyName obj)
    {
        HashCode hash = new();
        hash.Add((obj.Name ?? string.Empty).ToUpper());
        hash.Add(obj.Version);
        hash.Add(obj.CultureName);
        return hash.ToHashCode();
    }
}