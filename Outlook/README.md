```Javascript
function main() {
	
	var folders = json_parse(outlook_list_folders2("max.mustermann@github.com", ""));
	var folder;
	foreach(folder in folders) {		
		println(folder["Name"]);
	}

	var emails = json_parse(outlook_list_mails("max.mustermann@github.com", "Posteingang", 0, 10, false, 1000));
	var mail;
	foreach(mail in emails) {
		println(mail["Subject"]);
	}

	var calendar = json_parse(outlook_search_calendar2("max.mustermann@github.com", "Kalender", "2025-10-28T14:35:12", "2025-12-28T23:35:12", 10));
	var event;
	foreach(event in calendar) {
		println(event["Subject"]);
	}

	var found = json_parse(outlook_search_mails2("max.mustermann@github.com", "", "", "Github", "", "", false, "2024-10-28T14:35:12", "2025-12-28T23:35:12", 100, false, 1000));
	var m; 
	foreach(m in found) {
		println(m["Subject"]);
	}
	
}
```
