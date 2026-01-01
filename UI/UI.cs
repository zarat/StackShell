using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace StackShell
{
    /// <summary>
    /// WinForms UI Plugin für ScriptStack:
    /// - UI Thread (STA) + MessageLoop
    /// - Handles (int) für Forms/Controls
    /// - Events werden in Queue gepusht und im Script per ui.PollEvent() abgeholt
    /// </summary>
    public class UI : Model
    {
        #region Private Classes

        private class UiForm : Form
        {
            public UiForm()
            {
                DoubleBuffered = true;
                KeyPreview = true;
            }
        }

        private class UiEvt
        {
            public string Type = "";
            public int Id;
            public int I0;
            public int I1;
            public int I2;
            public int I3;
            public string S0 = "";
            public string S1 = "";
        }

        private class MultiFormContext : ApplicationContext
        {
            private int _openForms;

            public void Register(Form f)
            {
                Interlocked.Increment(ref _openForms);
                f.FormClosed += (_, __) =>
                {
                    if (Interlocked.Decrement(ref _openForms) == 0)
                    {
                        try { ExitThread(); } catch { }
                    }
                };
            }
        }

        #endregion

        #region Static

        private static ReadOnlyCollection<Routine> s_listRoutines;

        #endregion

        #region Fields

        private readonly object _sync = new object();

        private Thread _uiThread;
        private readonly ManualResetEventSlim _uiReady = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _uiExited = new ManualResetEventSlim(false);
        private Exception _uiStartError;

        private MultiFormContext _ctx;
        private UiForm _mainForm;

        private int _nextId = 1;
        private readonly Dictionary<int, Control> _controls = new Dictionary<int, Control>();
        private readonly Dictionary<Control, int> _ids = new Dictionary<Control, int>();

        private const int MaxEvents = 512;
        private readonly Queue<UiEvt> _events = new Queue<UiEvt>();

        private volatile bool _closeEventPending;
        private volatile bool _closedByUser;
        private volatile bool _everInitialised;

        #endregion

        #region Helpers

        private bool IsUiAlive =>
            _uiThread != null && _uiThread.IsAlive && _ctx != null;

        private static int ModsFromKeys(Keys modifiers)
        {
            int m = 0;
            if ((modifiers & Keys.Shift) != 0) m |= 1;
            if ((modifiers & Keys.Control) != 0) m |= 2;
            if ((modifiers & Keys.Alt) != 0) m |= 4;
            return m;
        }

        private static int Btn(MouseButtons b) => b switch
        {
            MouseButtons.Left => 1,
            MouseButtons.Right => 2,
            MouseButtons.Middle => 3,
            MouseButtons.XButton1 => 4,
            MouseButtons.XButton2 => 5,
            _ => 0
        };

        private void Enqueue(UiEvt e)
        {
            lock (_sync)
            {
                if (_events.Count >= MaxEvents)
                    _events.Dequeue();
                _events.Enqueue(e);
            }
        }

        private void HookEvents(Control c, int id)
        {
            // Mouse
            c.MouseDown += (_, e) =>
                Enqueue(new UiEvt { Type = "mousedown", Id = id, I0 = e.X, I1 = e.Y, I2 = Btn(e.Button), I3 = e.Clicks });

            c.MouseUp += (_, e) =>
                Enqueue(new UiEvt { Type = "mouseup", Id = id, I0 = e.X, I1 = e.Y, I2 = Btn(e.Button), I3 = e.Clicks });

            c.MouseMove += (_, e) =>
                Enqueue(new UiEvt { Type = "mousemove", Id = id, I0 = e.X, I1 = e.Y });

            c.MouseWheel += (_, e) =>
                Enqueue(new UiEvt { Type = "mousewheel", Id = id, I0 = e.X, I1 = e.Y, I2 = e.Delta });

            c.Click += (_, __) => Enqueue(new UiEvt { Type = "click", Id = id });

            c.DoubleClick += (_, __) => Enqueue(new UiEvt { Type = "dblclick", Id = id });

            // Keys
            c.KeyDown += (_, e) =>
                Enqueue(new UiEvt { Type = "keydown", Id = id, I0 = (int)e.KeyCode, I1 = ModsFromKeys(e.Modifiers) });

            c.KeyUp += (_, e) =>
                Enqueue(new UiEvt { Type = "keyup", Id = id, I0 = (int)e.KeyCode, I1 = ModsFromKeys(e.Modifiers) });

            c.KeyPress += (_, e) =>
                Enqueue(new UiEvt { Type = "keypress", Id = id, S0 = e.KeyChar.ToString(), I1 = ModsFromKeys(Control.ModifierKeys) });

            // Control-spezifisch
            if (c is TextBoxBase tb)
            {
                tb.TextChanged += (_, __) => Enqueue(new UiEvt { Type = "textchanged", Id = id, S0 = tb.Text ?? "" });
            }
            if (c is CheckBox cb)
            {
                cb.CheckedChanged += (_, __) => Enqueue(new UiEvt { Type = "checkedchanged", Id = id, I0 = cb.Checked ? 1 : 0 });
            }
            if (c is RadioButton rb)
            {
                rb.CheckedChanged += (_, __) => Enqueue(new UiEvt { Type = "checkedchanged", Id = id, I0 = rb.Checked ? 1 : 0 });
            }
            if (c is ComboBox combo)
            {
                combo.SelectedIndexChanged += (_, __) =>
                    Enqueue(new UiEvt
                    {
                        Type = "selectedchanged",
                        Id = id,
                        I0 = combo.SelectedIndex,
                        S0 = combo.SelectedItem?.ToString() ?? ""
                    });
                combo.TextChanged += (_, __) => Enqueue(new UiEvt { Type = "textchanged", Id = id, S0 = combo.Text ?? "" });
            }
            if (c is ListBox lb)
            {
                lb.SelectedIndexChanged += (_, __) =>
                    Enqueue(new UiEvt
                    {
                        Type = "selectedchanged",
                        Id = id,
                        I0 = lb.SelectedIndex,
                        S0 = lb.SelectedItem?.ToString() ?? ""
                    });
            }
            if (c is TrackBar tr)
            {
                tr.Scroll += (_, __) => Enqueue(new UiEvt { Type = "valuechanged", Id = id, I0 = tr.Value });
            }
            if (c is NumericUpDown nud)
            {
                nud.ValueChanged += (_, __) => Enqueue(new UiEvt { Type = "valuechanged", Id = id, I0 = (int)nud.Value });
            }
        }

        private ScriptStack.Runtime.ArrayList MakeArrayList(params object[] items)
        {
            var al = new ScriptStack.Runtime.ArrayList();
            foreach (var it in items) al.Add(it);
            return al;
        }

        private T RunOnUi<T>(Func<T> func, T fallback = default)
        {
            if (_mainForm == null || _mainForm.IsDisposed) return fallback;
            try
            {
                if (_mainForm.InvokeRequired)
                    return (T)_mainForm.Invoke(func);
                return func();
            }
            catch { return fallback; }
        }

        private void RunOnUi(Action action)
        {
            if (_mainForm == null || _mainForm.IsDisposed) return;
            try
            {
                if (_mainForm.InvokeRequired) _mainForm.BeginInvoke(action);
                else action();
            }
            catch { }
        }

        private Control GetControl(int id)
        {
            lock (_sync)
            {
                _controls.TryGetValue(id, out var c);
                return c;
            }
        }

        private int AddControl(Control c)
        {
            lock (_sync)
            {
                int id = _nextId++;
                _controls[id] = c;
                _ids[c] = id;
                return id;
            }
        }

        private bool RemoveControl(int id)
        {
            Control c;
            lock (_sync)
            {
                if (!_controls.TryGetValue(id, out c))
                    return false;

                _controls.Remove(id);
                _ids.Remove(c);
            }

            try
            {
                RunOnUi(() =>
                {
                    try
                    {
                        if (c is Form f)
                            f.Close();
                        else
                        {
                            var parent = c.Parent;
                            parent?.Controls.Remove(c);
                            c.Dispose();
                        }
                    }
                    catch { }
                });
            }
            catch { }

            return true;
        }

        private Control CreateFromType(string type)
        {
            var t = (type ?? "").Trim().ToLowerInvariant();

            return t switch
            {
                "button" => new Button(),
                "label" => new Label { AutoSize = false },
                "textbox" => new TextBox(),
                "multilinetextbox" => new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical },
                "checkbox" => new CheckBox(),
                "radiobutton" => new RadioButton(),
                "combobox" => new ComboBox(),
                "listbox" => new ListBox(),
                "panel" => new Panel(),
                "groupbox" => new GroupBox(),
                "picturebox" => new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle },
                "progressbar" => new ProgressBar(),
                "trackbar" => new TrackBar(),
                "numericupdown" => new NumericUpDown(),
                _ => null
            };
        }

        private bool StartUi(int width, int height, string title)
        {
            if (!OperatingSystem.IsWindows())
                return false;

            if (IsUiAlive)
                return false;

            _uiStartError = null;
            _uiReady.Reset();
            _uiExited.Reset();

            _closeEventPending = false;
            _closedByUser = false;
            _everInitialised = true;

            lock (_sync)
            {
                _events.Clear();
                _controls.Clear();
                _ids.Clear();
                _nextId = 1;
            }

            _uiThread = new Thread(() =>
            {
                try
                {
                    Application.SetHighDpiMode(HighDpiMode.SystemAware);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    _ctx = new MultiFormContext();

                    var form = new UiForm
                    {
                        Text = string.IsNullOrWhiteSpace(title) ? "UI" : title,
                        StartPosition = FormStartPosition.CenterScreen,
                        ClientSize = new Size(width, height)
                    };

                    // Close tracking
                    form.FormClosing += (_, e) =>
                    {
                        bool user = e.CloseReason == CloseReason.UserClosing;
                        _closedByUser = user;
                        _closeEventPending = true;
                        Enqueue(new UiEvt { Type = "formclosing", Id = 1, I0 = user ? 1 : 0 });
                    };

                    // Register + IDs
                    _mainForm = form;
                    int formId = AddControl(form);
                    HookEvents(form, formId);

                    _ctx.Register(form);

                    _uiReady.Set();

                    form.Show();
                    Application.Run(_ctx);
                }
                catch (Exception ex)
                {
                    _uiStartError = ex;
                    _uiReady.Set();
                }
                finally
                {
                    try
                    {
                        lock (_sync)
                        {
                            _controls.Clear();
                            _ids.Clear();
                            _events.Clear();
                        }
                    }
                    catch { }

                    _mainForm = null;
                    _ctx = null;
                    _uiExited.Set();
                }
            });

            _uiThread.IsBackground = true;
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();

            _uiReady.Wait();
            return _uiStartError == null && _mainForm != null;
        }

        private void StopUi()
        {
            var t = _uiThread;
            var f = _mainForm;

            if (f == null) return;

            RunOnUi(() =>
            {
                try
                {
                    // schließt alle Forms => Context beendet Thread
                    foreach (Form open in Application.OpenForms)
                    {
                        try { open.Close(); } catch { }
                    }
                }
                catch
                {
                    try { f.Close(); } catch { }
                }
            });

            if (t != null && Thread.CurrentThread != t)
                _uiExited.Wait();
        }

        #endregion

        #region Ctor / Routines

        public UI()
        {
            // Instance-Felder sind schon initialisiert (oben). Routines nur einmal bauen:
            if (s_listRoutines != null) return;

            var list = new List<Routine>();

            Type typeBool = typeof(bool);
            Type typeInt = typeof(int);
            Type typeString = typeof(string);

            // lifecycle
            list.Add(new Routine(typeBool, "ui.Initialise", typeInt, typeInt, typeString));
            list.Add(new Routine(typeBool, "ui.Shutdown"));
            list.Add(new Routine(typeBool, "ui.IsOpen"));
            list.Add(new Routine(typeBool, "ui.PollUserClosed"));

            // controls
            List<Type> types = new List<Type>();
            types.Add(typeString);
            types.Add(typeInt);
            types.Add(typeInt);
            types.Add(typeInt);
            types.Add(typeInt);
            types.Add(typeInt);
            types.Add(typeString);
            list.Add(new Routine(typeInt, "ui.CreateControl", types));
            list.Add(new Routine(typeBool, "ui.Remove", typeInt));

            // properties
            list.Add(new Routine(typeBool, "ui.SetText", typeInt, typeString));
            list.Add(new Routine(typeString, "ui.GetText", typeInt));

            List<Type> fiveInts = new List<Type>();
            fiveInts.Add(typeInt);
            fiveInts.Add(typeInt);
            fiveInts.Add(typeInt);
            fiveInts.Add(typeInt);
            fiveInts.Add(typeInt);
            list.Add(new Routine(typeBool, "ui.SetBounds", fiveInts));
            list.Add(new Routine(typeBool, "ui.SetVisible", typeInt, typeBool));
            list.Add(new Routine(typeBool, "ui.SetEnabled", typeInt, typeBool));

            List<Type> fourInts = new List<Type>();
            fourInts.Add(typeInt);
            fourInts.Add(typeInt);
            fourInts.Add(typeInt);
            fourInts.Add(typeInt);
            list.Add(new Routine(typeBool, "ui.SetBackColor", fourInts));
            list.Add(new Routine(typeBool, "ui.SetForeColor", fourInts));

            list.Add(new Routine(typeBool, "ui.SetValue", typeInt, typeInt));
            list.Add(new Routine(typeInt, "ui.GetValue", typeInt));

            list.Add(new Routine(typeBool, "ui.SetChecked", typeInt, typeBool));
            list.Add(new Routine(typeBool, "ui.GetChecked", typeInt));

            list.Add(new Routine(typeBool, "ui.AddItem", typeInt, typeString));
            list.Add(new Routine(typeBool, "ui.ClearItems", typeInt));

            list.Add(new Routine(typeBool, "ui.SetSelectedIndex", typeInt, typeInt));
            list.Add(new Routine(typeInt, "ui.GetSelectedIndex", typeInt));

            // events
            list.Add(new Routine((Type)null, "ui.PollEvent"));

            list.Add(new Routine(typeBool, "ui.RemoveItem", typeInt, typeInt));

            s_listRoutines = list.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines => s_listRoutines;

        #endregion

        #region Invoke

        public object Invoke(string strFunctionName, List<object> listParameters)
        {
            // --- Status ---
            if (strFunctionName == "ui.IsOpen")
                return IsUiAlive && _mainForm != null && !_mainForm.IsDisposed;

            if (strFunctionName == "ui.PollUserClosed")
            {
                if (_closeEventPending && _closedByUser)
                {
                    _closeEventPending = false;
                    _closedByUser = false;
                    return true;
                }
                return false;
            }

            // --- Lifecycle ---
            if (strFunctionName == "ui.Initialise")
            {
                if (IsUiAlive) return false;

                int w = (int)listParameters[0];
                int h = (int)listParameters[1];
                string title = (string)listParameters[2];

                if (w < 100 || h < 80) return false;
                return StartUi(w, h, title);
            }

            if (strFunctionName == "ui.Shutdown")
            {
                if (!IsUiAlive) return true;
                StopUi();
                return true;
            }

            // --- Event polling ---
            if (strFunctionName == "ui.PollEvent")
            {
                UiEvt evt = null;
                lock (_sync)
                {
                    if (_events.Count > 0)
                        evt = _events.Dequeue();
                }

                if (evt == null) return null;

                return MakeArrayList(evt.Type, evt.Id, evt.I0, evt.I1, evt.I2, evt.I3, evt.S0 ?? "", evt.S1 ?? "");
            }

            // If UI not alive => no-op
            if (!OperatingSystem.IsWindows())
                return false;

            // --- Create control ---
            if (strFunctionName == "ui.CreateControl")
            {
                if (!IsUiAlive || _mainForm == null || _mainForm.IsDisposed) return 0;

                string type = (string)listParameters[0];
                int parentId = (int)listParameters[1];
                int x = (int)listParameters[2];
                int y = (int)listParameters[3];
                int w = (int)listParameters[4];
                int h = (int)listParameters[5];
                string text = (string)listParameters[6];

                return RunOnUi(() =>
                {
                    var parent = parentId == 0 ? _mainForm : GetControl(parentId);
                    if (parent == null) return 0;

                    Control c;
                    if ((type ?? "").Trim().Equals("form", StringComparison.OrdinalIgnoreCase))
                    {
                        var f = new UiForm
                        {
                            Text = string.IsNullOrWhiteSpace(text) ? "Form" : text,
                            StartPosition = FormStartPosition.CenterScreen,
                            ClientSize = new Size(Math.Max(100, w), Math.Max(80, h))
                        };

                        f.FormClosing += (_, e) =>
                        {
                            bool user = e.CloseReason == CloseReason.UserClosing;
                            Enqueue(new UiEvt { Type = "formclosing", Id = _ids.TryGetValue(f, out var fid) ? fid : 0, I0 = user ? 1 : 0 });
                        };

                        int id = AddControl(f);
                        HookEvents(f, id);
                        _ctx?.Register(f);
                        f.Show();
                        return id;
                    }

                    c = CreateFromType(type);
                    if (c == null) return 0;

                    c.Text = text ?? "";
                    c.Location = new Point(x, y);
                    c.Size = new Size(Math.Max(1, w), Math.Max(1, h));

                    int newId = AddControl(c);
                    HookEvents(c, newId);

                    parent.Controls.Add(c);
                    c.BringToFront();
                    c.Focus();

                    return newId;
                }, 0);
            }

            // --- Remove ---
            if (strFunctionName == "ui.Remove")
            {
                int id = (int)listParameters[0];
                if (id <= 0) return false;
                if (id == 1) return false; // main form nicht via remove
                return RemoveControl(id);
            }

            // --- Text ---
            if (strFunctionName == "ui.SetText")
            {
                int id = (int)listParameters[0];
                string text = (string)listParameters[1];

                var c = GetControl(id);
                if (c == null) return false;

                RunOnUi(() => { try { c.Text = text ?? ""; } catch { } });
                return true;
            }

            if (strFunctionName == "ui.GetText")
            {
                int id = (int)listParameters[0];
                var c = GetControl(id);
                if (c == null) return "";
                return RunOnUi(() => c.Text ?? "", "");
            }

            // --- Bounds ---
            if (strFunctionName == "ui.SetBounds")
            {
                int id = (int)listParameters[0];
                int x = (int)listParameters[1];
                int y = (int)listParameters[2];
                int w = (int)listParameters[3];
                int h = (int)listParameters[4];

                var c = GetControl(id);
                if (c == null) return false;

                RunOnUi(() =>
                {
                    try
                    {
                        c.Location = new Point(x, y);
                        c.Size = new Size(Math.Max(1, w), Math.Max(1, h));
                    }
                    catch { }
                });
                return true;
            }

            // --- Visible / Enabled ---
            if (strFunctionName == "ui.SetVisible")
            {
                int id = (int)listParameters[0];
                bool v = (bool)listParameters[1];

                var c = GetControl(id);
                if (c == null) return false;

                RunOnUi(() => { try { c.Visible = v; } catch { } });
                return true;
            }

            if (strFunctionName == "ui.SetEnabled")
            {
                int id = (int)listParameters[0];
                bool v = (bool)listParameters[1];

                var c = GetControl(id);
                if (c == null) return false;

                RunOnUi(() => { try { c.Enabled = v; } catch { } });
                return true;
            }

            // --- Colors ---
            if (strFunctionName == "ui.SetBackColor" || strFunctionName == "ui.SetForeColor")
            {
                int id = (int)listParameters[0];
                int r = (int)listParameters[1];
                int g = (int)listParameters[2];
                int b = (int)listParameters[3];

                var c = GetControl(id);
                if (c == null) return false;

                var col = Color.FromArgb(
                    Math.Max(0, Math.Min(255, r)),
                    Math.Max(0, Math.Min(255, g)),
                    Math.Max(0, Math.Min(255, b)));

                RunOnUi(() =>
                {
                    try
                    {
                        if (strFunctionName == "ui.SetBackColor") c.BackColor = col;
                        else c.ForeColor = col;
                    }
                    catch { }
                });

                return true;
            }

            // --- Value ---
            if (strFunctionName == "ui.SetValue")
            {
                int id = (int)listParameters[0];
                int v = (int)listParameters[1];

                var c = GetControl(id);
                if (c == null) return false;

                RunOnUi(() =>
                {
                    try
                    {
                        switch (c)
                        {
                            case TrackBar tr:
                                tr.Value = Math.Max(tr.Minimum, Math.Min(tr.Maximum, v));
                                break;
                            case ProgressBar pb:
                                pb.Value = Math.Max(pb.Minimum, Math.Min(pb.Maximum, v));
                                break;
                            case NumericUpDown nud:
                                nud.Value = Math.Max(nud.Minimum, Math.Min(nud.Maximum, v));
                                break;
                        }
                    }
                    catch { }
                });

                return true;
            }

            if (strFunctionName == "ui.GetValue")
            {
                int id = (int)listParameters[0];
                var c = GetControl(id);
                if (c == null) return 0;

                return RunOnUi(() =>
                {
                    try
                    {
                        return c switch
                        {
                            TrackBar tr => tr.Value,
                            ProgressBar pb => pb.Value,
                            NumericUpDown nud => (int)nud.Value,
                            _ => 0
                        };
                    }
                    catch { return 0; }
                }, 0);
            }

            // --- Checked ---
            if (strFunctionName == "ui.SetChecked")
            {
                int id = (int)listParameters[0];
                bool v = (bool)listParameters[1];

                var c = GetControl(id);
                if (c == null) return false;

                RunOnUi(() =>
                {
                    try
                    {
                        switch (c)
                        {
                            case CheckBox cb: cb.Checked = v; break;
                            case RadioButton rb: rb.Checked = v; break;
                        }
                    }
                    catch { }
                });

                return true;
            }

            if (strFunctionName == "ui.GetChecked")
            {
                int id = (int)listParameters[0];
                var c = GetControl(id);
                if (c == null) return false;

                return RunOnUi(() =>
                {
                    try
                    {
                        return c switch
                        {
                            CheckBox cb => cb.Checked,
                            RadioButton rb => rb.Checked,
                            _ => false
                        };
                    }
                    catch { return false; }
                }, false);
            }

            // --- Items ---
            if (strFunctionName == "ui.AddItem")
            {
                int id = (int)listParameters[0];
                string item = (string)listParameters[1];

                var c = GetControl(id);
                if (c == null) return false;

                RunOnUi(() =>
                {
                    try
                    {
                        switch (c)
                        {
                            case ListBox lb: lb.Items.Add(item ?? ""); break;
                            case ComboBox cb: cb.Items.Add(item ?? ""); break;
                        }
                    }
                    catch { }
                });

                return true;
            }

            if (strFunctionName == "ui.ClearItems")
            {
                int id = (int)listParameters[0];
                var c = GetControl(id);
                if (c == null) return false;

                RunOnUi(() =>
                {
                    try
                    {
                        switch (c)
                        {
                            case ListBox lb: lb.Items.Clear(); break;
                            case ComboBox cb: cb.Items.Clear(); break;
                        }
                    }
                    catch { }
                });

                return true;
            }

            // --- Selection ---
            if (strFunctionName == "ui.SetSelectedIndex")
            {
                int id = (int)listParameters[0];
                int idx = (int)listParameters[1];

                var c = GetControl(id);
                if (c == null) return false;

                RunOnUi(() =>
                {
                    try
                    {
                        switch (c)
                        {
                            case ListBox lb:
                                lb.SelectedIndex = (idx >= -1 && idx < lb.Items.Count) ? idx : -1;
                                break;
                            case ComboBox cb:
                                cb.SelectedIndex = (idx >= -1 && idx < cb.Items.Count) ? idx : -1;
                                break;
                        }
                    }
                    catch { }
                });

                return true;
            }

            if (strFunctionName == "ui.GetSelectedIndex")
            {
                int id = (int)listParameters[0];
                var c = GetControl(id);
                if (c == null) return -1;

                return RunOnUi(() =>
                {
                    try
                    {
                        return c switch
                        {
                            ListBox lb => lb.SelectedIndex,
                            ComboBox cb => cb.SelectedIndex,
                            _ => -1
                        };
                    }
                    catch { return -1; }
                }, -1);
            }

            if (strFunctionName == "ui.RemoveItem")
            {
                int id = (int)listParameters[0];
                int idx = (int)listParameters[1];

                var c = GetControl(id);
                if (c == null) return false;

                return RunOnUi(() =>
                {
                    try
                    {
                        if (c is ListBox lb)
                        {
                            if (idx < 0 || idx >= lb.Items.Count) return false;
                            lb.Items.RemoveAt(idx);
                            return true;
                        }
                        if (c is ComboBox cb)
                        {
                            if (idx < 0 || idx >= cb.Items.Count) return false;
                            cb.Items.RemoveAt(idx);
                            return true;
                        }
                        return false;
                    }
                    catch { return false; }
                }, false);
            }

            return false;
        }

        #endregion
    }
}
