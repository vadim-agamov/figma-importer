using System;

namespace FigmaImporter.Editor
{
    public static class FunctionalExtensions
    {
        public static TOut Map<TIn, TOut>(this TIn @this, Func<TIn, TOut> f) => f(@this);
        public static T MapIf<T>(this T @this, Func<T, T> f, bool condition) => condition ? f(@this) : @this;
    }
}