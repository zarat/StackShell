# ScriptStack.JSON

Einfaches Plugin zum Arbeiten mit JSON Daten

```JSON
{
  "company": "spacelift",
  "domain": ["devops", "devsecops"],
  "tutorial": [
    { "yaml": { "name": "YAML Ain't Markup Language", "type": "awesome", "born": 2001 } },
    { "json": { "name": "JavaScript Object Notation", "type": "great", "born": 2001 } }
  ],
  "author": "omkarbirade",
  "published": true
}
```

```Javascript
function main() {

    // -------- Datei lesen --------
    var f = fopen("test.json", "r");
    var data = fread(f);
    fclose(f);

    print("== RAW JSON ==\n" + data + "\n");

    // -------- parse --------
    var doc = json_parse(data);

    // -------- get / has --------
    print("== json_get / json_has ==");
    print("company exists? " + json_has(doc, "company"));
    print("company = " + json_get(doc, "company"));

    print("domain[0] exists? " + json_has(doc, "domain[0]"));
    print("domain[0] = " + json_get(doc, "domain[0]"));

    print("tutorial[0].yaml.name = " + json_get(doc, "tutorial[0].yaml.name"));
    print("");

    // -------- count --------
    print("== json_count ==");
    print("root count (object keys) = " + json_count(doc));
    print("domain count (array) = " + json_count(json_get(doc, "domain")));
    print("");

    // -------- keys --------
    print("== json_keys (root object) ==");
    var k = json_keys(doc);
    print(k);   // ArrayList Darstellung deiner Runtime
    print("");

    // -------- type checks --------
    print("== json_is_obj / json_is_arr ==");
    print("doc is obj? " + json_is_obj(doc));
    print("doc is arr? " + json_is_arr(doc));
    print("domain is arr? " + json_is_arr(json_get(doc, "domain")));
    print("");

    // -------- iter root object --------
    print("== json_iter over ROOT (Object) ==");
    var it = json_iter(doc);
    while (json_next(it)) {
        // object: index = -1, key != null
        print("key=" + json_key(it) + " idx=" + json_index(it) + " val=" + json_value(it));
    }
    print("");

    // -------- iter sequence (domain) --------
    print("== json_iter over domain (Array) ==");
    it = json_iter(json_get(doc, "domain"));
    while (json_next(it)) {
        // array: key = null, index >= 0
        print("idx=" + json_index(it) + " val=" + json_value(it));
    }
    print("");

    // -------- set: einfache Werte --------
    print("== json_set ==");
    json_set(doc, "company", "NEWCO");
    json_set(doc, "published", false);
    json_set(doc, "domain[1]", "security");

    print("company now = " + json_get(doc, "company"));
    print("published now = " + json_get(doc, "published"));
    print("domain[1] now = " + json_get(doc, "domain[1]"));
    print("");

    // -------- set: ArrayList -> JsonArray --------
    json_set(doc, "numbers", [1, 2, 3]);
    print("numbers count = " + json_count(json_get(doc, "numbers")));

    it = json_iter(json_get(doc, "numbers"));
    while (json_next(it)) {
        print("numbers[" + json_index(it) + "]=" + json_value(it));
    }
    print("");

    // -------- json_node / json_string --------
    print("== json_node / json_string ==");
    var node = json_node([ "a", "b", "c" ]);   // JsonArray
    print(json_string(node));
    print("");

    // -------- final dump --------
    print("== FINAL JSON ==");
    print(json_string(doc));
}
```
