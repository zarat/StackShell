```Javascript
function XMLTest() {
	
	var xml = xml_parse("<root><items><item>Hello</item><item>World</item></items></root>");
	var elem = xml_select_all(xml, "/root/items/item");
	print(xml_value(elem[0]));
	
	
	
	var doc = xml_parse("<root><item id='a'>Hello</item><item id='b'>World</item></root>");
	var n = xml_select(doc, "/root/item[@id='a']");
	print(xml_value(n)); // Hello

	if(xml_has(doc, "/root/item[@id='b']"))
		print(xml_value(xml_select(doc, "/root/item[@id='b']"))); // World

	// erstes Element der Liste nehmen (ohne zu iterieren)
	var all = xml_select_all(doc, "/root/item");
	print(xml_value(all[0])); // Hello 

	// setzen
	print(xml_set(doc, "/root/item[@id='b']", "Changed"));
	
	
	
	// Iterator
	doc = xml_parse("<root><item id='a'>Hello</item><item id='b'>World</item></root>");
	var items = xml_select_all(doc, "/root/item");
	
	var it;
	foreach (it in items) {
		print(xml_attr(it, "id") + ": " + xml_value(it));
	}

	// oder mit key:
	var i;
	foreach (i, it in items) {
		print("" + i + ": " + xml_value(it));
	}

	
	doc = xml_parse("<root><item id='a' lang='de'>Hello</item><item id='b'>World</item></root>");

	// Attribut von einem selektierten Element lesen
	var first = xml_select(doc, "/root/item[1]");
	print(xml_attr(first, "id"));    // a
	print(xml_attr(first, "lang"));  // de

	// Attribut direkt per XPath aufs Element + xml_attr
	var second = xml_select(doc, "/root/item[@id='b']");
	print(xml_attr(second, "id"));   // b
	print(xml_attr(second, "lang")); // null (nicht vorhanden)

	// Alternative: Attribut per XPath selektieren und xml_value nutzen
	var idAttr = xml_select(doc, "/root/item[1]/@id");
	print(xml_value(idAttr)); // a	

}
```
