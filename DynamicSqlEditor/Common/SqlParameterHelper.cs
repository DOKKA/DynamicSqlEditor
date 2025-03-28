using System;
using System.Data;
using System.Data.SqlClient;

namespace DynamicSqlEditor.Common
{
    public static class SqlParameterHelper
    {
        public static SqlParameter CreateParameter(string name, object value, SqlDbType? dbType = null)
        {
            var parameter = new SqlParameter(name, value ?? DBNull.Value);
            if (dbType.HasValue)
            {
                parameter.SqlDbType = dbType.Value;
            }
            return parameter;
        }

        public static SqlParameter CreateParameter(string name, object value, SqlDbType dbType, int size)
        {
            var parameter = new SqlParameter(name, dbType, size)
            {
                Value = value ?? DBNull.Value
            };
            return parameter;
        }

        public static SqlParameter CreateOutputParameter(string name, SqlDbType dbType, int size = -1)
        {
            var parameter = new SqlParameter(name, dbType)
            {
                Direction = ParameterDirection.Output
            };
            if (size > -1)
            {
                parameter.Size = size;
            }
            return parameter;
        }
    }
}