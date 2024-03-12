namespace WrapperEmitter;

public static class ReflectionExtensions
{
    // TODO: Pull the test over form azure dev-ops
    // TODO: What about Types with reserved words in their name... is that a thing?
    public static string FullTypeExpression(this Type type)
    {
        if (type == typeof(void))
        {
            return "void";
        }

        string result;
        // Fun Facts....
        //   typeof(List<int[]>) .IsArray = false, .IsGenericType = true,  .GetGenericArguments().Length = 1
        //   typeof(List<int>[]) .IsArray = true,  .IsGenericType = false, .GetGenericArguments().Length = 1  🤔
        if (type.IsGenericType)
        {
            Type genericTypeDefinition = type.GetGenericTypeDefinition();
            string genericTypeName = genericTypeDefinition.FullName
                ?? throw UnexpectedReflectionsException.MissingFullName(genericTypeDefinition);
            genericTypeName = genericTypeName.Substring(0, genericTypeName.IndexOf('`'));

            string genericArgs = string.Join(",", type.GetGenericArguments()
                    .Select(x => x.FullTypeExpression()).ToArray());

            result = $"{genericTypeName}<{genericArgs}>";
        }
        else if (type.IsArray)
        {
            // Funny story... typeof(int[][,]).ToString() => "System.Int32[,][]"
            // The same thing happens if you follow the "GetElementType recursive chain"
            var rangeText = string.Empty;
            var arrayType = type;
            while (true)
            {
                var elementType = arrayType.GetElementType()
                  ?? throw UnexpectedReflectionsException.ArrayMissingElementType(arrayType);

                rangeText += $"[{string.Join(",", new string[arrayType.GetArrayRank()])}]";

                if (elementType.IsArray)
                {
                    arrayType = elementType;
                }
                else
                {
                    result = $"{elementType.FullTypeExpression()}{rangeText}";
                    break;
                }
            }
        }
        else
        {
            // TODO: falling back to type.Name here allows use deal with open generics, but it also changes typeof(List<>) => "List<T>"
            result = type.FullName ?? type.Name;
        }

        // address nested types
        if (type.IsNested)
        {
            // TODO: Add Test for nested within a nested class
            result = result.Replace('+', '.');
        }
        else if (result.Contains('+'))
        {
            throw UnexpectedReflectionsException.PlusFoundInTypeName(type, result);
        }

        // TODO: try some type.IsByRefLike https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct
        // TODO: Track down if these can be "inside" a type (aka come up in the recursive calls) if so that may cause some problems
        if (type.IsByRef)
        {
            if (result.EndsWith('&'))
            {
                result = result.Remove(result.Length - 1);
            }
            else
            {
                throw UnexpectedReflectionsException.AmpersandNotFoundAtEndOfTypeName(type, result);
            }
        }
        if (result.Contains('&'))
        {
            throw UnexpectedReflectionsException.AmpersandFoundInTypeName(type, result);
        }

        return result;
    }
}
