```Javascript
var running;

function main() {
	
	running = true;
	
	run draw();
	
	while(running) {
		
		if (Gfx_PollUserClosed())
            return;
		
		var k = Gfx_PollKey();
		if(k != null) {
			// [type,keyCode,keyChar,modifiers]
			println("key=" + k[0] + " code=" + k[1] + " char=" + k[2]);
		}
		
		var m = Gfx_PollMouse();
		if (m != null) {
			if(m[0] == "wheel") 
			{
				var d = m[5];
				if (d > 0) println("scroll up " + d);
				else if (d < 0) println("scroll down " + d);
			}
			else 
			{
				// [type,x,y,button,clicks,wheelDelta,modifiers]
				println(m[0] + " at " + m[1] + "," + m[2] + " btn=" + m[3]);
			}
		}

		var s = Gfx_GetMouseState();
		if(s != null) {
			// [x,y,buttonsMask,wheelAccum,modifiers]
			println("state x=" + s[0] + " y=" + s[1] + " buttons=" + s[2]);
		}
		
		yield;
		
	}
	
}

function draw() {
	
	Gfx_Initialise(640,480);
	Gfx_DrawString(20, 20, "StackShell GFX Demo");
	
    var intensity = 0;
    for (var index = 0; index < 400; index += 10) {

        Gfx_SetColour(255 - intensity, 0, intensity);
        intensity += 5;

        Gfx_DrawLine(0, index, 400 - index, 0);

        Gfx_SetColour(intensity, 255, 0);
        Gfx_FillEllipse(index, 200, index / 4, index / 4);

        Gfx_SetColour(0, 0, 0);
        Gfx_DrawEllipse(index, 200, index / 4, index / 4);
		
		//for (var p = 0; p < 500; p++);
		
		if(index > 380) {
			Gfx_Clear();
			index = 0;
			intensity = 0;
			Gfx_DrawString(20, 20, "StackShell GFX Demo");
		}

    }

    Gfx_Shutdown();	
	
}
```

Die ButtonsMask beim MouseState ist eine Bitmask "welche Buttons sind gerade gedrückt":
```Javascript
var s = Gfx_GetMouseState();
if (s != null) {
  var mask = s[2];
  if ((mask & 4) != 0) println("middle down");
}
```
- 1 = Left gedrückt
- 2 = Right gedrückt
- 4 = Middle gedrückt (Mausrad-Klick)
- 8 = X1 gedrückt
- 16 = X2 gedrückt
