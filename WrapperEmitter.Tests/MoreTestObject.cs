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
        public class @double<@float> { }
    }
}

namespace WrapperEmitter.Tests
{
    public static class MoreTestObject
    {
        public static readonly (Type Type, string Expression)[] Types = new [] {
            (typeof(@namespace.@return.@interface), "@namespace.@return.@interface"),
            (typeof(@namespace.@return.@class), "@namespace.@return.@class"),
            (typeof(@namespace.@return.@class.@int), "@namespace.@return.@class.@int"),
            (typeof(@namespace.@return.@void<@namespace.@return.@interface>), "@namespace.@return.@void<@namespace.@return.@interface>"),
            (typeof(@namespace.@return.@class[]), "@namespace.@return.@class[]"),
        };

        // TODO: It looks like Nested + Generic yields some funny stuff...
        /*
            Expected:<@namespace.@return.@void<@namespace.@return.@class>.@double<@namespace.@return.@class.@int>>
            Actual  :<@namespace.@return.@void<@namespace.@return.@class,@namespace.@return.@class.@int>>
        */
    }
}

#pragma warning restore IDE1006 // Naming Styles