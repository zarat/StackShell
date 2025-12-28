```Javascript
function main() {
	
	var wb = xlsx_new(); //xlsx_load("C:\\Users\\manuel.zarat\\Desktop\\Gold.xlsx");	

	var sheet1 = xlsx_add_ws(wb, "User");
	var sheet2 = xlsx_add_ws(wb, "Gruppen");
	
	xlsx_remove_ws(wb, sheet1.Name);

	print(xlsx_rows(sheet2));
	print(xlsx_cols(sheet2));
	
	xlsx_set(sheet2, 1, 1, "Hello");
	xlsx_set(sheet2, 2, 1, "World");
	
	wb.SaveAs("HelloWorld.xlsx");
	
	//xlsx_close(wb);

}
```
