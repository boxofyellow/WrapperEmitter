namespace WrapperEmitter;

public static class ReflectionExtensions
{
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
            genericTypeName = genericTypeName
                .Substring(0, genericTypeName.IndexOf('`'))
                .Replace(".", ".@")
                .Replace("+", ".@");

            string genericArgs = string.Join(",", type.GetGenericArguments()
                    .Select(x => x.FullTypeExpression()).ToArray());

            result = $"@{genericTypeName}<{genericArgs}>";
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
        else if (type.IsPointer)
        {
            var elementType = type.GetElementType()
                ?? throw UnexpectedReflectionsException.ArrayMissingElementType(type);
            result = $"{elementType.FullTypeExpression()}*";
        }
        else
        {
            result = '@' + (type.FullName ?? type.Name)
                .Replace(".", ".@")
                .Replace("+", ".@");;
        }

        if (result.Contains('+'))
        {
            throw UnexpectedReflectionsException.PlusFoundInTypeName(type, result);
        }

        if (type.IsByRef)
        {
            // TODO: Should we use GetElementType here?
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

    public static bool IsGenericTypeOf(this Type type, Type openGenericType) 
        => type.IsGenericType && (type.GetGenericTypeDefinition() == openGenericType);

    public static bool ContainsPointer(this Type type)
    {
        Type? t = type;
        while (t is not null)
        {
            if (t.IsPointer)
            {
                return true;
            }
            // Pointers types can be used as generics, so this should be the only place we need to look
            t = t.GetElementType();
        }
        return false;
    }
}
