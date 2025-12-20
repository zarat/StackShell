```Javascript
function main() {

    var f = fopen("test.xml", "r");
    var data = fread(f);
    fclose(f);

    var doc = xml_parse(data);

    print("== xml_has ==");
    print("has /CATALOG/CDS/CD => " + xml_has(doc, "/CATALOG/CDS/CD") + "\n\n");

    print("== xml_select (Element) ==");
    var companyNode = xml_select(doc, "/CATALOG/CDS/CD[1]/COMPANY");
    print("is elem: " + xml_is_elem(companyNode) + "\n");
    print("value: " + xml_value(companyNode) + "\n\n");

    print("== xml_select (Attribute) ==");
    var idAttr = xml_select(doc, "/CATALOG/CDS/CD[1]/@id");
    print("is attr: " + xml_is_attr(idAttr) + "\n");
    print("attr value: " + xml_value(idAttr) + "\n\n");

    print("== xml_attr (Attribute by name) ==");
    var cd1 = xml_select(doc, "/CATALOG/CDS/CD[1]");
    print("cd1 id via xml_attr: " + xml_attr(cd1, "id") + "\n\n");

    print("== xml_set (Element value) ==");
    xml_set(doc, "/CATALOG/CDS/CD[1]/COMPANY", "NEW_LABEL");
    print("company after set: " + xml_value(xml_select(doc, "/CATALOG/CDS/CD[1]/COMPANY")) + "\n\n");

    print("== xml_set (Attribute value) ==");
    xml_set(doc, "/CATALOG/CDS/CD[1]/@id", "999");
    print("id after set: " + xml_value(xml_select(doc, "/CATALOG/CDS/CD[1]/@id")) + "\n\n");

    print("== xml_select_all (alle CD Nodes) ==");
    var cds = xml_select_all(doc, "/CATALOG/CDS/CD");
    print("select_all returned: " + cds + "\n\n"); // je nach ScriptStack wird das evtl. als ArrayList angezeigt

    print("== xml_iter (children von /CATALOG/CDS/CD[1]) ==");
    var it = xml_iter(cd1);
    while (xml_next(it)) {
        print("" + xml_index(it) + " name=" + xml_name(it) + " val=" + xml_value_it(it) + "\n");
    }
    print("\n");

    print("== xml_iter_attr (Attribute von CD[1]) ==");
    it = xml_iter_attr(cd1);
    while (xml_next(it)) {
        print(xml_name(it) + " = " + xml_value_it(it) + "\n");
    }
    print("\n");

    print("== xml_iter_all (Descendants unter /CATALOG) ==");
    var catalog = xml_select(doc, "/CATALOG");
    it = xml_iter_all(catalog);
    while (xml_next(it)) {
        // hier sind es nur Elemente (Descendants())
        print(""+ xml_index(it) + " <" + xml_name(it) + ">\n");
    }

    print("\n== Final XML (pretty)==");
    print(xml_string(doc));

}
```
