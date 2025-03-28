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

        public async Task<List<TableSchema>> GetAllTablesAsync()
        {
            // 1. Get base table list
            var tables = await GetTablesAndViewsAsync();
            if (!tables.Any()) return tables;

            // Create a lookup dictionary for efficient access during linking
            var tableMap = tables.ToDictionary(t => $"{t.SchemaName}.{t.TableName}", StringComparer.OrdinalIgnoreCase);

            // 2. Fetch all raw data concurrently
            var columnsTask = GetColumnsAsync(); // Returns List<ColumnSchema> with names, not linked ParentTable yet
            var primaryKeysTask = GetPrimaryKeysAsync(); // Returns List<PrimaryKeySchema> with names
            var foreignKeysTask = GetForeignKeysAsync(); // Returns List<ForeignKeySchema> with names

            // Await all results
            var columnData = await columnsTask;
            var primaryKeyData = await primaryKeysTask;
            var foreignKeyData = await foreignKeysTask;

            // --- 3. Process and Link Data AFTER all fetching is complete ---
            FileLogger.Info("Linking schema information...");

            // Link Columns to Tables
            foreach (var col in columnData)
            {
                if (tableMap.TryGetValue($"{col.ParentTableSchemaName}.{col.ParentTableName}", out var table))
                {
                    col.ParentTable = table; // Assign the actual TableSchema object reference NOW
                    table.Columns.Add(col);
                }
                else
                {
                    FileLogger.Warning($"Column '{col.ColumnName}' references table '{col.ParentTableSchemaName}.{col.ParentTableName}' which was not found in the initial table list.");
                }
            }
            FileLogger.Info($"Linked {columnData.Count} columns to {tableMap.Count} tables.");

            // Link Primary Keys to Tables and Columns
            int linkedPKs = 0;
            foreach (var pk in primaryKeyData)
            {
                if (tableMap.TryGetValue($"{pk.ParentTableSchemaName}.{pk.ParentTableName}", out var table))
                {
                    pk.ParentTable = table;
                    // Find the ColumnSchema object that was just added to the table's Columns list
                    pk.Column = table.Columns.FirstOrDefault(c => c.ColumnName.Equals(pk.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (pk.Column != null)
                    {
                        pk.Column.IsPrimaryKey = true; // Mark the column object
                        table.PrimaryKeys.Add(pk);
                        linkedPKs++;
                    }
                    else
                    {
                        FileLogger.Warning($"Primary key '{pk.KeyName}' references column '{pk.ColumnName}' which was not found in table '{table.FullName}'.");
                    }
                }
                else
                {
                    FileLogger.Warning($"Primary key '{pk.KeyName}' references table '{pk.ParentTableSchemaName}.{pk.ParentTableName}' which was not found in the initial table list.");
                }
            }
            FileLogger.Info($"Linked {linkedPKs} primary key columns.");

            // Link Foreign Keys to Tables and Columns
            int linkedFKs = 0;
            foreach (var fk in foreignKeyData)
            {
                // Look up referencing (child) and referenced (parent) tables
                if (tableMap.TryGetValue($"{fk.ReferencingSchemaName}.{fk.ReferencingTableName}", out var referencingTable) &&
                    tableMap.TryGetValue($"{fk.ReferencedSchemaName}.{fk.ReferencedTableName}", out var referencedTable))
                {
                    fk.ReferencingTable = referencingTable;
                    fk.ReferencedTable = referencedTable;

                    // Now, look up columns within the populated lists
                    fk.ReferencingColumn = referencingTable.Columns.FirstOrDefault(c => c.ColumnName.Equals(fk.ReferencingColumnName, StringComparison.OrdinalIgnoreCase));
                    fk.ReferencedColumn = referencedTable.Columns.FirstOrDefault(c => c.ColumnName.Equals(fk.ReferencedColumnName, StringComparison.OrdinalIgnoreCase));

                    if (fk.ReferencingColumn != null && fk.ReferencedColumn != null)
                    {
                        fk.ReferencingColumn.IsForeignKey = true; // Mark the column object
                        referencingTable.ForeignKeys.Add(fk);          // Add to FKs list of child
                        referencedTable.ReferencedByForeignKeys.Add(fk); // Add to ReferencedBy list of parent
                        linkedFKs++;
                    }
                    else
                    {
                        FileLogger.Warning($"Could not fully resolve FK '{fk.ConstraintName}'. Column lookup failed (Child: {fk.ReferencingColumnName} found={fk.ReferencingColumn != null}, Parent: {fk.ReferencedColumnName} found={fk.ReferencedColumn != null}).");
                    }
                }
                else
                {
                    FileLogger.Warning($"Could not fully resolve FK '{fk.ConstraintName}'. Table lookup failed (Child: {fk.ReferencingSchemaName}.{fk.ReferencingTableName} found={referencingTable != null}, Parent: {fk.ReferencedSchemaName}.{fk.ReferencedTableName} ).");
                }
            }
            FileLogger.Info($"Linked {linkedFKs} foreign keys.");

            // 4. Return the fully populated list
            return tables;
        }

        // --- Helper Methods (Modified) ---

        private async Task<List<TableSchema>> GetTablesAndViewsAsync()
        {
            var tables = new List<TableSchema>();
            string sql = @"SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES
                           WHERE TABLE_TYPE IN ('BASE TABLE', 'VIEW') ORDER BY TABLE_SCHEMA, TABLE_NAME;";
            try
            {
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
                FileLogger.Info($"Retrieved {tables.Count} tables and views.");
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to retrieve tables and views.", ex);
                throw;
            }
            return tables;
        }

        private async Task<List<ColumnSchema>> GetColumnsAsync() // No 'tables' parameter
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
                DataTable dt = await _dbManager.ExecuteQueryAsync(sql, null);
                foreach (DataRow row in dt.Rows)
                {
                    columns.Add(new ColumnSchema
                    {
                        // Store names needed for later lookup
                        ParentTableSchemaName = row["TABLE_SCHEMA"].ToString(),
                        ParentTableName = row["TABLE_NAME"].ToString(),
                        // Assign other properties
                        ColumnName = row["COLUMN_NAME"].ToString(),
                        OrdinalPosition = Convert.ToInt32(row["ORDINAL_POSITION"]),
                        DataType = row["DATA_TYPE"].ToString(),
                        MaxLength = row["CHARACTER_MAXIMUM_LENGTH"] != DBNull.Value ? Convert.ToInt32(row["CHARACTER_MAXIMUM_LENGTH"]) : (int?)null,
                        NumericPrecision = row["NUMERIC_PRECISION"] != DBNull.Value ? Convert.ToInt32(row["NUMERIC_PRECISION"]) : (int?)null,
                        NumericScale = row["NUMERIC_SCALE"] != DBNull.Value ? Convert.ToInt32(row["NUMERIC_SCALE"]) : (int?)null,
                        IsNullable = row["IS_NULLABLE"].ToString().Equals("YES", StringComparison.OrdinalIgnoreCase),
                        IsIdentity = row["IS_IDENTITY"] != DBNull.Value && Convert.ToInt32(row["IS_IDENTITY"]) == 1,
                        IsComputed = row["IS_COMPUTED"] != DBNull.Value && Convert.ToInt32(row["IS_COMPUTED"]) == 1,
                        IsTimestamp = Convert.ToInt32(row["IS_TIMESTAMP"]) == 1,
                        // Ensure object reference is null initially
                        ParentTable = null
                    });
                }
                FileLogger.Info($"Retrieved {columns.Count} column definitions.");
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to retrieve column information.", ex);
                throw;
            }
            return columns;
        }

        private async Task<List<PrimaryKeySchema>> GetPrimaryKeysAsync() // No 'tables' parameter
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
                DataTable dt = await _dbManager.ExecuteQueryAsync(sql, null);
                foreach (DataRow row in dt.Rows)
                {
                    primaryKeys.Add(new PrimaryKeySchema
                    {
                        // Store names needed for later lookup
                        ParentTableSchemaName = row["TABLE_SCHEMA"].ToString(),
                        ParentTableName = row["TABLE_NAME"].ToString(),
                        ColumnName = row["COLUMN_NAME"].ToString(), // Store column name
                        // Other properties
                        KeyName = row["CONSTRAINT_NAME"].ToString(),
                        OrdinalPosition = Convert.ToInt32(row["ORDINAL_POSITION"]),
                        // Ensure object references are null initially
                        ParentTable = null,
                        Column = null
                    });
                }
                FileLogger.Info($"Retrieved {primaryKeys.Count} primary key column definitions.");
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to retrieve primary key information.", ex);
                throw;
            }
            return primaryKeys;
        }

        private async Task<List<ForeignKeySchema>> GetForeignKeysAsync() // No 'tables' parameter
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
                DataTable dt = await _dbManager.ExecuteQueryAsync(sql, null);
                foreach (DataRow row in dt.Rows)
                {
                    foreignKeys.Add(new ForeignKeySchema
                    {
                        ConstraintName = row["FK_Name"].ToString(),
                        // Store names
                        ReferencingSchemaName = row["Referencing_Schema"].ToString(),
                        ReferencingTableName = row["Referencing_Table"].ToString(),
                        ReferencingColumnName = row["Referencing_Column"].ToString(),
                        ReferencedSchemaName = row["Referenced_Schema"].ToString(),
                        ReferencedTableName = row["Referenced_Table"].ToString(),
                        ReferencedColumnName = row["Referenced_Column"].ToString(),
                        // Ensure object references are null initially
                        ReferencingTable = null,
                        ReferencingColumn = null,
                        ReferencedTable = null,
                        ReferencedColumn = null
                    });
                }
                FileLogger.Info($"Retrieved {foreignKeys.Count} foreign key definitions.");
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