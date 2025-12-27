```Javascript
function main() {

  // HTTP GET Request
	var req = http_request();
	req.Method = "GET";
	req.Url = "https://www.orf.at";
	req.ContentType = "text/html; charset=utf-8";
	req.Body = "";
	req.Headers = { "Authorization": "Bearer abcdef" };

	var resp = http_send(req);
	print(resp.Status);
	print(resp.Body);

  // Download file
	var req = http_request();
	req.Method = "GET";
	req.Url = "https://example.com/image.jpeg";
	req.ReadResponseAsBytes = true;

	var resp = http_send(req);

	var f = fopen("C:\\image.jpeg", "wb");
	fwritebytes(f, resp.Bytes);
	fclose(f);
	
}
```
