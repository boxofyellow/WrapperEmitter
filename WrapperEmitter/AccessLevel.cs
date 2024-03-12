using System.Reflection;

namespace WrapperEmitter;

public enum AccessLevel : int
{
    Protected = 0,
    Public = 1,
}

public static class AccessLevelExtensions
{
    public static string CodeText(this AccessLevel accessLevel) => accessLevel.ToString().ToLower();

    public static AccessLevel Max(params AccessLevel?[] levels)
        => levels.Where(x => x is not null).Select(x => x!.Value).Max();

    public static string? TextIfNotMax(this AccessLevel accessLevel, AccessLevel max)
        => accessLevel == max ? null : $"{accessLevel.CodeText()} "; 

    public static AccessLevel GetAccessLevel(this MethodInfo method)
    {
        if (method.IsFamily)
        {
            return AccessLevel.Protected;
        }
        if (method.IsPublic)
        {
            return AccessLevel.Public;
        }
        throw InvalidCSharpException.UnAccessibleMethod(method);
    }
}