```Javascript
function main()
{
	var video = yt_get_manifest("https://www.youtube.com/watch?v=cQ5q0Y5T6x8");
	var k, v;
	
	foreach(k, v in video["audioOnly"])
	{
		std.print("AUDIO " + v.container + "(" + v.bitrateBps + ") ==> " + v.url + "\n\n");
	}
	
	foreach(k, v in video["videoOnly"])
	{
		std.print("VIDEO " + v.container + "(" + v.bitrateBps + ") ==> " + v.url + "\n\n");
	}
}
```
