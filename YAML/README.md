Example YAML file
```YAML
---
# A sample yaml file
company: spacelift
domain:
 - devops
 - devsecops
tutorial:
  - yaml:
      name: "YAML Ain't Markup Language"
      type: awesome
      born: 2001
  - json:
      name: JavaScript Object Notation
      type: great
      born: 2001
  - xml:
      name: Extensible Markup Language
      type: good
      born: 1996
author: omkarbirade
published: true
```

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

	it = yaml_iter(doc);
	while (yaml_next(it)) {
		var idx = yaml_index(it);
		if (idx >= 0)
			print("" + idx + " => " + yaml_value(it));
		else
			print("" + yaml_key(it) + " => " + yaml_value(it));
	}

	var node = yaml_get(doc, "");
	it = yaml_iter(node);
	while (yaml_next(it)) {
		if (yaml_is_seq(node)) {
			print("" + yaml_index(it) + " => " + yaml_value(it));
		} else {
			print("" + yaml_key(it) + " => " + yaml_value(it));
		}
	}
	
}
```
