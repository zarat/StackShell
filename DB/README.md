Die Provider-DLLs inkl. Abhängigkeiten müssen im Plugin-Ordner liegen.

- MySQL
- MS-SQL
- PostgreSQL

```Javascript
function main() {

	// MySQL
	db_register("MySqlConnector", "MySqlConnector.MySqlConnectorFactory, MySqlConnector");
	var conn = db_open("MySqlConnector", "Server=127.0.0.1;User ID=root;Password=;Database=test");
	var r = db_query(conn, "select * FROM test;");
	
	// MSSql
	//db_register("Microsoft.Data.SqlClient", "Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient");
	//var conn = db_open( "Microsoft.Data.SqlClient", "Server=DB-SRV\\DB-INSTANCE;Database=test;User ID=admin; Password=;TrustServerCertificate=True;Connection Timeout=30");	
	//var r = db_query(conn, "select * FROM test;");
	
	// PGSql
	//db_register("Npgsql", "Npgsql.NpgsqlFactory, Npgsql");
	//var conn = db_open("Npgsql", "Host=127.0.0.1;Port=5432;Username=postgres;Password=;Database=postgres");
	//var r = db_query(conn, "select rolname, rolsuper, rolcreatedb, rolcreaterole, rolcanlogin from pg_roles order by rolname;");
	
	var row;
	foreach(row in r.rows)
		println(row);
	db_close(conn);
	
}
```
