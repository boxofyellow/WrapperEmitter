using System.Reflection;

namespace WrapperEmitter;

/// <summary>
/// This enum denotes the different access level of members that we can interact with
/// Note: that for the most part Interface Implementations cannot interact with the projected properties of the object they are delegating their work too 
/// </summary>
public enum AccessLevel : int
{
    Protected = 0,
    Public = 1,
}

public static class AccessLevelExtensions
{
    /// <summary>
    /// The string representation of that this access level should look like in code. 
    /// </summary>
    /// <param name="level">the level to format</param>
    /// <returns>Text that can be included in code as modifier of the member</returns>
    public static string CodeText(this AccessLevel level) => level.ToString().ToLower();

    /// <summary>
    /// Given multiple levels (including nullable ones), of all the non-null ones which is least restrictive
    /// </summary>
    /// <param name="levels"></param>
    /// <returns>the least restricted of the provided levels</returns>
    public static AccessLevel Max(params AccessLevel?[] levels)
        => levels.Where(x => x is not null).Select(x => x!.Value).Max();

    /// <summary>
    /// Often access modifiers should only be included in code if (and only if) they are more restricted for example
    /// public int X { get; protected set; }
    /// This helper makes it easy to meet that goal
    /// </summary>
    /// <param name="level">the level to format</param>
    /// <param name="max">the max level of the related properties</param>
    /// <returns>Text that can be included in code as modifier for a getter/setter or adder/remover</returns>
    public static string? TextIfNotMax(this AccessLevel level, AccessLevel max)
        => level == max ? null : $"{level.CodeText()} "; 

    /// <summary>
    /// A method for extracting the Access Level from a method
    /// </summary>
    /// <param name="method">the method to check</param>
    /// <returns>the method's level</returns>
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