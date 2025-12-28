```Javascript
function main() {
	
	var wb = xlsx_new(); //xlsx_load("C:\\Users\\manuel.zarat\\Desktop\\Gold.xlsx");	

	var sheet1 = xlsx_add_ws(wb, "User");
	var sheet2 = xlsx_add_ws(wb, "Gruppen");
	
	xlsx_remove_ws(wb, sheet1.Name);

	print(xlsx_rows(sheet));
	print(xlsx_cols(sheet));
	
	xlsx_set(sheet1, 1, 1, "Hello");
	xlsx_set(sheet1, 2, 1, "World");
	
	wb.SaveAs("HelloWorld.xlsx");
	
	//xlsx_close(wb);

}
```
