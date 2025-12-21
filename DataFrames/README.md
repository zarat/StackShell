```CSV
"id";"name";"email"
1;"Friedrich Merz";"friedrich.merz@3rdreich.org"
2;"Jeffrey Epstein";"jeffrey.epstein@trump.tower"
3;"Bill Gates";"bill.gates@wef.org"
4;"P!nk";"p!nk@sony.com"
```

```Javascript
function main() {

	var h = dfLoadCsv("test.csv", ";", 1);
	print(dfInfo(h));
	print(dfHead(h, 10));

	var cols = dfColumns(h);
	print(cols);

	var price0 = dfGet(h, 3, "name");
	print(price0);

	print(dfRow(h, 0));                 // ganze erste Zeile

	var h2 = dfSelect(h, ["id","name"]); // nur 2 Spalten (neuer DF)
	print(dfHead(h2, 10));

	var h3 = dfFilterEq(h, "name", "Jango"); // Filter
	print(dfInfo(h3));

	print(dfDescribe(h, ""));           // describe für alle numerischen
	print(dfDescribe(h, "id"));         // describe nur für "id"

	// sort
	var hs = dfSort(h, "name", 1);
	print(dfHead(hs, 10));

	// filter >
	var hf = dfFilterGt(h, "id", 2);
	print(dfHead(hf, 10));

	// value counts
	var vc = dfValueCounts(h, "email", 0); // 0 = counts absteigend
	print(dfHead(vc, 50));

	h2 = dfSortBy(h, ["id","name"], [1,0]);  // id asc, name desc
	print(dfHead(h2, 10));

	var ge = dfFilterGE(h, "id", 3);
	print(dfHead(ge, 10));

	var le = dfFilterLE(h, "id", 2);
	print(dfHead(le, 10));
	
	// alle Emails von sony:
	var sony = dfFilterLike(h, "email", "%@sony.com", 1);
	print(dfHead(sony, 10));

	// alle Namen die mit "B" anfangen:
	var b = dfFilterLike(h, "name", "B%", 1);
	print(dfHead(b, 10));

	// Unterstrich: genau ein Zeichen
	// z.B. "P!nk" würde "P_nk" matchen
	var p = dfFilterLike(h, "name", "P_nk", 1);
	print(dfHead(p, 10));

	dfSet(h, 3, "P!nk", "Jango");
	dfSaveCsv(h, "out.csv", ",", 1);

	dfClose(h);

}
```
