```Javascript
function JSONTest() {

	var arr = ["Hello", "World"];
	
	// ScriptStack Array als JSON-String anzeigen
	print(json_string(arr)); // ["Hello", "World"]

	// JSON Node aus ScriptStack Array erstellen
	var node = json_node(arr);
	print(json_get(node, "[0]")); // Hello
	
	var obj = {"a": "Hello", "b": "World"};

	// ScriptStack Object als JSON-String anzeigen
	print(json_string(obj)); // {"a":"Hello", "b":"World"}
	
	// JSON Node aus ScriptStack Object erstellen
	print(json_get(json_node(obj), "b")); // World
	
	// for Schleife über ein Array
	var root = json_parse("[\"Hello\", \"World\"]");
	var n = json_count(root);
	for (var i = 0; i < n; i = i + 1) {
		print(json_get(root, "[" + i + "]"));
	}

	// for Schleife über ein Object
	root = json_parse("{ \"a\": \"Hello\", \"b\": \"World\" }");
	n = json_count(root);
	var keys = json_keys(root);
	for (i = 0; i < n; i = i + 1) {	
		print(json_get(root, keys[i]));
	}
	
	// foreach Schleife über ein Object
	root = json_parse("{\"a\": \"Hello\", \"b\": \"World\"}");
	keys = json_keys(root);
	var key;
	foreach (key in keys) {
		print("" + key + " => " + json_get(root, key));
	}

	// Iterator über Array
	root = json_parse("[\"Hello\", \"World\"]");
	var it = json_iter(root);
	while (json_next(it)) {
		print(json_value(it));
	}
	
	// Iterator über Object
	root = json_parse("{\"a\": \"Hello\", \"b\": \"World\"}");
	it = json_iter(root);
	while (json_next(it)) {
		print(json_value(it));
	}
	
	// Iterator über inneres Object
	root = json_parse("{\"items\": { \"a\":1, \"b\":2, \"c\":3 } }");
	it = json_iter(json_get(root, "items"));
	while(json_next(it)) {
		var ok = json_key(it);
		var ov = json_value(it);
		print("items[" + ok + "] => " + ov);
	}
	
	// Iterator über inneres Array
	root = json_parse("{\"items\": [1, 2, 3] }");
	it = json_iter(json_get(root, "items"));
	while(json_next(it)) {
		var ai = json_index(it);
		var av = json_value(it);
		print("items[" + ai + "]=" + av);
	}
	
}
```
