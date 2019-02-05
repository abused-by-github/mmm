using Dapper;
using Dapper.Contrib.Extensions;
using System;
using System.Data;
using System.Linq;

namespace Mmm.Helpers
{
    public static class DbHelpers
    {
        public static long NextID<T>(this IDbConnection connection)
        {
            var keyProp = typeof(T).GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(ExplicitKeyAttribute))).Single();
            var maxID = connection.Query<long?>($"SELECT MAX({keyProp.Name}) FROM {typeof(T).Name}").SingleOrDefault() ?? 0;
            return maxID + 1;
        }
    }
}
