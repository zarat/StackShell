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

	var h = df.LoadCsv("test.csv", ",", 1);
	print(df.Info(h));
	print(df.Head(h, 10));

	var cols = df.Columns(h);
	print(cols);

	var price0 = df.Get(h, 3, "HistoricalPrice");
	print(price0);

	print(df.Row(h, 0));                 // ganze erste Zeile

	var h2 = df.Select(h, ["HistoricalPrice","CurrentPrice"]); // nur 2 Spalten (neuer DF)
	print(df.Head(h2, 10));

	var h3 = df.FilterEq(h, "HistoricalPrice", 320884); // Filter
	print(df.Info(h3));

	print(df.Describe(h, ""));           // describe für alle numerischen
	print(df.Describe(h, "HistoricalPrice"));         // describe nur für "id"

	// sort
	var hs = df.Sort(h, "HistoricalPrice", 1);
	print(df.Head(hs, 10));

	// filter >
	var hf = df.FilterGt(h, "HistoricalPrice", 300000);
	print(df.Head(hf, 10));

	// value counts
	var vc = df.ValueCounts(h, "HistoricalPrice", 0); // 0 = counts absteigend
	print(df.Head(vc, 50));

	h2 = df.SortBy(h, ["HistoricalPrice","CurrentPrice"], [1,0]);  // id asc, name desc
	print(df.Head(h2, 10));

	var ge = df.FilterGE(h, "HistoricalPrice", 300000);
	print(df.Head(ge, 10));

	var le = df.FilterLE(h, "HistoricalPrice", 30000);
	print(df.Head(le, 10));
	
	// alle HistoricalPrice die mit "2" anfangen:
	var b = df.FilterLike(h, "HistoricalPrice", "2%", 1);
	print(df.Head(b, 10));

	// Unterstrich: genau ein Zeichen
	// z.B. "P!nk" würde "P_nk" matchen
	var p = df.FilterLike(h, "HistoricalPrice", "3_0884", 1);
	print(df.Head(p, 10));

	df.Set(h, 3, "HistoricalPrice", 123); // ERROR!!!!!
	
	df.SetCsvFmt(h, "float=0.########;double=0.################;culture=invariant;quoteAll=0");
	// oder
	// dfSetCsvFmt(h, "float=0.00;double=0.00;culture=invariant;quoteAll=1");
	
	df.SaveCsv(h, "out.csv", ";", 1);

	df.ResetCsvFmt(h); // reset format
	
	df.Close(h);

}
```
