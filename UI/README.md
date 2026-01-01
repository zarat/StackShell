Plugin zum Erstellen von Windows Forms mit Steuerelementen

```Javascript
var running;

var lblText;
var txtInput;
var btnAdd;
var btnRemove;
var btnClear;
var lstItems;

var chkUpper;
var chkAutoClear;
var lblInfo;

var e, id, type, key;
var s;
var idx;
var isOn;

function setupUi() {
    if (!ui.Initialise(640, 460, "UI Demo")) {
        running = false;
        return;
    }

    lblText   = ui.CreateControl("label",    0, 12, 14, 60, 22, "Text:");
    txtInput  = ui.CreateControl("textbox",  0, 80, 12, 300, 26, "");
    btnAdd    = ui.CreateControl("button",   0, 392, 12, 70, 26, "Add");
    btnRemove = ui.CreateControl("button",   0, 470, 12, 80, 26, "Remove");
    btnClear  = ui.CreateControl("button",   0, 558, 12, 70, 26, "Clear");

    chkUpper     = ui.CreateControl("checkbox", 0, 12, 46, 200, 22, "UPPERCASE");
    chkAutoClear = ui.CreateControl("checkbox", 0, 230, 46, 220, 22, "Auto-clear input");
    lblInfo      = ui.CreateControl("label",    0, 470, 46, 158, 22, "ESC=Quit");

    lstItems  = ui.CreateControl("listbox",  0, 12, 76, 616, 360, "");
}

function addCurrentText() {
    s = ui.GetText(txtInput);
    if (s == null) s = "";
    if (s == "") return;

    // wenn dein ScriptStack keine String-Methoden hat, diese Zeile entfernen
    if (ui.GetChecked(chkUpper)) s = s.toUpperCase();

    ui.AddItem(lstItems, s);

    if (ui.GetChecked(chkAutoClear)) {
        ui.SetText(txtInput, "");
    }
}

function removeSelected() {
    idx = ui.GetSelectedIndex(lstItems);
    if (idx < 0) return;

    ui.RemoveItem(lstItems, idx);
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

        if (type == "formclosing") {
            running = false;
        }

        if (type == "click") {
            if (id == btnAdd) addCurrentText();
            if (id == btnRemove) removeSelected();
            if (id == btnClear) clearList();
        }

        if (type == "checkedchanged") {
            if (id == chkUpper) {
                isOn = (e[2] != 0);
                if (isOn) ui.SetText(lblInfo, "UPPERCASE ON");
                else ui.SetText(lblInfo, "UPPERCASE OFF");
            }

            if (id == chkAutoClear) {
                isOn = (e[2] != 0);
                if (isOn) ui.SetText(lblInfo, "Auto-clear ON");
                else ui.SetText(lblInfo, "Auto-clear OFF");
            }
        }

        if (type == "keydown") {
            key = e[2];

            // ESC quit
            if (key == 27) running = false;

            // Enter in textbox => add
            if (id == txtInput && key == 13) addCurrentText();

            // Delete in listbox => remove selected (Keys.Delete meist 46)
            if (id == lstItems && key == 46) removeSelected();
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
