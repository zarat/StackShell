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

```Javascript
function main() 
{
	//single();
	playlist();
}

function single()
{
	
	var url = "https://www.youtube.com/watch?v=cQ5q0Y5T6x8";
	var info = yt_get_info(url);
	var source = yt_get_manifest(url);
	
	var video;
	foreach(video in source["muxed"])
	{
		if(video.container == "mp4") 
		{
			var webclient = clr("System.Net.WebClient", []);
			webclient.DownloadFile(video.url, "" + info.title + "." + video.container);
			break;
		}
	}
	
}

function playlist() 
{
	
	var pl = yt_get_playlist_info("https://www.youtube.com/playlist?list=PLYUHroQHA-xQFTB-GBPHvyY0cODiirqcX");
	
	std.print(pl.title + "\n");
	std.print(pl.author + "\n");
	std.print(pl.videoCount.ToString() + "\n");
	std.print(pl.videosReturned.ToString() + "\n");

	var videos = pl.videos;
	var video;
	foreach(video in videos)
	{
		std.print(video.title + " (" + video.url + ")\n");
	}
}
```
