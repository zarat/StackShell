# ScriptStack.YAML

Einfaches Plugin zum Arbeiten mit YAML Daten

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

    // -------- Datei lesen --------
    var f = fopen("test.yaml", "r");
    var data = fread(f);
    fclose(f);

    print("== RAW YAML ==\n" + data + "\n");

    // -------- parse --------
    var doc = yaml_parse(data);

    // -------- get / has --------
    print("== yaml_get / yaml_has ==");
    print("company exists? " + yaml_has(doc, "company"));
    print("company = " + yaml_get(doc, "company"));

    print("domain[0] exists? " + yaml_has(doc, "domain[0]"));
    print("domain[0] = " + yaml_get(doc, "domain[0]"));

    // nested example (aus deinem sample)
    print("tutorial[0].yaml.name = " + yaml_get(doc, "tutorial[0].yaml.name"));
    print("");

    // -------- count --------
    print("== yaml_count ==");
    print("root count (mapping keys) = " + yaml_count(doc));
    print("domain count (sequence) = " + yaml_count(yaml_get(doc, "domain")));
    print("");

    // -------- keys --------
    print("== yaml_keys (root mapping) ==");
    var k = yaml_keys(doc);
    print(k); // ArrayList-Stringdarstellung von deiner Runtime
    print("");

    // -------- type checks --------
    print("== yaml_is_map / yaml_is_seq ==");
    print("doc is map? " + yaml_is_map(doc));
    print("doc is seq? " + yaml_is_seq(doc));
    print("domain is seq? " + yaml_is_seq(yaml_get(doc, "domain")));
    print("");

    // -------- iter root mapping --------
    print("== yaml_iter over ROOT (Mapping) ==");
    var it = yaml_iter(doc);
    while (yaml_next(it)) {
        // mapping: index = -1, key != null
        print("key=" + yaml_key(it) + " idx=" + yaml_index(it) + " val=" + yaml_value(it));
    }
    print("");

    // -------- iter sequence (domain) --------
    print("== yaml_iter over domain (Sequence) ==");
    it = yaml_iter(yaml_get(doc, "domain"));
    while (yaml_next(it)) {
        // sequence: key = null, index >= 0
        print("idx=" + yaml_index(it) + " val=" + yaml_value(it));
    }
    print("");

    // -------- set: einfache Werte --------
    print("== yaml_set ==");
    yaml_set(doc, "company", "NEWCO");
    yaml_set(doc, "published", false);
    yaml_set(doc, "domain[1]", "security");
    print("company now = " + yaml_get(doc, "company"));
    print("published now = " + yaml_get(doc, "published"));
    print("domain[1] now = " + yaml_get(doc, "domain[1]"));
    print("");

    // -------- set: ArrayList -> YAML Sequence --------
    // [1,2,3] als ScriptStack ArrayList (bei dir ist [] literal schon ArrayList)
    yaml_set(doc, "numbers", [1, 2, 3]);
    print("numbers count = " + yaml_count(yaml_get(doc, "numbers")));
    it = yaml_iter(yaml_get(doc, "numbers"));
    while (yaml_next(it)) {
        print("numbers[" + yaml_index(it) + "]=" + yaml_value(it));
    }
    print("");

    // -------- yaml_node / yaml_string --------
    print("== yaml_node / yaml_string ==");
    var node = yaml_node([ "a", "b", "c" ]);     // macht Sequence
    print(yaml_string(node));

    // -------- final dump --------
    print("== FINAL YAML ==\n" + yaml_string(doc));

    it = yaml_iter(doc);
	while (yaml_next(it)) {
	  var idx = yaml_index(it);
	  if (idx >= 0)
		print("[" + idx + "] => " + yaml_value(it));
	  else
		print(yaml_key(it) + " => " + yaml_value(it));
	}

}
```
