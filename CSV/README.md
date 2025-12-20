```CSV
"id";"name";"email"
1;"Friedrich Merz";"friedrich.merz@3rdreich.org"
2;"Jeffrey Epstein";"jeffrey.epstein@trump.tower"
3;"Bill Gates";"bill.gates@wef.org"
```

```Javascript
function main() {

	var f = fopen("test.csv", "r");
	var data = fread(f);
	fclose(f);
	
	var csv = csv_parse(data);
	
	print("Rows: " + csv_row_count(csv));
	print("Columns: " + csv_col_count(csv));	
	
	var headers = csv_headers(csv);
	print("Headers: " + csv_headers(csv));
	
	var it = csv_iter(csv);
	while(csv_next(it)) { 
	
		var row = csv_value(it);
		var i = 0;
		
		while (headers[i] != null) {
			
			var h = headers[i];
			var v = row[h];  
            
			print(h + " = " + v);
			
			i++;
			
		}
		
	}
	
}
```
