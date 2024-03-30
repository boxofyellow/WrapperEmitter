using System.Runtime.CompilerServices;

#pragma warning disable IDE1006 // Naming Styles
namespace @namespace.@return
{
    public interface @interface {}
    public class @class : @interface
    {
        public class @int { }
    }
    public class @void<@int>
    {
        public class @double<@float, @char> 
        {
            public class @bool<@long, @object> { }
        }
    }
}
#pragma warning restore IDE1006 // Naming Styles

namespace WrapperEmitter.Tests
{
    public static class MoreTestObject
    {
        public static readonly (Type Type, string Expression)[] Types = new [] {
            TypeText(typeof(@namespace.@return.@interface)),
            TypeText(typeof(@namespace.@return.@class)),
            TypeText(typeof(@namespace.@return.@class.@int)),
            TypeText(typeof(@namespace.@return.@void<@namespace.@return.@interface>)),
            TypeText(typeof(@namespace.@return.@class[])),
            TypeText(typeof(@namespace.@return.@void<@namespace.@return.@class>.@double<@namespace.@return.@class.@int, @namespace.@return.@class>.@bool<@namespace.@return.@interface, @namespace.@return.@class[]>)),
            TypeText(typeof(@namespace.@return.@void<@namespace.@return.@void<@namespace.@return.@interface>>)),
        };

        public static readonly (Type Type, string Expression)[] OpenTypes = new [] {
            TypeText(typeof(@namespace.@return.@void<>)),
            TypeText(typeof(@namespace.@return.@void<>.@double<, >)),
            TypeText(typeof(@namespace.@return.@void<>.@double<, >.@bool<, >)),
        };

        private static (Type Type, string Expression) TypeText(Type type, [CallerArgumentExpression("type")] string text = "")
        {
            int start = "typeof(".Length;
            string expression = text.Substring(start, text.Length - start - 1);
            return (type, expression);
        }
    }
}