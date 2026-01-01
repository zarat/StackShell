Plugin zum Erstellen von Windows Forms mit Steuerelementen

```Javascript
var running;
var formOpen;
var lblText;
var txtInput;
var btnAdd;
var btnClear;
var lstItems;
var chkUpper;
var lblInfo;
var id, type, keyCode, e;

function setupUi() {
    // Main-Form (w,h,title)
    if (!ui.Initialise(640, 420, "UI Demo")) {
        running = false;
        return;
    }

    // Controls (type, parentId, x,y,w,h, text)
    lblText  = ui.CreateControl("label", 0, 12, 14, 60, 22, "Text:");
    txtInput = ui.CreateControl("textbox", 0, 80, 12, 360, 26, "");
    btnAdd   = ui.CreateControl("button", 0, 452, 12, 80, 26, "Add");
    btnClear = ui.CreateControl("button", 0, 540, 12, 80, 26, "Clear");

    chkUpper = ui.CreateControl("checkbox", 0, 12, 46, 200, 22, "UPPERCASE");
    lblInfo  = ui.CreateControl("label", 0, 230, 46, 390, 22, "Enter = Add, ESC = Quit");

    lstItems = ui.CreateControl("listbox", 0, 12, 76, 610, 300, "");

    // ui.SetBackColor(lstItems, 250, 250, 250);
}

function addCurrentText() {
    var s = ui.GetText(txtInput);
    if (s == null) s = "";

    if (s == "") return;

    if (ui.GetChecked(chkUpper)) {
        s = s.toUpper();
    }

    ui.AddItem(lstItems, s);
    ui.SetText(txtInput, "");
}

function clearList() {
    ui.ClearItems(lstItems);
}

function handleEvents() {
    while (true) {
        e = ui.PollEvent();
        if (e == null) break;

        type = e[0];
        id   = e[1];

        // ---- Window close ----
        if (type == "formclosing") {
            // i0 == 1 => user closing
            running = false;
        }

        // ---- Button clicks ----
        if (type == "click") {
            if (id == btnAdd) {
                addCurrentText();
            }
            if (id == btnClear) {
                clearList();
            }
        }

        // ---- TextBox Enter ----
        if (type == "keydown" && id == txtInput) {
            keyCode = e[2];    // i0
            // var mods = e[3];    // i1 bitmask shift/ctrl/alt

            // Keys.Enter = 13 (wie in deinem Pong Beispiel)
            if (keyCode == 13) {
                addCurrentText();
            }

            // Keys.Escape = 27
            if (keyCode == 27) {
                running = false;
            }
        }

        // ---- Global ESC (auch wenn Fokus woanders ist) ----
        if (type == "keydown") {
            var keyCode2 = e[2];
            if (keyCode2 == 27) running = false;
        }

        // ---- Checkbox changed ----
        if (type == "checkedchanged" && id == chkUpper) {
            var isOn = (e[2] != 0); // i0
            if (isOn) {
                ui.SetText(lblInfo, "UPPERCASE ON");
            } else {
                ui.SetText(lblInfo, "UPPERCASE OFF");
            }
        }

        // ---- Double click on list => remove all (Demo) ----
        if (type == "dblclick" && id == lstItems) {
            clearList();
        }
    }
}

function main() {
    running = true;

    setupUi();
    if (!running) return;

    while (running) {
        if (ui.PollUserClosed()) break;
        if (!ui.IsOpen()) break;

        handleEvents();
        yield;
    }

    ui.Shutdown();
}
```
