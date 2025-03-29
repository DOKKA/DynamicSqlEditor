using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DynamicSqlEditor.Common;
using DynamicSqlEditor.Configuration;
using DynamicSqlEditor.DataAccess;
using DynamicSqlEditor.Schema;
using DynamicSqlEditor.Schema.Models;

namespace DynamicSqlEditor.Core
{
    public class StateManager
    {
        public ConfigurationManager ConfigManager { get; }
        public DatabaseManager DbManager { get; private set; }
        public SchemaProvider SchemaProvider { get; private set; }
        public List<TableSchema> AvailableTables { get; private set; } = new List<TableSchema>();
        public bool IsConnected => DbManager?.IsConnected() ?? false;
        public string CurrentDatabaseName { get; private set; }

        public event EventHandler ConnectionChanged;
        public event EventHandler SchemaRefreshed;

        public StateManager()
        {
            ConfigManager = new ConfigurationManager();
        }

        public async Task<bool> InitializeAsync()
        {
            bool configLoaded = ConfigManager.LoadConfiguration();

            if (ConfigManager.CurrentConfig?.Connection?.ConnectionString != null)
            {
                if (ConnectToDatabase())
                {
                    await RefreshSchemaAsync();
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                FileLogger.Warning("No connection string found in configuration. Cannot connect automatically.");
                return false;
            }
        }

        public bool ConnectToDatabase(string connectionString = null, int? queryTimeout = null)
        {
            string connStr = connectionString ?? ConfigManager.CurrentConfig?.Connection?.ConnectionString;
            int timeout = queryTimeout ?? ConfigManager.CurrentConfig?.Connection?.QueryTimeout ?? Constants.DefaultQueryTimeout;

            if (string.IsNullOrEmpty(connStr))
            {
                FileLogger.Error("Attempted to connect with an empty connection string.");
                return false;
            }

            try
            {
                DbManager?.Dispose();
                DbManager = new DatabaseManager(connStr, timeout);
                DbManager.TestConnection();

                CurrentDatabaseName = DbManager.GetDatabaseName();
                FileLogger.Info($"Successfully connected to database: {CurrentDatabaseName}");

                ConfigManager.LoadConfiguration(CurrentDatabaseName);

                DbManager.DefaultTimeout = ConfigManager.CurrentConfig.Connection.QueryTimeout;

                SchemaProvider = new SchemaProvider(DbManager);
                OnConnectionChanged();
                return true;
            }
            catch (Exception ex)
            {
                FileLogger.Error($"Database connection failed for string: {connStr}", ex);
                DbManager = null;
                SchemaProvider = null;
                CurrentDatabaseName = null;
                OnConnectionChanged();
                return false;
            }
        }

        public void Disconnect()
        {
            DbManager?.Dispose();
            DbManager = null;
            SchemaProvider = null;
            AvailableTables.Clear();
            CurrentDatabaseName = null;
            FileLogger.Info("Disconnected from database.");
            OnConnectionChanged();
            OnSchemaRefreshed();
        }

        public async Task RefreshSchemaAsync()
        {
            if (!IsConnected || SchemaProvider == null)
            {
                AvailableTables.Clear();
                OnSchemaRefreshed();
                return;
            }

            try
            {
                FileLogger.Info("Refreshing database schema...");
                var allTables = await SchemaProvider.GetAllTablesAsync();
                AvailableTables = SchemaFilter.FilterTables(allTables, ConfigManager.CurrentConfig.Global);
                FileLogger.Info($"Schema refreshed. Found {AvailableTables.Count} available tables after filtering.");
                OnSchemaRefreshed();
            }
            catch (Exception ex)
            {
                FileLogger.Error("Failed to refresh database schema.", ex);
                AvailableTables.Clear();
                OnSchemaRefreshed();
            }
        }

        protected virtual void OnConnectionChanged()
        {
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnSchemaRefreshed()
        {
            SchemaRefreshed?.Invoke(this, EventArgs.Empty);
        }
    }
}