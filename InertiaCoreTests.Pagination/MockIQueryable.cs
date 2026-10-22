using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InertiaCoreTests.Pagination
{
    public static class MockIQueryable
    {
        public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> query, Func<T, object> keySelector) where T : class
        {
            return query.OrderBy(keySelector);
        }
        
        public static IOrderedQueryable<T> OrderByDescending<T>(this IQueryable<T> query, Func<T, object> keySelector) where T : class
        {
            return query.OrderByDescending(keySelector);
        }
        
        public static IQueryable<T> AsAsyncEnumerable<T>(this IQueryable<T> query)
        {
            return query;
        }
    }
}
