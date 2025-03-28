# Dynamic SQL Editor

## Overview

Dynamic SQL Editor is a Windows Forms application designed to provide a flexible and configurable interface for browsing and editing data in Microsoft SQL Server databases. Instead of requiring developers to build custom UI screens for every table, this tool dynamically generates data views, detail forms, filters, and related data tabs based on database schema and a custom configuration Domain-Specific Language (DSL).

This allows users (like administrators, support staff, or developers) to quickly view, filter, sort, page through, and perform CRUD (Create, Read, Update, Delete) operations on tables without needing dedicated application features for each one.

## Features

*   **Dynamic UI Generation:** Creates data grids, detail editing forms, and related data views based on database schema and configuration.
*   **Configuration Driven:** Uses simple `.dsl` text files to control behavior, appearance, and available features per table.
*   **Schema Discovery:** Automatically detects tables, views, columns, primary keys, and foreign keys.
*   **CRUD Operations:** Supports viewing, creating, updating, and deleting records with basic concurrency handling (using `timestamp`/`rowversion` columns if available).
*   **Data Grid:**
    *   Displays table data in a familiar grid format.
    *   Column visibility and headers configurable.
    *   Basic data formatting for dates and numbers.
*   **Paging:** Efficiently handles large tables using server-side paging (supports `OFFSET/FETCH` and fallback `ROW_NUMBER()`). Configurable page size.
*   **Sorting:** Allows sorting data by clicking column headers. Default sort order configurable.
*   **Filtering:**
    *   Define pre-set filters with custom `WHERE` clauses in the configuration.
    *   Supports filters that require user input (text or lookup values).
    *   Configurable default filter per table.
*   **Detail View/Editor:**
    *   Dynamically generated form for viewing/editing individual records.
    *   Control types inferred from data types (TextBox, CheckBox, DateTimePicker) or explicitly configured (MultiLine TextBox, ComboBox, Label, etc.).
    *   Nullable field support with dedicated "Is Null" checkboxes.
    *   Configurable field visibility, read-only status, labels, and display order.
*   **Foreign Key Support:**
    *   Automatically detects foreign keys.
    *   Displays FK columns as ComboBoxes in the detail view.
    *   Populates ComboBoxes using configurable lookups (`FKLookup.*`) or heuristics based on column names (`Name`, `Description`, `*ID`, etc. - configurable globally).
*   **Related Data Tabs:**
    *   Automatically discovers and displays related child data (based on foreign keys referencing the current table).
    *   Configurable explicit child relationships (`RelatedChild.*`).
    *   Lazy loading of related data when a tab is selected.
*   **Custom Actions:**
    *   Define custom buttons (`ActionButton.*`) that execute external commands or scripts.
    *   Can pass data from the selected row as command-line arguments.
    *   Execution can be globally disabled for security.
*   **Configuration Merging:** Supports a base `AppConfig.dsl` and database-specific overrides (`<DatabaseName>.dsl`).
*   **Logging:** Logs operations and errors to files in a `Logs` directory.
*   **Error Handling:** Global exception handlers capture and log unhandled errors.

## Prerequisites

*   **.NET Framework:** Version 4.8 (as specified in `DynamicSqlEditor.csproj`).
*   **SQL Server:** Access to a Microsoft SQL Server database (tested primarily with SQL Server, compatibility with Azure SQL Database is likely but may need verification, especially regarding schema queries).
*   **Visual Studio:** Required for building the project (e.g., Visual Studio 2019 or later with .NET desktop development workload).

## Setup & Installation

1.  **Clone or Download:** Get the source code from the repository.
2.  **Open in Visual Studio:** Open the `DynamicSqlEditor.sln` solution file (if available) or the `DynamicSqlEditor.csproj` project file.
3.  **Restore NuGet Packages:** Visual Studio should automatically restore any necessary packages upon opening (though this project seems to use only framework assemblies).
4.  **Configure Connection:**
    *   Locate the `Config` directory in the project's root. If it doesn't exist, create it.
    *   Create or edit the `AppConfig.dsl` file within the `Config` directory.
    *   Add a `[Connection]` section and set the `ConnectionString` key. See the Configuration section below for details.
    *   Example `Config/AppConfig.dsl`:
        ```ini
        [Connection]
        ConnectionString = Server=your_server_name;Database=your_database_name;Integrated Security=True; # Or User ID=...;Password=...;
        QueryTimeout = 60 # Optional: Default query timeout in seconds
        ```
5.  **Build:** Build the solution (Build > Build Solution or Ctrl+Shift+B).
6.  **Run:**
    *   Run the project from Visual Studio (press F5).
    *   Alternatively, navigate to the output directory (e.g., `bin\Debug` or `bin\Release`) and run `DynamicSqlEditor.exe`.

## Configuration (`.dsl` Files)

The application's behavior is heavily controlled by configuration files located in the `Config` directory.

### DSL Syntax

*   **Sections:** Defined by square brackets, e.g., `[Connection]`. Section names are case-insensitive.
*   **Key-Value Pairs:** Settings within sections are defined as `Key = Value`. Keys are case-insensitive.
*   **Comments:** Lines starting with `#` are ignored.
*   **Attributes:** Some values contain comma-separated attributes, often in the format `AttributeName="Value"` or `AttributeName=Value`. Values with spaces should be quoted. Parsing is case-insensitive for attribute names.

### File Loading and Merging

1.  **Base Configuration:** The application first loads `Config\AppConfig.dsl`. This file should contain common settings or defaults.
2.  **Database-Specific Configuration:** After successfully connecting to a database, the application attempts to load `Config\<DatabaseName>.dsl` (e.g., `Config\MyDatabase.dsl`). Settings in this file *overwrite* any settings with the same section and key loaded from `AppConfig.dsl`. This allows for database-specific customizations.

### Configuration Sections and Keys

#### `[Connection]`

Defines how to connect to the SQL Server database.

*   `ConnectionString` (Required): The standard ADO.NET connection string.
*   `QueryTimeout` (Optional): Default command timeout in seconds for SQL queries. Defaults to `60`.

#### `[Global]`

Defines application-wide settings and schema filtering rules.

*   `IncludeSchemas` (Optional): Comma-separated list of schemas to include. Supports wildcards (`*`, `?`, `%`). If omitted, all schemas are considered initially. Example: `dbo, sales, hr*`
*   `ExcludeTables` (Optional): Comma-separated list of tables (in `Schema.Table` format) to exclude. Supports wildcards. Applied *after* `IncludeSchemas`. Example: `dbo.sysdiagrams, dbo.%audit%`
*   `DefaultFKDisplayHeuristic` (Optional): Comma-separated, ordered list of column names or patterns used to automatically find the best display column for foreign key ComboBoxes when not explicitly configured via `FKLookup`. Defaults to `Name, Description, Title, *ID`.
*   `DisableCustomActionExecution` (Optional): `True` or `False`. If `True`, prevents `ActionButton` commands from being executed. Useful for security. Defaults to `False`.

#### `[Table:SchemaName.TableName]`

Defines settings specific to a particular table or view. Replace `SchemaName.TableName` with the actual schema and table name (e.g., `[Table:dbo.Customers]`).

*   `CustomSelectQuery` (Optional): Allows providing a completely custom SQL query for fetching data for the grid and detail view.
    *   **Crucial:** Must contain the placeholders `{WHERE}`, `{ORDERBY}`, and `{PAGING}` which the application will replace.
    *   Example: `SELECT c.CustomerID, c.Name, a.City FROM Customers c JOIN Addresses a ON c.AddressID = a.AddressID {WHERE} {ORDERBY} {PAGING}`
    *   **Warning:** Use with caution. Ensure the query is secure and performs well. The application does not heavily sanitize this query.
*   `DefaultSortColumn` (Optional): The column name to sort by default when the view loads.
*   `DefaultSortDirection` (Optional): `Ascending` or `Descending`. Defaults to `Ascending`.
*   `Filter.Default` (Optional): Specifies the default filter to apply when the view loads. Only applies if the filter does not require input.
    *   Attributes:
        *   `FilterName` (Required): The name of the filter defined below (e.g., `FilterName=ActiveCustomers`).
*   `Filter.<FilterName>` (Optional, Multiple): Defines a named filter available in the filter dropdown. Replace `<FilterName>` with a unique name (e.g., `Filter.ActiveCustomers`).
    *   Attributes:
        *   `Label` (Required): The text displayed in the dropdown list (e.g., `Label="Active Customers"`).
        *   `WhereClause` (Required): The SQL `WHERE` clause condition (without the `WHERE` keyword). Parameters can be used if `RequiresInput` is set (e.g., `WhereClause="IsActive = 1 AND Country = @Country"`).
        *   `RequiresInput` (Optional): Comma-separated list of parameter names the user must provide. If a parameter needs a lookup, use `ParamName:LookupType`. `LookupType` can be a table name (`Schema.Table`) or potentially a named `FKLookup` definition (needs verification). Example: `RequiresInput="StartDate, EndDate, Country:dbo.Countries"`.
*   `DetailFormField.<ColumnName>` (Optional, Multiple): Customizes the appearance and behavior of a specific column in the detail editing panel. Replace `<ColumnName>` with the actual column name.
    *   Attributes:
        *   `Label` (Optional): Overrides the default label text (which is the column name).
        *   `Order` (Optional): An integer determining the display order in the detail form (lower numbers appear first). Defaults to a high value (schema order).
        *   `ReadOnly` (Optional): `True` or `False`. Forces the field to be read-only, overriding default behavior (PKs, Identity, Computed, Timestamp are read-only by default).
        *   `Visible` (Optional): `True` or `False`. Hides or shows the field in the detail form. Defaults based on data type (complex types like XML, spatial are hidden by default).
        *   `ControlType` (Optional): Explicitly sets the control type. Useful for overriding defaults. Values: `TextBox`, `TextBoxMultiLine`, `ComboBox`, `CheckBox`, `DateTimePicker`, `Label`. `Default` uses automatic detection.
*   `FKLookup.<FKColumnName>` (Optional, Multiple): Explicitly configures the lookup behavior for a foreign key column's ComboBox in the detail view. Replace `<FKColumnName>` with the foreign key column name in *this* table. Overrides automatic FK detection and heuristics for this column.
    *   Attributes:
        *   `ReferencedTable` (Required): The table being looked up (format `Schema.TableName`, e.g., `ReferencedTable=dbo.Countries`).
        *   `DisplayColumn` (Required): The column from `ReferencedTable` to display in the ComboBox dropdown (e.g., `DisplayColumn=CountryName`).
        *   `ValueColumn` (Optional): The column from `ReferencedTable` whose value should be stored in the `FKColumnName`. Defaults to the primary key of `ReferencedTable`.
        *   `ReferencedColumn` (Optional): The column in `ReferencedTable` that `ValueColumn` corresponds to (usually the same as `ValueColumn` or the PK). Defaults to `ValueColumn` or the PK.
*   `ActionButton.<ButtonName>` (Optional, Multiple): Defines a custom button displayed below the related data tabs. Replace `<ButtonName>` with a unique name.
    *   Attributes:
        *   `Label` (Required): The text displayed on the button (e.g., `Label="Run Report"`).
        *   `Command` (Required): The command or executable to run. Can include placeholders `{ColumnName}` which will be replaced with the value from the currently selected row. Example: `Command="C:\Scripts\ProcessOrder.bat {OrderID} {CustomerName}"`.
        *   `RequiresSelection` (Optional): `True` or `False`. If `True` (default), the button is disabled unless a row is selected.
        *   `SuccessMessage` (Optional): A message box to show after the command is launched.
    *   **Warning:** Commands are executed via `Process.Start`. Ensure commands are safe and consider setting `DisableCustomActionExecution=True` in `[Global]` if untrusted users might modify configuration files.
*   `RelatedChild.<RelationName>` (Optional, Multiple): Defines an explicit relationship to display as a tab, showing child records. Replace `<RelationName>` with a unique name. Overrides automatic detection for the specified child table/FK.
    *   Attributes:
        *   `Label` (Required): The text displayed on the tab (e.g., `Label="Orders"`).
        *   `ChildTable` (Required): The related child table (format `Schema.TableName`, e.g., `ChildTable=dbo.Orders`).
        *   `ChildFKColumn` (Required): The foreign key column in the `ChildTable` that references the *current* table (e.g., `ChildFKColumn=CustomerID`).
        *   `ParentPKColumn` (Optional): The primary key column in the *current* table that `ChildFKColumn` references. Defaults to the single primary key of the current table if omitted. Required if the current table has a composite PK or the FK references a non-PK unique key.
        *   `ChildFilter` (Optional): An additional static SQL `WHERE` clause condition to apply when querying the `ChildTable` (e.g., `ChildFilter="IsActive = 1"`). **Warning:** This is appended directly to the SQL query; avoid user input or complex logic here to prevent injection risks.

## Usage

1.  **Launch:** Run `DynamicSqlEditor.exe`.
2.  **Connect:** The application attempts to connect using the configuration. The status bar indicates the connection status.
3.  **Select Table:** Use the "Tables" menu. Tables are grouped by schema. Click a table name to open its data view in a new MDI child window.
4.  **Browse Data:**
    *   The top panel shows a grid with the table data.
    *   Use the paging controls at the bottom of the grid to navigate through pages. Change the page size using the dropdown.
    *   Click column headers to sort data (click again to toggle direction).
    *   Use the "Filter" dropdown (if available) to apply pre-defined filters. Some filters may prompt for input.
5.  **View/Edit Details:**
    *   Select a row in the grid.
    *   The bottom panel ("Details" tab) shows the data for the selected row in an editable form.
    *   Fields might be read-only based on configuration or schema (PKs, computed columns, etc.).
    *   Make changes to the editable fields. An asterisk (`*`) appears in the window title indicating unsaved changes.
    *   Use the "Is Null" checkbox for nullable fields.
    *   Use ComboBoxes for foreign key lookups.
6.  **CRUD Actions (Detail Panel):**
    *   **New:** Clears the detail form to enter a new record. Disables grid/paging/filtering.
    *   **Save:** Saves the current changes (for a new or modified record). Enabled only when changes are pending. Performs concurrency check if applicable.
    *   **Delete:** Deletes the currently selected record (prompts for confirmation). Performs concurrency check if applicable.
    *   **Refresh:** Reloads the data for the current page, discarding any pending changes in the detail view if prompted.
7.  **Related Data:** Click other tabs in the bottom panel (if any) to view data from related tables linked to the currently selected record in the main grid.
8.  **Custom Actions:** Click custom buttons (if configured) below the tabs to execute external commands, potentially using data from the selected row.
9.  **Window Management:** Use the "Window" menu to arrange or close MDI child windows.
10. **Closing:** Closing a data view window or the main application will prompt to save unsaved changes.

## Architecture Overview

*   **UI Layer (`DynamicSqlEditor.UI`):**
    *   Contains WinForms Forms (`MainForm`, `DataViewForm`), User Controls (`PagingControl`, `NullableDateTimePicker`), Dialogs (`FilterInputDialog`), and Builders (`DetailFormBuilder`, `FilterUIBuilder`, etc.).
    *   Responsible for presenting data and handling user interaction.
    *   Builders dynamically create UI elements based on configuration and schema.
*   **Core Layer (`DynamicSqlEditor.Core`):**
    *   Manages application state (`StateManager`).
    *   Handles data view logic (`DataViewManager` - paging, sorting, filtering).
    *   Orchestrates CRUD operations (`CrudManager`).
*   **DataAccess Layer (`DynamicSqlEditor.DataAccess`):**
    *   Handles direct database interaction (`DatabaseManager`).
    *   Provides data paging implementation (`DataPager`).
    *   Builds SQL queries (`QueryBuilder`).
    *   Manages concurrency checks (`ConcurrencyHandler`).
*   **Schema Layer (`DynamicSqlEditor.Schema`):**
    *   Retrieves database metadata (`SchemaProvider`).
    *   Filters schema based on configuration (`SchemaFilter`).
    *   Defines schema object models (`TableSchema`, `ColumnSchema`, etc.).
*   **Configuration Layer (`DynamicSqlEditor.Configuration`):**
    *   Loads and parses `.dsl` files (`ConfigurationManager`, `DslParser`).
    *   Defines configuration object models (`AppConfig`, `TableConfig`, `*Definition`, etc.).
*   **Common Layer (`DynamicSqlEditor.Common`):**
    *   Provides shared utilities like logging (`FileLogger`), constants (`Constants`), control creation helpers (`ControlFactory`), and global error handling (`GlobalExceptionHandler`).

## Known Issues / Limitations

*   **Complex Data Types:** Limited support for displaying/editing complex SQL types (XML, spatial types, `sql_variant`, UDTs). They are generally hidden by default.
*   **Performance:** Performance on tables with a very large number of columns or rows, or complex custom queries, might degrade. Paging helps with rows, but wide tables can still be slow to render.
*   **DSL Parser:** The DSL parser is basic. It might not handle complex quoting or escaping scenarios robustly.
*   **SQL Injection Risk:** `CustomSelectQuery`, `ActionButton.Command`, and `RelatedChild.ChildFilter` allow embedding configured SQL/commands. While placeholders help for action buttons, care must be taken to avoid SQL injection vulnerabilities, especially if configuration files can be modified by non-administrators. Consider using `DisableCustomActionExecution=True`.
*   **Concurrency:** Basic timestamp/rowversion concurrency is supported. More complex scenarios (e.g., logical deletes, multi-user edits without timestamps) are not handled.
*   **Schema Query Compatibility:** Schema queries are written for SQL Server's `INFORMATION_SCHEMA` and `sys` objects. Compatibility with other database systems is not guaranteed.
*   **No IntelliSense for DSL:** Configuration is done in plain text files.

## Contributing

(Placeholder - Add guidelines if contributions are accepted)
Contributions are welcome! Please follow standard practices like creating issues for bugs or feature requests and submitting pull requests for changes.

## License

(Placeholder - Add license information, e.g., MIT, Apache 2.0)
This project is licensed under the [Your License Name] License - see the LICENSE.md file for details.