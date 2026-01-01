Die ButtonsMask ist eine Bitmask "welche Buttons sind gerade gedrückt":
- 1 = Left gedrückt
- 2 = Right gedrückt
- 4 = Middle gedrückt (Mausrad-Klick)
- 8 = X1 gedrückt
- 16 = X2 gedrückt

```Javascript
var running;

function main() {
	
	running = true;
	
	run draw();
	
	while(running) {
		
		if (Gfx_PollUserClosed())
            return;
		
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
		
		for (var p = 0; p < 2000; p++);
		
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
