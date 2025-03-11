using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FigmaImporter.Editor
{
    public static class UniTaskExtensions
    {
        public static async UniTask<T[]> WhenAll<T>(this IReadOnlyList<UniTask<T>> tasks, IProgress<float> progress)
        {
            var total = tasks.Count;
            var pending = total;
            var results = new T[total];
            var exceptions = new List<Exception>();

            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                var index = i;
                task.OnComplete(
                        result =>
                        {
                            results[index] = result;
                            pending--;
                            progress.Report((total - pending) / (float)total);
                        },
                        exception =>
                        {
                            pending--;
                            exceptions.Add(exception);
                        })
                    .Forget();
            }
            
            await UniTask.WaitUntil(() => pending == 0 || exceptions.Count > 0);
            
            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }

            return results;
        }

        private static async UniTaskVoid OnComplete<T>(this UniTask<T> task, Action<T> completeHandler, Action<Exception> exceptionHandler)
        {
            try
            {
                completeHandler.Invoke(await task);
            }
            catch (Exception e)
            {
                exceptionHandler.Invoke(e);
            }
        }
    }
}