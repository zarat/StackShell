```Javascript
function main() {
	
	var wb = xlsx_new(); //xlsx_load("C:\\Users\\manuel.zarat\\Desktop\\Gold.xlsx");	

	var sheet1 = xlsx_add_ws(wb, "User");
	var sheet2 = xlsx_add_ws(wb, "Gruppen");
	
	//xlsx_remove_ws(wb, sheet1.Name);
	
	var sheets = xlsx_list_ws(wb);
	var sheetname;
	foreach(sheetname in sheets)
		println(sheetname);
	
	xlsx_set(sheet1, 1, 1, 1);
	xlsx_set(sheet1, 2, 1, 2);
	xlsx_set_formula(sheet1, 3, 1, "=SUM(A1:A2)");
	print(xlsx_get_formula(sheet1, 3, 1));
	
	//xlsx_close(wb);
	wb.SaveAs("HelloWorld.xlsx");

}
```
