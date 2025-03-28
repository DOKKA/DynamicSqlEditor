using System;
using System.Collections.Generic;
using System.Linq; // Ensure Linq is included
using DynamicSqlEditor.Configuration;
using DynamicSqlEditor.DataAccess;
using DynamicSqlEditor.Schema; // <-- Make sure this line exists
using DynamicSqlEditor.Schema.Models;
using DynamicSqlEditor.Common;
using System.Threading.Tasks; // For FileLogger

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

        public async Task<bool> InitializeAsync() // Make Initialize async
        {
            bool configLoaded = ConfigManager.LoadConfiguration();

            if (ConfigManager.CurrentConfig?.Connection?.ConnectionString != null)
            {
                // Assuming ConnectToDatabase remains synchronous for simplicity here,
                // but if it does async work, make it async too.
                if (ConnectToDatabase())
                {
                    await RefreshSchemaAsync(); // Await the async refresh
                    return true;
                }
                else
                {
                    // Connection failed based on initial config
                    return false;
                }
            }
            else
            {
                FileLogger.Warning("No connection string found in configuration. Cannot connect automatically.");
                return false; // Cannot proceed without connection
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
                DbManager?.Dispose(); // Dispose previous connection if any
                DbManager = new DatabaseManager(connStr, timeout);
                DbManager.TestConnection(); // Verify connection works

                CurrentDatabaseName = DbManager.GetDatabaseName();
                FileLogger.Info($"Successfully connected to database: {CurrentDatabaseName}");

                // Reload config potentially merging DB specific file
                ConfigManager.LoadConfiguration(CurrentDatabaseName);

                // Update DbManager timeout if config changed
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

        public async Task RefreshSchemaAsync() // Renamed and made async
        {
            if (!IsConnected || SchemaProvider == null)
            {
                AvailableTables.Clear();
                OnSchemaRefreshed(); // This event needs careful handling if UI updates directly
                return;
            }

            try
            {
                FileLogger.Info("Refreshing database schema...");
                // Use await when calling the async version
                var allTables = await SchemaProvider.GetAllTablesAsync();
                AvailableTables = SchemaFilter.FilterTables(allTables, ConfigManager.CurrentConfig.Global);
                FileLogger.Info($"Schema refreshed. Found {AvailableTables.Count} available tables after filtering.");
                OnSchemaRefreshed(); // Consider invoking UI updates safely if needed here
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