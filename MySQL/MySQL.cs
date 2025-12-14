using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using MySqlConnector;

using ScriptStack.Runtime;

namespace ScriptStack
{
    public class MySQL : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;

        // sehr simples Handle-Management
        private static readonly object _lock = new object();
        private static int _nextHandle = 1;
        private static readonly Dictionary<int, MySqlConnection> _conns = new Dictionary<int, MySqlConnection>();

        public MySQL()
        {
            if (exportedRoutines != null) return;

            var r = new List<Routine>();

            r.Add(new Routine(typeof(int), "mysql_open", typeof(string),
                "Öffnet eine MySQL Connection. Param: connStr(string). Return: handle(int)."));

            r.Add(new Routine(typeof(int), "mysql_close", typeof(int),
                "Schließt eine Connection. Param: handle(int). Return: 1 wenn ok, sonst 0."));

            r.Add(new Routine(typeof(ArrayList), "mysql_query", typeof(int), typeof(string),
                "Führt SQL aus (Query oder NonQuery). Params: handle(int), sql(string). Return: ArrayList {rows, affected, last_id}."));

            r.Add(new Routine(typeof(ArrayList), "mysql_query_p", typeof(int), typeof(string), typeof(ArrayList),
                "Führt SQL mit Parametern aus. Params: handle(int), sql(string), params(ArrayList). "
                + "params = ArrayList von ArrayList: p['name'], p['value'], optional p['type']. Return: ArrayList {rows, affected, last_id}."));

            r.Add(new Routine(typeof(object), "mysql_scalar", typeof(int), typeof(string),
                "Führt SQL aus und gibt das erste Feld der ersten Zeile zurück (oder null)."));

            r.Add(new Routine(typeof(int), "mysql_exec", typeof(int), typeof(string),
                "Führt NonQuery aus und gibt affected rows zurück."));

            exportedRoutines = r.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string fn, List<object> p)
        {
            try
            {
                if (fn == "mysql_open") return Open((string)p[0]);
                if (fn == "mysql_close") return Close((int)p[0]) ? 1 : 0;

                if (fn == "mysql_query") return Query((int)p[0], (string)p[1], null);
                if (fn == "mysql_query_p") return Query((int)p[0], (string)p[1], (ArrayList)p[2]);

                if (fn == "mysql_scalar") return Scalar((int)p[0], (string)p[1]);
                if (fn == "mysql_exec") return Exec((int)p[0], (string)p[1]);
            }
            catch (Exception e)
            {
                throw new ScriptStackException(e.Message);
            }

            return null;
        }

        private static int Open(string connStr)
        {
            var conn = new MySqlConnection(connStr);
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
            MySqlConnection conn = null;

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

        private static MySqlConnection GetConn(int handle)
        {
            lock (_lock)
            {
                if (!_conns.TryGetValue(handle, out var conn) || conn == null)
                    throw new ScriptStackException("Ungültiger MySQL handle: " + handle);

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

                // Versuch: Reader; wenn keine Rows, fällt es auf NonQuery zurück
                // MySQL erlaubt i.d.R. auch NonQuery über ExecuteNonQuery, aber wir machen beides robust.
                try
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        var rows = ReadRows(reader);
                        var result = new ArrayList();
                        result["rows"] = rows;
                        result["affected"] = reader.RecordsAffected;
                        result["last_id"] = (long)cmd.LastInsertedId;
                        return result;
                    }
                }
                catch
                {
                    int affected = cmd.ExecuteNonQuery();
                    var result = new ArrayList();
                    result["rows"] = new ArrayList();
                    result["affected"] = affected;
                    result["last_id"] = (long)cmd.LastInsertedId;
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

        // params: ArrayList von ArrayList { name, value, type? }
        private static void ApplyParams(MySqlCommand cmd, ArrayList parameters)
        {
            if (parameters == null) return;

            foreach (var obj in parameters.Values)
            {
                if (obj == null) continue;
                if (obj is not ArrayList p)
                    throw new ScriptStackException("mysql_query_p: params muss ArrayList von ArrayList sein.");

                string name = p.ContainsKey("name") ? (p["name"]?.ToString() ?? "") : "";
                if (string.IsNullOrWhiteSpace(name))
                    throw new ScriptStackException("mysql_query_p: Parameter ohne name.");

                object value = p.ContainsKey("value") ? p["value"] : null;
                if (value == null) value = DBNull.Value;

                var prm = cmd.CreateParameter();
                prm.ParameterName = name;
                prm.Value = value ?? DBNull.Value;

                if (p.ContainsKey("type") && !(p["type"] == null))
                {
                    string t = p["type"]?.ToString() ?? "";
                    if (!string.IsNullOrWhiteSpace(t))
                        prm.MySqlDbType = ParseMySqlDbType(t);
                }

                cmd.Parameters.Add(prm);
            }
        }

        private static MySqlDbType ParseMySqlDbType(string t)
        {
            // akzeptiert z.B.: "Int32", "VarChar", "Text", "DateTime", ...
            // wir lassen Enum.Parse arbeiten, werfen bei Fehlern sauber
            try
            {
                return (MySqlDbType)Enum.Parse(typeof(MySqlDbType), t, ignoreCase: true);
            }
            catch
            {
                throw new ScriptStackException("Unbekannter MySqlDbType: " + t);
            }
        }

        private static ArrayList ReadRows(MySqlDataReader reader)
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

                    if (val == null || val is DBNull)
                        row[col] = null;
                    else
                        row[col] = val;
                }

                rows[rowIndex++] = row;
            }

            return rows;
        }
    }
}
