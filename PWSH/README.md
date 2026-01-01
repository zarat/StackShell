```Javascript
function main() 
{
	
	//
	// example 1
	//
	var res = ps.exec("Get-ChildItem -Path .");
	var k, v, entry;
	foreach(k, v in res) 
	{
		entry = ps.base(v); // Optional hole das BaseObject
		println("" + ps.prop(v, "Mode") + "\t" + ps.prop(v, "LastWriteTime") + "\t" + ps.prop(v, "Name"));
	}

	//
	// example 2
	//
	res = ps.exec("Get-Acl -Path .\\");
	var pso = res[0];

	// echtes CLR-Objekt (DirectorySecurity/FileSecurity)
	var acl = ps.base(pso);

	// $ACL.GetAccessRules(explicit, inherited, targettype)
	// Mit NTAccount
	var rules = ps.call(acl, "GetAccessRules", [ true, true, ps.type("System.Security.Principal.NTAccount") ]);
	// fallback mit SID wenn NTAccount nicht geht
	if (rules == null || rules.size == 0) {
		rules = ps.call(acl, "GetAccessRules", [ true, true, ps.type("System.Security.Principal.SecurityIdentifier") ]);
	}

	println("rules.size=" + rules.size);

	foreach(k, v in rules) {
		println(ps.prop(v, "IdentityReference"));
		println(ps.prop(v, "FileSystemRights"));
		println(ps.prop(v, "AccessControlType"));
		println("---");
	}

	var errs = ps.errors(res);
	if (errs.size > 0) {
		var i, e;
		foreach(i, e in errs) 
			println("ERR: " + e);
	}
	
	//
	// example 3
	//
	res = ps.exec("Get-Process");
	foreach(k, v in res) 
	{
		entry = ps.base(v);
		println("" + ps.prop(entry, "Id") + "\t" + ps.prop(entry, "ProcessName"));
	}
	
}
```
