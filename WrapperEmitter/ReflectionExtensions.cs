namespace WrapperEmitter;

public static class ReflectionExtensions
{
    public static string FullTypeExpression(this Type type, bool leaveOpenGenericsOpen = false)
    {
        if (type == typeof(void))
        {
            return "void";
        }

        if (type.IsArray)
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
                    return $"{elementType.FullTypeExpression(leaveOpenGenericsOpen)}{rangeText}";
                }
            }
        }

        if (type.IsPointer)
        {
            var elementType = type.GetElementType()
                ?? throw UnexpectedReflectionsException.ArrayMissingElementType(type);
            return $"{elementType.FullTypeExpression(leaveOpenGenericsOpen)}*";
        }

        if (type.IsByRef)
        {
            var elementType = type.GetElementType()
                ?? throw UnexpectedReflectionsException.ArrayMissingElementType(type);
            return elementType.FullTypeExpression(leaveOpenGenericsOpen);
        }

        if (type.IsGenericParameter)
        {
            return leaveOpenGenericsOpen ? string.Empty : $"@{type.Name}";
        }

        var genericArgs = type.GetGenericArguments();
        var genericArgsIndex = 0;

        string result;
        if (string.IsNullOrEmpty(type.Namespace))
        {
            result = string.Empty;
        }
        else
        {
            result = "@" + type.Namespace.Replace(".", ".@") + ".";
        }

        List<Type> types = new() { type };
        var nestedLoop = type;
        while (nestedLoop.IsNested)
        {
            nestedLoop = nestedLoop.DeclaringType
                ?? throw UnexpectedReflectionsException.NestedTypeMissingDeclaringType(nestedLoop);
            types.Add(nestedLoop);
        }
        for (int i = types.Count - 1; i >=0; i--)
        {
            var parent = types[i];
            // Fun Facts....
            //   typeof(List<int[]>) .IsArray = false, .IsGenericType = true,  .GetGenericArguments().Length = 1
            //   typeof(List<int>[]) .IsArray = true,  .IsGenericType = false, .GetGenericArguments().Length = 1  🤔
            if (parent.IsGenericType)
            {
                var genericTypeDefinition = parent.GetGenericTypeDefinition();
                string genericTypeName = genericTypeDefinition.Name;
                genericTypeName = genericTypeName
                    .Substring(0, genericTypeName.IndexOf('`'));

                var parentLength = parent.GetGenericArguments().Length;
                var numberOfGenericArguments = parentLength - genericArgsIndex;

                string genericArgsText = string.Join(", ", genericArgs
                        .Skip(genericArgsIndex)
                        .Take(numberOfGenericArguments)
                        .Select(x => x.FullTypeExpression(leaveOpenGenericsOpen)));
                
                genericArgsIndex = parentLength;

                result +=  $"@{genericTypeName}<{genericArgsText}>";
            }
            else
            {
                result +=  $"@{parent.Name}";
            }
            if (i != 0)
            {
                result += ".";
            }
        }

        if (result.Contains('+'))
        {
            throw UnexpectedReflectionsException.PlusFoundInTypeName(type, result);
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
