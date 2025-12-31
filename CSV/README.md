```CSV
Id,Size,HistoricalPrice,CurrentPrice
1,4174,302283,350235
2,4507,296769,175939
3,1860,137065,592141
4,2294,323165,586157
5,2130,199299,302906
6,2095,111534,168047
7,4772,140397,438249
8,4092,357750,225766
9,2638,453531,558923
10,3169,363160,565192
11,1466,155591,194262
12,2238,320884,537261
13,1330,123247,435920
14,2482,124300,311152
15,3135,182798,278376
16,4444,109268,287848
17,4171,448951,242787
18,3919,374329,277948
19,4735,294776,205016
20,1130,317851,552690
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

	csv.set(csv, "0.HistoricalPrice", 111);

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
# CSVPath
CsvPath akzeptiert (0-basiert):
- 3 oder [3] → ganze Zeile
- 3.Name oder [3].Name → Zelle über Headername
- 3[2] oder [3][2] → Zelle über Spaltenindex
