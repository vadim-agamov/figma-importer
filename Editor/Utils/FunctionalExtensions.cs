using System;

namespace FigmaImporter.Editor
{
    public static class FunctionalExtensions
    {
        public static TOut Map<TIn, TOut>(this TIn @this, Func<TIn, TOut> f) => f(@this);
    }
}