using ScriptStack.Runtime;
using System;
//using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace ScriptStack
{
    /// <summary>
    /// Generisches DB-Plugin über ADO.NET DbProviderFactory.
    /// Funktioniert mit MySQL, Postgres, MSSQL, SQLite, Oracle, ... sofern Provider-DLL vorhanden und registriert ist.
    /// </summary>
    public class DB : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;

        private static readonly object _lock = new object();
        private static int _nextHandle = 1;

        // Handle -> Connection
        private static readonly Dictionary<int, DbConnection> _conns = new Dictionary<int, DbConnection>();

        public DB()
        {

            if (exportedRoutines != null) 
                return;

            var r = new List<Routine>();

            r.Add(new Routine(typeof(int), "db_register", typeof(string), typeof(string),"Registriert einen Provider. Params: invariant(string), factoryType(string). Return: 1 wenn ok. Beispiel factoryType: \"Npgsql.NpgsqlFactory, Npgsql\""));

            r.Add(new Routine(typeof(int), "db_open", typeof(string), typeof(string),"Öffnet eine DB-Connection. Params: invariant(string), connStr(string). Return: handle(int)."));

            r.Add(new Routine(typeof(int), "db_close", typeof(int),"Schließt eine Connection. Param: handle(int). Return: 1 wenn ok, sonst 0."));

            r.Add(new Routine(typeof(ArrayList), "db_query", typeof(int), typeof(string),"Führt SQL aus (Query oder NonQuery). Params: handle(int), sql(string). Return: ArrayList {rows, affected, last_id} (last_id meist null)."));

            r.Add(new Routine(typeof(ArrayList), "db_query_p", typeof(int), typeof(string), typeof(ArrayList),"Führt SQL mit Parametern aus. Params: handle(int), sql(string), params(ArrayList). params = ArrayList von ArrayList: p['name'], p['value'], optional p['dbtype']. Return: ArrayList {rows, affected, last_id} (last_id meist null)."));

            r.Add(new Routine((Type)null, "db_scalar", typeof(int), typeof(string),"Führt SQL aus und gibt das erste Feld der ersten Zeile zurück (oder null)."));

            r.Add(new Routine(typeof(int), "db_exec", typeof(int), typeof(string),"Führt NonQuery aus und gibt affected rows zurück."));

            exportedRoutines = r.AsReadOnly();

        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string fn, List<object> p)
        {
            try
            {
                if (fn == "db_register") return RegisterProvider((string)p[0], (string)p[1]);

                if (fn == "db_open") return Open((string)p[0], (string)p[1]);
                if (fn == "db_close") return Close((int)p[0]) ? 1 : 0;

                if (fn == "db_query") return Query((int)p[0], (string)p[1], null);
                if (fn == "db_query_p") return Query((int)p[0], (string)p[1], (ArrayList)p[2]);

                if (fn == "db_scalar") return Scalar((int)p[0], (string)p[1]);
                if (fn == "db_exec") return Exec((int)p[0], (string)p[1]);
            }
            catch (ScriptStackException) { throw; }
            catch (Exception e)
            {
                throw new ScriptStackException(e.Message);
            }

            return null;
        }

        private static int RegisterProvider(string invariant, string factoryType)
        {
            if (string.IsNullOrWhiteSpace(invariant))
                throw new ScriptStackException("db_register: invariant darf nicht leer sein.");
            if (string.IsNullOrWhiteSpace(factoryType))
                throw new ScriptStackException("db_register: factoryType darf nicht leer sein.");

            var t = ResolveType(factoryType);
            if (t == null)
                throw new ScriptStackException("db_register: Factory-Type nicht gefunden: " + factoryType);

            if (!typeof(DbProviderFactory).IsAssignableFrom(t))
                throw new ScriptStackException("db_register: Type ist keine DbProviderFactory: " + t.FullName);

            DbProviderFactories.RegisterFactory(invariant, t);
            return 1;
        }

        private static Type ResolveType(string typeSpec)
        {
            // 1) Direkt versuchen (klappt oft bei "Namespace.Type, Assembly")
            var t = Type.GetType(typeSpec, throwOnError: false);
            if (t != null) return t;

            // 2) Wenn "Type, Assembly" angegeben: Assembly explizit laden
            var parts = typeSpec.Split(',');
            if (parts.Length >= 2)
            {
                var typeName = parts[0].Trim();
                var asmName = parts[1].Trim();

                try
                {
                    var asm = Assembly.Load(new AssemblyName(asmName));
                    t = asm.GetType(typeName, throwOnError: false, ignoreCase: true);
                    if (t != null) return t;
                }
                catch { /* ignore */ }
            }

            // 3) In bereits geladenen Assemblies suchen
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    t = asm.GetType(typeSpec, throwOnError: false, ignoreCase: true);
                    if (t != null) return t;
                }
                catch { /* ignore */ }
            }

            return null;
        }

        private static int Open(string invariant, string connStr)
        {
            DbProviderFactory factory;
            try
            {
                factory = DbProviderFactories.GetFactory(invariant);
            }
            catch (Exception ex)
            {
                throw new ScriptStackException(
                    $"db_open: Provider '{invariant}' nicht registriert. " +
                    $"Nutze db_register(invariant, \"FactoryType, Assembly\"). Details: {ex.Message}");
            }

            var conn = factory.CreateConnection();
            if (conn == null) throw new ScriptStackException("db_open: Provider konnte keine Connection erzeugen.");

            conn.ConnectionString = connStr;
            conn.Open();

            lock (_lock)
            {
                int h = _nextHandle++;
                _conns[h] = conn;
                return h;
            }
        }

        private static bool Close(int handle)
        {
            DbConnection conn = null;

            lock (_lock)
            {
                if (!_conns.TryGetValue(handle, out conn))
                    return false;

                _conns.Remove(handle);
            }

            try { conn.Close(); } catch { }
            try { conn.Dispose(); } catch { }
            return true;
        }

        private static DbConnection GetConn(int handle)
        {
            lock (_lock)
            {
                if (!_conns.TryGetValue(handle, out var conn) || conn == null)
                    throw new ScriptStackException("Ungültiger DB handle: " + handle);

                return conn;
            }
        }

        private static ArrayList Query(int handle, string sql, ArrayList parameters)
        {
            var conn = GetConn(handle);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandType = CommandType.Text;

                ApplyParams(cmd, parameters);

                // Generic: manche Provider können NonQuery nicht via ExecuteReader -> fallback
                try
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        var rows = ReadRows(reader);

                        var result = new ArrayList();
                        result["rows"] = rows;
                        result["affected"] = reader.RecordsAffected;
                        result["last_id"] = null; // DB-spezifisch, user soll scalar("SELECT ...") nutzen
                        return result;
                    }
                }
                catch
                {
                    int affected = cmd.ExecuteNonQuery();
                    var result = new ArrayList();
                    result["rows"] = new ArrayList();
                    result["affected"] = affected;
                    result["last_id"] = null;
                    return result;
                }
            }
        }

        private static int Exec(int handle, string sql)
        {
            var conn = GetConn(handle);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandType = CommandType.Text;
                return cmd.ExecuteNonQuery();
            }
        }

        private static object Scalar(int handle, string sql)
        {
            var conn = GetConn(handle);
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.CommandType = CommandType.Text;

                var v = cmd.ExecuteScalar();
                if (v == null || v is DBNull) return null;
                return v;
            }
        }

        // params: ArrayList von ArrayList { name, value, dbtype? }
        private static void ApplyParams(DbCommand cmd, ArrayList parameters)
        {
            if (parameters == null) return;

            foreach (var obj in parameters.Values)
            {
                if (obj == null) continue;
                if (obj is not ArrayList p)
                    throw new ScriptStackException("db_query_p: params muss ArrayList von ArrayList sein.");

                string name = p.ContainsKey("name") ? (p["name"]?.ToString() ?? "") : "";
                if (string.IsNullOrWhiteSpace(name))
                    throw new ScriptStackException("db_query_p: Parameter ohne name.");

                object value = p.ContainsKey("value") ? p["value"] : null;
                if (value == null) value = DBNull.Value;

                var prm = cmd.CreateParameter();
                prm.ParameterName = name;
                prm.Value = value ?? DBNull.Value;

                if (p.ContainsKey("dbtype") && p["dbtype"] != null)
                {
                    var t = p["dbtype"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(t))
                        prm.DbType = ParseDbType(t);
                }

                cmd.Parameters.Add(prm);
            }
        }

        private static DbType ParseDbType(string t)
        {
            try
            {
                return (DbType)Enum.Parse(typeof(DbType), t, ignoreCase: true);
            }
            catch
            {
                throw new ScriptStackException("Unbekannter DbType: " + t);
            }
        }

        private static ArrayList ReadRows(DbDataReader reader)
        {
            var rows = new ArrayList();
            int rowIndex = 0;

            while (reader.Read())
            {
                var row = new ArrayList();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string col = reader.GetName(i);
                    object val = reader.GetValue(i);

                    row[col] = (val == null || val is DBNull) ? null : val;
                }

                rows[rowIndex++] = row;
            }

            return rows;
        }
    }
}
