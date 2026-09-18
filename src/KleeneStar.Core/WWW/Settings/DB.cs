using KleeneStar.Model;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using WebExpress.WebApp.WebScope;
using WebExpress.WebApp.WebSettingPage;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebSettingPage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Core.WWW.Settings
{
    /// <summary>
    /// Represents the database settings page and provides administrative scope functionality.
    /// </summary>
    [Title("kleenestar.core:setting.database.title")]
    [WebIcon<IconDatabase>]
    [SettingGroup<SettingGroupSystemGeneral>()]
    [SettingSection(SettingSection.Secondary)]
    [Scope<IScopeAdmin>]
    public sealed class DB : ISettingPage<VisualTreeWebAppSetting>, IScopeAdmin
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public DB()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebAppSetting visualTree)
        {
            // retrieve provider and connection string
            var dbInfo = ModelHub.DatabaseSettings;
            var providerName = dbInfo.Provider ?? string.Empty;
            var connectionString = dbInfo.ConnectionString ?? string.Empty;

            visualTree.Content.MainPanel.AddPrimary(new ControlText()
            {
                Text = _ => I18N.Translate
                (
                    renderContext,
                    "kleenestar.core:setting.group.database.label"
                ),
                TextColor = _ => new PropertyColorText(TypeColorText.Info),
                Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two)
            });

            // mask password for display
            var maskedConnectionString = MaskConnectionString(connectionString);

            // create base table and add static rows
            var table = new ControlTable()
            {
                Striped = _ => TypeStripedTable.Row,
                SuppressHeaders = _ => true
            }
                .AddColumn("")
                .AddColumn("")
                .AddRow
                (
                    new ControlTableCell()
                    {
                        Text = _ => I18N.Translate(renderContext, "kleenestar.core:setting.database.provider.label")
                    },
                    new ControlTableCellPanel().Add(new ControlText()
                    {
                        Text = _ => providerName,
                        Format = _ => TypeFormatText.Code
                    })
                )
                .AddRow
                (
                    new ControlTableCell()
                    {
                        Text = _ => I18N.Translate(renderContext, "kleenestar.core:setting.database.datasource.label")
                    },
                    new ControlTableCellPanel().Add(new ControlText()
                    {
                        Text = _ => maskedConnectionString,
                        Format = _ => TypeFormatText.Code
                    })
                );

            // attempt to open a connection via factory or provider-specific fallback
            try
            {
                using var conn = CreateDbConnection(providerName, connectionString);
                if (conn is null)
                {
                    // fallback: cannot create connection, render minimal info
                    visualTree.Content.MainPanel.AddPrimary(table);
                    return;
                }

                conn.Open();

                // add server/version info
                string serverVersion = "(unknown)";
                try
                {
                    serverVersion = conn.ServerVersion ?? "(unknown)";
                }
                catch
                {
                    // some providers may not expose ServerVersion; ignore
                }

                table.AddRow
                (
                    new ControlTableCell() { Text = _ => "Server version" },
                    new ControlTableCellPanel().Add(new ControlText() { Text = _ => serverVersion, Format = _ => TypeFormatText.Code })
                );

                // provider-specific queries
                var lowerProvider = (providerName ?? string.Empty).ToLowerInvariant();

                if (lowerProvider.Contains("sqlclient") || lowerProvider.Contains("sqlserver") || lowerProvider.Contains("system.data.sqlclient"))
                {
                    // sql server specific queries
                    table.AddRow(new ControlTableCell() { Text = _ => "Database type" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => "SQL Server", Format = _ => TypeFormatText.Code }));

                    using var cmd = conn.CreateCommand();
                    // database size in bytes
                    cmd.CommandText = "SELECT SUM(size) * 8 * 1024 FROM sys.master_files WHERE database_id = DB_ID()";
                    var dbSizeObj = cmd.ExecuteScalar();
                    if (dbSizeObj != null && dbSizeObj != DBNull.Value)
                    {
                        table.AddRow(new ControlTableCell() { Text = _ => "Database size (bytes)" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => dbSizeObj.ToString(), Format = _ => TypeFormatText.Code }));
                    }

                    // top tables by rowcount
                    cmd.CommandText = @"
                                SELECT t.name, SUM(p.rows) AS row_count
                                FROM sys.tables t
                                JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
                                GROUP BY t.name
                                ORDER BY row_count DESC";
                    using var reader = cmd.ExecuteReader();
                    int count = 0;
                    var topRows = new List<string>();
                    while (reader.Read() && count < 5)
                    {
                        var name = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                        var rows = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                        topRows.Add($"{name} ({rows})");
                        count++;
                    }

                    if (topRows.Count > 0)
                    {
                        table.AddRow(new ControlTableCell() { Text = _ => "Top tables (rows)" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => string.Join(", ", topRows), Format = _ => TypeFormatText.Code }));
                    }
                }
                else if (lowerProvider.Contains("sqlite") || lowerProvider.Contains("system.data.sqlite") || LooksLikeSqliteConnectionString(connectionString))
                {
                    // sqlite: show database file path and size if available
                    table.AddRow(new ControlTableCell() { Text = _ => "Database type" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => "SQLite", Format = _ => TypeFormatText.Code }));

                    // attempt to resolve the sqlite file path from the connection string
                    string filePath = ResolveSqliteFilePath(connectionString);
                    if (string.IsNullOrEmpty(filePath))
                    {
                        table.AddRow(new ControlTableCell() { Text = _ => "Database file" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => "(unknown or in-memory)", Format = _ => TypeFormatText.Code }));
                    }
                    else
                    {
                        try
                        {
                            // get file information
                            var fileInfo = new FileInfo(filePath);
                            if (fileInfo.Exists)
                            {
                                table.AddRow(new ControlTableCell() { Text = _ => "Database file" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => fileInfo.FullName, Format = _ => TypeFormatText.Code }));
                                table.AddRow(new ControlTableCell() { Text = _ => "Database file size (bytes)" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => fileInfo.Length.ToString(), Format = _ => TypeFormatText.Code }));
                            }
                            else
                            {
                                table.AddRow(new ControlTableCell() { Text = _ => "Database file" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => fileInfo.FullName + " (not found)", Format = _ => TypeFormatText.Code }));
                            }
                        }
                        catch (Exception exFile)
                        {
                            table.AddRow(new ControlTableCell() { Text = _ => "Database file" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => $"{filePath} (error: {exFile.Message})", Format = _ => TypeFormatText.Code }));
                        }
                    }
                }
                else if (lowerProvider.Contains("npgsql") || lowerProvider.Contains("postgres"))
                {
                    // postgresql specific queries
                    table.AddRow(new ControlTableCell() { Text = _ => "Database type" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => "PostgreSQL", Format = _ => TypeFormatText.Code }));

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT version()";
                    var ver = cmd.ExecuteScalar();
                    if (ver is not null)
                    {
                        table.AddRow(new ControlTableCell() { Text = _ => "Server version" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => ver.ToString(), Format = _ => TypeFormatText.Code }));
                    }

                    cmd.CommandText = "SELECT pg_database_size(current_database())";
                    var sizeObj = cmd.ExecuteScalar();
                    if (sizeObj is not null)
                    {
                        table.AddRow(new ControlTableCell() { Text = _ => "Database size (bytes)" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => sizeObj.ToString(), Format = _ => TypeFormatText.Code }));
                    }

                    cmd.CommandText = @"
                                SELECT relname, n_live_tup
                                FROM pg_stat_user_tables
                                ORDER BY n_live_tup DESC
                                LIMIT 5";
                    using var reader = cmd.ExecuteReader();
                    var top = new List<string>();
                    while (reader.Read())
                    {
                        var name = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                        var rows = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);
                        top.Add($"{name} ({rows})");
                    }

                    if (top.Count > 0)
                    {
                        table.AddRow(new ControlTableCell() { Text = _ => "Top tables (estimate)" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => string.Join(", ", top), Format = _ => TypeFormatText.Code }));
                    }
                }
                else if (lowerProvider.Contains("mysql") || lowerProvider.Contains("mariadb"))
                {
                    // mysql specific queries
                    table.AddRow(new ControlTableCell() { Text = _ => "Database type" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => "MySQL/MariaDB", Format = _ => TypeFormatText.Code }));

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT VERSION()";
                    var ver = cmd.ExecuteScalar();
                    if (ver is not null)
                    {
                        table.AddRow(new ControlTableCell() { Text = _ => "Server version" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => ver.ToString(), Format = _ => TypeFormatText.Code }));
                    }

                    cmd.CommandText = "SELECT SUM(data_length + index_length) FROM information_schema.tables WHERE table_schema = DATABASE()";
                    var sizeObj = cmd.ExecuteScalar();
                    if (sizeObj is not null)
                    {
                        table.AddRow(new ControlTableCell() { Text = _ => "Database size (bytes)" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => sizeObj.ToString(), Format = _ => TypeFormatText.Code }));
                    }

                    cmd.CommandText = @"
                                SELECT table_name, table_rows
                                FROM information_schema.tables
                                WHERE table_schema = DATABASE()
                                ORDER BY table_rows DESC
                                LIMIT 5";
                    using var reader = cmd.ExecuteReader();
                    var top = new List<string>();
                    while (reader.Read())
                    {
                        var name = reader.IsDBNull(0) ? "(null)" : reader.GetString(0);
                        var rows = reader.IsDBNull(1) ? 0L : reader.GetInt64(1);
                        top.Add($"{name} ({rows})");
                    }

                    if (top.Count > 0)
                    {
                        table.AddRow(new ControlTableCell() { Text = _ => "Top tables (approx.)" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => string.Join(", ", top), Format = _ => TypeFormatText.Code }));
                    }
                }
                else
                {
                    // fallback: display basic connection info
                    table.AddRow(new ControlTableCell() { Text = _ => "Database type" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => providerName, Format = _ => TypeFormatText.Code }));
                }

                conn.Close();
            }
            catch (Exception ex)
            {
                // in case of failure show the error message in the table
                table.AddRow(new ControlTableCell() { Text = _ => "Database diagnostics" }, new ControlTableCellPanel().Add(new ControlText() { Text = _ => ex.Message, Format = _ => TypeFormatText.Code }));
            }

            // add table to main panel
            visualTree.Content.MainPanel.AddPrimary(table);
        }

        /// <summary>
        /// Returns a version of the specified connection string with password values masked 
        /// for security.
        /// </summary>
        /// <param name="cs">
        /// The connection string to process. May contain sensitive information such as passwords.
        /// </param>
        /// <returns>
        /// A connection string with the values of password-related keys (such as "Password" or "Pwd") 
        /// replaced by asterisks. If the input is null or empty, the original value is returned.
        /// </returns>
        private static string MaskConnectionString(string cs)
        {
            if (string.IsNullOrEmpty(cs))
            {
                return cs;
            }

            // split into key=value parts
            var parts = cs.Split([';'], StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var idx = part.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }

                var key = part[..idx].Trim();
                _ = part[(idx + 1)..];

                // mask common password keys (case-insensitive)
                var lowerKey = key.ToLowerInvariant();
                if (lowerKey == "password" || lowerKey == "pwd")
                {
                    parts[i] = $"{key}=*****";
                }
            }

            // reassemble preserving order
            return string.Join(";", parts) + (cs.EndsWith(";") ? ";" : string.Empty);
        }

        /// <summary>
        /// Extracts the file path to the SQLite database from the specified connection string, 
        /// if present.
        /// </summary>
        /// <param name="cs">
        /// The SQLite connection string from which to extract the database file path. Cannot 
        /// be null or empty.
        /// </param>
        /// <returns>
        /// The file path to the SQLite database as specified in the connection string, or 
        /// null if the path cannot be determined or if the connection string refers to an 
        /// in-memory database.
        /// </returns>
        private static string ResolveSqliteFilePath(string cs)
        {
            if (string.IsNullOrEmpty(cs))
            {
                return null;
            }

            try
            {
                // use DbConnectionStringBuilder to parse the connection string
                var builder = new DbConnectionStringBuilder() { ConnectionString = cs };

                // check common key names case-insensitively
                foreach (object keyObj in builder.Keys)
                {
                    // keys in builder may have differing casing; compare lowercased
                    var key = keyObj?.ToString() ?? string.Empty;
                    var lowerKey = key.ToLowerInvariant();

                    if (lowerKey == "data source" || lowerKey == "datasource" || lowerKey == "filename" || lowerKey == "file" || lowerKey == "datafile")
                    {
                        object valObj = builder[key];
                        if (valObj != null)
                        {
                            var path = valObj.ToString();
                            // sqlite in-memory indicator
                            if (path == ":memory:")
                            {
                                return null;
                            }

                            return path;
                        }
                    }
                }

                // fallback: try to parse "Data Source=" manually (common formats)
                var parts = cs.Split([';'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var idx = part.IndexOf('=');
                    if (idx <= 0)
                    {
                        continue;
                    }

                    var k = part[..idx].Trim().ToLowerInvariant();
                    var v = part[(idx + 1)..].Trim();
                    if (k == "data source" || k == "datasource" || k == "filename" || k == "file" || k == "datafile")
                    {
                        if (v == ":memory:")
                        {
                            return null;
                        }

                        return v;
                    }
                }
            }
            catch
            {
                // ignore parsing errors and return null
            }

            return null;
        }

        /// <summary>
        /// crude guess if connection string likely points to sqlite
        /// </summary>
        /// <param name="cs">connection string</param>
        /// <returns>true if looks like sqlite</returns>
        private static bool LooksLikeSqliteConnectionString(string cs)
        {
            if (string.IsNullOrEmpty(cs))
            {
                return false;
            }

            var lower = cs.ToLowerInvariant();
            if (lower.Contains("data source") && (lower.Contains(".db") || lower.Contains(".sqlite") || lower.Contains(":memory:")))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// create a DbConnection using DbProviderFactories or sqlite-specific fallbacks
        /// </summary>
        /// <param name="provider">provider invariant name</param>
        /// <param name="cs">connection string</param>
        /// <returns>a DbConnection or null if creation failed</returns>
        private static DbConnection CreateDbConnection(string provider, string cs)
        {
            // try factory first (works when provider is registered)
            try
            {
                // get factory may throw if provider is not registered; wrap in try/catch
                var factory = DbProviderFactories.GetFactory(provider);
                if (factory != null)
                {
                    var conn = factory.CreateConnection();
                    if (conn != null)
                    {
                        conn.ConnectionString = cs;
                        return conn;
                    }
                }
            }
            catch
            {
                // swallow and try sqlite-specific fallbacks
            }

            // if provider indicates sqlite or connection string looks like sqlite, try sqlite providers directly
            var lowerProvider = (provider ?? string.Empty).ToLowerInvariant();
            if (lowerProvider.Contains("sqlite") || LooksLikeSqliteConnectionString(cs))
            {
                // try Microsoft.Data.Sqlite via direct type (works if package referenced)
                try
                {
                    // try to get the type by name; this avoids hard compile-time dependency
                    var msType = Type.GetType("Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite");
                    if (msType != null)
                    {
                        // create instance using constructor (string)
                        var msConn = Activator.CreateInstance(msType, new[] { cs }) as DbConnection;
                        if (msConn != null)
                        {
                            return msConn;
                        }
                    }
                }
                catch
                {
                    // ignore and try next option
                }

                // try System.Data.SQLite via reflection (if present)
                try
                {
                    var sysType = Type.GetType("System.Data.SQLite.SQLiteConnection, System.Data.SQLite");
                    if (sysType != null)
                    {
                        var sysConn = Activator.CreateInstance(sysType, new[] { cs }) as DbConnection;
                        if (sysConn != null)
                        {
                            return sysConn;
                        }
                    }
                }
                catch
                {
                    // ignore and fall through
                }
            }

            // unable to create a connection
            return null;
        }
    }
}