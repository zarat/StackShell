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

# Pong
```Javascript
var running;

var W;
var H;

var paddleW;
var paddleH;
var leftX;
var rightX;
var leftY;
var rightY;

var ballSize;
var ballX;
var ballY;
var vx;
var vy;

var scoreL;
var scoreR;

var keyW;
var keyS;
var keyUp;
var keyDown;

function clamp(v, lo, hi) {
    if (v < lo) return lo;
    if (v > hi) return hi;
    return v;
}

function resetBall(dir) {
    ballX = (W / 2) - (ballSize / 2);
    ballY = (H / 2) - (ballSize / 2);

    // dir: -1 nach links, +1 nach rechts
    vx = 5 * dir;
    vy = 2;
}

function setup() {
    W = 640;
    H = 480;

    paddleW = 12;
    paddleH = 90;

    leftX = 20;
    rightX = W - 20 - paddleW;

    leftY = (H / 2) - (paddleH / 2);
    rightY = (H / 2) - (paddleH / 2);

    ballSize = 12;

    scoreL = 0;
    scoreR = 0;

    keyW = false;
    keyS = false;
    keyUp = false;
    keyDown = false;

    resetBall(1);
}

function handleInput() {
    while (true) {
        var k = Gfx_PollKey();
        if (k == null) break;

        var type = k[0];
        var code = k[1];

        // WinForms Keys Codes (meistens):
        // W=87, S=83, Up=38, Down=40, Esc=27
        if (type == "down") {
            if (code == 87) keyW = true;
            if (code == 83) keyS = true;
            if (code == 38) keyUp = true;
            if (code == 40) keyDown = true;
            if (code == 27) running = false;
        }

        if (type == "up") {
            if (code == 87) keyW = false;
            if (code == 83) keyS = false;
            if (code == 38) keyUp = false;
            if (code == 40) keyDown = false;
        }
    }
}

function paddleHitY(paddleY) {
    // Ball-Mitte relativ zur Paddle-Mitte: daraus 5 Zonen => vy
    var ballMid = ballY + (ballSize / 2);
    var padMid = paddleY + (paddleH / 2);
    var d = ballMid - padMid;

    if (d < -30) return -5;
    if (d < -12) return -3;
    if (d < 12)  return 0;
    if (d < 30)  return 3;
    return 5;
}

function updateGame() {
    var paddleSpeed = 7;

    // paddles bewegen
    if (keyW) leftY -= paddleSpeed;
    if (keyS) leftY += paddleSpeed;
    if (keyUp) rightY -= paddleSpeed;
    if (keyDown) rightY += paddleSpeed;

    leftY = clamp(leftY, 0, H - paddleH);
    rightY = clamp(rightY, 0, H - paddleH);

    // ball bewegen
    ballX += vx;
    ballY += vy;

    // bounce top/bottom
    if (ballY < 0) {
        ballY = 0;
        vy = -vy;
    }
    if (ballY + ballSize > H) {
        ballY = H - ballSize;
        vy = -vy;
    }

    // collision left paddle
    if (vx < 0) {
        if (ballX <= leftX + paddleW &&
            ballX + ballSize >= leftX &&
            ballY + ballSize >= leftY &&
            ballY <= leftY + paddleH)
        {
            ballX = leftX + paddleW; // raus schieben
            vx = -vx;
            vy = paddleHitY(leftY);

            // nie komplett "flach" ewig geradeaus:
            if (vy == 0) vy = 2;
        }
    }

    // collision right paddle
    if (vx > 0) {
        if (ballX + ballSize >= rightX &&
            ballX <= rightX + paddleW &&
            ballY + ballSize >= rightY &&
            ballY <= rightY + paddleH)
        {
            ballX = rightX - ballSize; // raus schieben
            vx = -vx;
            vy = paddleHitY(rightY);

            if (vy == 0) vy = -2;
        }
    }

    // score
    if (ballX + ballSize < 0) {
        scoreR++;
        resetBall(1);
    }
    if (ballX > W) {
        scoreL++;
        resetBall(-1);
    }
}

function render() {
    // Frame neu zeichnen
    Gfx_Clear();

    // Hintergrund weiß
    Gfx_SetColour(255, 255, 255);
    Gfx_FillRectangle(0, 0, W, H);

    // Mittellinie (gestrichelt)
    Gfx_SetColour(200, 200, 200);
    var y = 0;
    while (y < H) {
        Gfx_FillRectangle((W/2) - 2, y, 4, 12);
        y += 22;
    }

    // Paddles + Ball schwarz
    Gfx_SetColour(0, 0, 0);
    Gfx_FillRectangle(leftX, leftY, paddleW, paddleH);
    Gfx_FillRectangle(rightX, rightY, paddleW, paddleH);
    Gfx_FillEllipse(ballX, ballY, ballSize, ballSize);

    // Score
    Gfx_SetColour(0, 0, 0);
    Gfx_DrawString((W/2) - 40, 20, "" + scoreL + " : " + scoreR);
    Gfx_DrawString(20, H - 30, "W/S  |  Up/Down  |  ESC quit");
}

function main() {
    running = true;

    Gfx_Initialise(640, 480);
    setup();

    while (running) {
        if (Gfx_PollUserClosed()) break;
        if (!Gfx_IsOpen()) break;

        handleInput();
        updateGame();
        render();

        yield;
    }

    Gfx_Shutdown();
}
```
