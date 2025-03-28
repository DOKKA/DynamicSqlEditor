using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.DataAccess;
using DynamicSqlEditor.Schema.Models;

namespace DynamicSqlEditor.Schema
{
    public class SchemaProvider
    {
        private readonly DatabaseManager _dbManager;

        public SchemaProvider(DatabaseManager dbManager)
        {
            _dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
        }

        public async Task<List<TableSchema>> GetAllTablesAsync() // Renamed to indicate async
        {
            var tables = await GetTablesAndViewsAsync(); // Call async version
            if (!tables.Any()) return tables;

            // Fetch schema components concurrently if possible, or sequentially with await
            var columnsTask = GetColumnsAsync(tables);
            var primaryKeysTask = GetPrimaryKeysAsync(tables);
            var foreignKeysTask = GetForeignKeysAsync(tables);

            // Await all tasks
            var columns = await columnsTask;
            var primaryKeys = await primaryKeysTask;
            var foreignKeys = await foreignKeysTask;

            // (Rest of the method remains the same logic, just uses awaited results)
            foreach (var table in tables)
            {
                table.Columns.AddRange(columns.Where(c => c.ParentTable == table));
                table.PrimaryKeys.AddRange(primaryKeys.Where(pk => pk.ParentTable == table));
                table.ForeignKeys.AddRange(foreignKeys.Where(fk => fk.ReferencingTable == table));
                table.ReferencedByForeignKeys.AddRange(foreignKeys.Where(fk => fk.ReferencedTable == table));
            }

            return tables;
        }

        private async Task<List<TableSchema>> GetTablesAndViewsAsync()
        {
            var tables = new List<TableSchema>();
            string sql = @"SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES
                   WHERE TABLE_TYPE IN ('BASE TABLE', 'VIEW') ORDER BY TABLE_SCHEMA, TABLE_NAME;";
            try
            {
                // Use await instead of .Result
                DataTable dt = await _dbManager.ExecuteQueryAsync(sql, null);
                foreach (DataRow row in dt.Rows)
                {
                    tables.Add(new TableSchema
                    {
                        SchemaName = row["TABLE_SCHEMA"].ToString(),
                        TableName = row["TABLE_NAME"].ToString(),
                        IsView = row["TABLE_TYPE"].ToString().Equals("VIEW", StringComparison.OrdinalIgnoreCase)
                    });
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to retrieve tables and views.", ex);
                throw;
            }
            return tables;
        }

        private async Task<List<ColumnSchema>> GetColumnsAsync(List<TableSchema> tables)
        {
            var columns = new List<ColumnSchema>();
            string sql = @"
                SELECT
                    c.TABLE_SCHEMA,
                    c.TABLE_NAME,
                    c.COLUMN_NAME,
                    c.ORDINAL_POSITION,
                    c.DATA_TYPE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    c.NUMERIC_PRECISION,
                    c.NUMERIC_SCALE,
                    c.IS_NULLABLE,
                    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS IS_IDENTITY,
                    COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsComputed') AS IS_COMPUTED,
                    CASE WHEN c.DATA_TYPE = 'timestamp' OR c.DATA_TYPE = 'rowversion' THEN 1 ELSE 0 END AS IS_TIMESTAMP
                FROM INFORMATION_SCHEMA.COLUMNS c
                ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION;";
            try
            {
                DataTable dt = await _dbManager.ExecuteQueryAsync(sql, null); // Use await
                foreach (DataRow row in dt.Rows)
                {
                    string schemaName = row["TABLE_SCHEMA"].ToString();
                    string tableName = row["TABLE_NAME"].ToString();
                    var parentTable = tables.FirstOrDefault(t => t.SchemaName == schemaName && t.TableName == tableName);
                    if (parentTable == null) continue; // Skip columns for tables not found (shouldn't happen)

                    columns.Add(new ColumnSchema
                    {
                        ParentTable = parentTable,
                        ColumnName = row["COLUMN_NAME"].ToString(),
                        OrdinalPosition = Convert.ToInt32(row["ORDINAL_POSITION"]),
                        DataType = row["DATA_TYPE"].ToString(),
                        MaxLength = row["CHARACTER_MAXIMUM_LENGTH"] != DBNull.Value ? Convert.ToInt32(row["CHARACTER_MAXIMUM_LENGTH"]) : (int?)null,
                        NumericPrecision = row["NUMERIC_PRECISION"] != DBNull.Value ? Convert.ToInt32(row["NUMERIC_PRECISION"]) : (int?)null,
                        NumericScale = row["NUMERIC_SCALE"] != DBNull.Value ? Convert.ToInt32(row["NUMERIC_SCALE"]) : (int?)null,
                        IsNullable = row["IS_NULLABLE"].ToString().Equals("YES", StringComparison.OrdinalIgnoreCase),
                        IsIdentity = row["IS_IDENTITY"] != DBNull.Value && Convert.ToInt32(row["IS_IDENTITY"]) == 1,
                        IsComputed = row["IS_COMPUTED"] != DBNull.Value && Convert.ToInt32(row["IS_COMPUTED"]) == 1,
                        IsTimestamp = Convert.ToInt32(row["IS_TIMESTAMP"]) == 1
                    });
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to retrieve column information.", ex);
                throw;
            }
            return columns;
        }

        private async Task<List<PrimaryKeySchema>> GetPrimaryKeysAsync(List<TableSchema> tables)
        {
            var primaryKeys = new List<PrimaryKeySchema>();
            string sql = @"
                SELECT
                    kcu.TABLE_SCHEMA,
                    kcu.TABLE_NAME,
                    kcu.COLUMN_NAME,
                    kcu.ORDINAL_POSITION,
                    tc.CONSTRAINT_NAME
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    ON kcu.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                    AND kcu.TABLE_SCHEMA = tc.TABLE_SCHEMA
                    AND kcu.TABLE_NAME = tc.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ORDER BY kcu.TABLE_SCHEMA, kcu.TABLE_NAME, kcu.ORDINAL_POSITION;";
            try
            {
                DataTable dt = await _dbManager.ExecuteQueryAsync(sql, null); // Use await
                foreach (DataRow row in dt.Rows)
                {
                     string schemaName = row["TABLE_SCHEMA"].ToString();
                    string tableName = row["TABLE_NAME"].ToString();
                    var parentTable = tables.FirstOrDefault(t => t.SchemaName == schemaName && t.TableName == tableName);
                    if (parentTable == null) continue;

                    var column = parentTable.Columns.FirstOrDefault(c => c.ColumnName == row["COLUMN_NAME"].ToString());
                    if (column == null) continue; // Should have column info already

                    column.IsPrimaryKey = true; // Mark the column as part of PK

                    primaryKeys.Add(new PrimaryKeySchema
                    {
                        ParentTable = parentTable,
                        Column = column,
                        KeyName = row["CONSTRAINT_NAME"].ToString(),
                        OrdinalPosition = Convert.ToInt32(row["ORDINAL_POSITION"])
                    });
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to retrieve primary key information.", ex);
                throw;
            }
            return primaryKeys;
        }

        private async Task<List<ForeignKeySchema>> GetForeignKeysAsync(List<TableSchema> tables)
        {
            var foreignKeys = new List<ForeignKeySchema>();
             string sql = @"
                SELECT
                    fk.name AS FK_Name,
                    ts.name AS Referencing_Schema,
                    tp.name AS Referencing_Table,
                    pc.name AS Referencing_Column,
                    trs.name AS Referenced_Schema,
                    trp.name AS Referenced_Table,
                    rc.name AS Referenced_Column
                FROM sys.foreign_keys AS fk
                INNER JOIN sys.foreign_key_columns AS fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.tables AS tp ON fkc.parent_object_id = tp.object_id
                INNER JOIN sys.schemas AS ts ON tp.schema_id = ts.schema_id
                INNER JOIN sys.columns AS pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
                INNER JOIN sys.tables AS trp ON fkc.referenced_object_id = trp.object_id
                INNER JOIN sys.schemas AS trs ON trp.schema_id = trs.schema_id
                INNER JOIN sys.columns AS rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
                ORDER BY Referencing_Schema, Referencing_Table, FK_Name;";
            try
            {
                DataTable dt = await _dbManager.ExecuteQueryAsync(sql, null); // Use await
                foreach (DataRow row in dt.Rows)
                {
                    string referencingSchema = row["Referencing_Schema"].ToString();
                    string referencingTable = row["Referencing_Table"].ToString();
                    string referencedSchema = row["Referenced_Schema"].ToString();
                    string referencedTable = row["Referenced_Table"].ToString();

                    var parentTable = tables.FirstOrDefault(t => t.SchemaName == referencingSchema && t.TableName == referencingTable);
                    var pkTable = tables.FirstOrDefault(t => t.SchemaName == referencedSchema && t.TableName == referencedTable);

                    if (parentTable == null || pkTable == null) continue; // Skip if tables involved aren't in our list

                    var referencingColumn = parentTable.Columns.FirstOrDefault(c => c.ColumnName == row["Referencing_Column"].ToString());
                    var referencedColumn = pkTable.Columns.FirstOrDefault(c => c.ColumnName == row["Referenced_Column"].ToString());

                    if (referencingColumn == null || referencedColumn == null) continue; // Skip if columns not found

                    referencingColumn.IsForeignKey = true; // Mark the column

                    foreignKeys.Add(new ForeignKeySchema
                    {
                        ConstraintName = row["FK_Name"].ToString(),
                        ReferencingTable = parentTable,
                        ReferencingColumn = referencingColumn,
                        ReferencedTable = pkTable,
                        ReferencedColumn = referencedColumn
                    });
                }
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to retrieve foreign key information.", ex);
                throw;
            }
            return foreignKeys;
        }
    }
}