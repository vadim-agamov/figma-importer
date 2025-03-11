using System;
using System.Collections.Generic;
using System.Linq;

namespace FigmaImporter.Editor
{
    public static class CollectionExtensions
    {
        public static IEnumerable<List<T>> SplitIntoBatches<T>(this IReadOnlyList<T> source, int batchSize)
        {
            if (batchSize <= 0)
            {
                throw new ArgumentException("Batch size must be greater than 0.", nameof(batchSize));
            }

            for (var i = 0; i < source.Count; i += batchSize)
            {
                yield return new List<T>(source.Skip(i).Take(batchSize));
            }
        }
    }
}