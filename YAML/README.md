```Javascript
function main() {

	var f = fopen("test.yaml", "r");
	var data = fread(f);
	fclose(f);

	var doc = yaml_parse(data);	

	var it = yaml_iter(doc);
	while (yaml_next(it)) {
		print("" + yaml_key(it) + " => " + yaml_value(it));
	}

	yaml_set(doc, "company", [1, 2, 3]);
	it = yaml_iter(yaml_get(doc, "company"));
	while (yaml_next(it)) {
		print("" + yaml_index(it) + " => " + yaml_value(it));
	}


	it = yaml_iter(yaml_get(doc, "tutorial"));
	while (yaml_next(it)) {
		
		//print("" + yaml_index(it) + " => " + yaml_value(it));
		var tit = yaml_iter(yaml_value(it));
		
		while(yaml_next(tit)) {
			print("" + yaml_key(tit) + " => " + yaml_value(tit));
		}
		
	}
	
}
```
