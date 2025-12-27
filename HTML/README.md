```Javascript
function testHTML() {

	var req = http_request();
	req.Method = "GET";
	req.Url = "https://www.orf.at";

	var resp = http_send(req);

	// DOM parsen
    var doc = dom_parse(resp.Body);

    // Get elements
    var stories = dom_select(doc, ".ticker-story-text");
    var story;
    foreach(story in stories) {
        print("[out id] " + story.Attributes["id"]);
		print("[out text] " + story.InnerText.Trim());
		print("[out html] " + story.InnerHTML);
    }

    // Change elements
    var h1 = dom_first(doc, "nav");
    if(h1 != null) {
        node_set_text(h1, "Hallo aus ScriptStack!");
        node_set_attr(h1, "data-edited", "1");
    }

	// Remove elements
	var banner = dom_select(doc, "#sitebar-banner");
	print(typeof(banner));
	var b;
	foreach(b in banner) {
		node_remove(b);
	}

    // Inject HTML
    var body = dom_first(doc, "body");
    if(body != null) {
        node_append_html(body, "<div id='injected'>Injected!</div>");
    }

    // Resultierendes HTML ausgeben
    var out = dom_html(doc);
    print(out);
	
}
```
