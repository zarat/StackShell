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
    public class GFX : Model
    {
        #region Private Enumerations

        private enum DrawingPrimitive
        {
            Colour,
            LineWidth,
            Line,
            DrawRectangle,
            FillRectangle,
            DrawEllipse,
            FillEllipse,
            DrawString
        }

        #endregion

        #region Private Classes

        private class GraphicsForm : Form
        {
            public GraphicsForm()
                : base()
            {
                DoubleBuffered = true;
                KeyPreview = true; // wichtig: Form bekommt Keys auch wenn Child-Control Fokus hat
            }
        }

        private class DrawingInstruction
        {
            public DrawingPrimitive m_drawingPrimitive;
            public int m_iValue0;
            public int m_iValue1;
            public int m_iValue2;
            public int m_iValue3;
            public string m_strValue4;
        }

        private class MouseEvt
        {
            public string Type;     // "down","up","move","click","dblclick","wheel"
            public int X;
            public int Y;
            public int Button;      // 0..5
            public int Clicks;
            public int WheelDelta;  // per event
            public int Modifiers;   // bitmask 1/2/4
        }

        private class KeyEvt
        {
            public string Type;    // "down","up","press"
            public int KeyCode;    // int (Keys)
            public string KeyChar; // "" wenn nicht vorhanden
            public int Modifiers;  // bitmask 1/2/4
        }

        #endregion

        #region Private Static Variables

        private static ReadOnlyCollection<Routine> s_listRoutines;

        #endregion

        #region Private Variables

        private readonly object _sync = new object();

        private GraphicsForm m_form;
        private Thread _uiThread;
        private readonly ManualResetEventSlim _uiReady = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _uiExited = new ManualResetEventSlim(false);
        private Exception _uiStartError;

        private List<DrawingInstruction> m_listDrawingInstructions;

        // UI resources (nur im UI-Thread benutzen/entsorgen)
        private Pen m_pen;
        private SolidBrush m_brush;
        private Font m_font;

        // Flags für "Fenster wurde geschlossen"
        private volatile bool _closeEventPending;
        private volatile bool _closedByUser;
        private volatile bool _everInitialised;

        // Mouse/Key Events (Queues)
        private const int MaxMouseEvents = 256;
        private const int MaxKeyEvents = 256;
        private readonly Queue<MouseEvt> _mouseQueue = new Queue<MouseEvt>();
        private readonly Queue<KeyEvt> _keyQueue = new Queue<KeyEvt>();

        // Mouse state
        private int _mouseX;
        private int _mouseY;
        private int _mouseButtonsMask; // bitmask: 1 left,2 right,4 middle,8 x1,16 x2
        private int _wheelAccum;
        private int _lastModifiers;

        #endregion

        #region Private Helpers

        private bool IsUiAlive =>
            m_form != null && !m_form.IsDisposed && _uiThread != null && _uiThread.IsAlive;

        private void RunOnUi(Action action)
        {
            var form = m_form;
            if (form == null || form.IsDisposed) return;

            try
            {
                if (form.InvokeRequired) form.BeginInvoke(action);
                else action();
            }
            catch
            {
                // UI schließt vielleicht gerade – ignorieren
            }
        }

        private void InvalidateUi()
        {
            RunOnUi(() =>
            {
                if (m_form != null && !m_form.IsDisposed)
                    m_form.Invalidate();
            });
        }

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

        private static int BtnMask(MouseButtons b) => b switch
        {
            MouseButtons.Left => 1,
            MouseButtons.Right => 2,
            MouseButtons.Middle => 4,
            MouseButtons.XButton1 => 8,
            MouseButtons.XButton2 => 16,
            _ => 0
        };

        private void EnqueueMouse(string type, int x, int y, MouseButtons button, int clicks, int wheelDelta, int modifiers)
        {
            lock (_sync)
            {
                // update state
                _mouseX = x;
                _mouseY = y;
                _lastModifiers = modifiers;
                if (wheelDelta != 0) _wheelAccum += wheelDelta;

                if (_mouseQueue.Count >= MaxMouseEvents)
                    _mouseQueue.Dequeue();

                _mouseQueue.Enqueue(new MouseEvt
                {
                    Type = type,
                    X = x,
                    Y = y,
                    Button = Btn(button),
                    Clicks = clicks,
                    WheelDelta = wheelDelta,
                    Modifiers = modifiers
                });
            }
        }

        private void EnqueueKey(string type, int keyCode, string keyChar, int modifiers)
        {
            lock (_sync)
            {
                _lastModifiers = modifiers;

                if (_keyQueue.Count >= MaxKeyEvents)
                    _keyQueue.Dequeue();

                _keyQueue.Enqueue(new KeyEvt
                {
                    Type = type,
                    KeyCode = keyCode,
                    KeyChar = keyChar ?? string.Empty,
                    Modifiers = modifiers
                });
            }
        }

        private bool StartUi(int width, int height)
        {
            if (!OperatingSystem.IsWindows())
                return false;

            if (IsUiAlive)
                return false;

            _uiStartError = null;
            _uiReady.Reset();
            _uiExited.Reset();

            // Reset close flags beim (Neu)Start
            _closeEventPending = false;
            _closedByUser = false;
            _everInitialised = true;

            // Reset input state
            lock (_sync)
            {
                _mouseQueue.Clear();
                _keyQueue.Clear();
                _mouseX = 0;
                _mouseY = 0;
                _mouseButtonsMask = 0;
                _wheelAccum = 0;
                _lastModifiers = 0;
            }

            _uiThread = new Thread(() =>
            {
                try
                {
                    Application.SetHighDpiMode(HighDpiMode.SystemAware);
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    var form = new GraphicsForm
                    {
                        Text = "Gfx",
                        StartPosition = FormStartPosition.CenterScreen,
                        ClientSize = new Size(width, height)
                    };

                    // WICHTIG: User-X erkennen
                    form.FormClosing += (_, e) =>
                    {
                        if (e.CloseReason == CloseReason.UserClosing)
                        {
                            _closedByUser = true;
                            _closeEventPending = true;
                        }
                        else
                        {
                            _closeEventPending = true;
                        }

                        lock (_sync)
                        {
                            m_listDrawingInstructions.Clear();
                            _mouseQueue.Clear();
                            _keyQueue.Clear();
                        }
                    };

                    // Message-Loop definitiv beenden
                    form.FormClosed += (_, __) =>
                    {
                        try { Application.ExitThread(); } catch { }
                    };

                    // Zeichnen
                    form.Paint += OnPaint;

                    // ---- Mouse events ----
                    form.MouseDown += (_, e) =>
                    {
                        var mods = ModsFromKeys(Control.ModifierKeys);
                        lock (_sync) { _mouseButtonsMask |= BtnMask(e.Button); }
                        EnqueueMouse("down", e.X, e.Y, e.Button, e.Clicks, 0, mods);
                    };

                    form.MouseUp += (_, e) =>
                    {
                        var mods = ModsFromKeys(Control.ModifierKeys);
                        lock (_sync) { _mouseButtonsMask &= ~BtnMask(e.Button); }
                        EnqueueMouse("up", e.X, e.Y, e.Button, e.Clicks, 0, mods);
                    };

                    form.MouseMove += (_, e) =>
                    {
                        var mods = ModsFromKeys(Control.ModifierKeys);
                        EnqueueMouse("move", e.X, e.Y, MouseButtons.None, 0, 0, mods);
                    };

                    form.MouseClick += (_, e) =>
                    {
                        var mods = ModsFromKeys(Control.ModifierKeys);
                        EnqueueMouse("click", e.X, e.Y, e.Button, e.Clicks, 0, mods);
                    };

                    form.MouseDoubleClick += (_, e) =>
                    {
                        var mods = ModsFromKeys(Control.ModifierKeys);
                        EnqueueMouse("dblclick", e.X, e.Y, e.Button, e.Clicks, 0, mods);
                    };

                    form.MouseWheel += (_, e) =>
                    {
                        var mods = ModsFromKeys(Control.ModifierKeys);
                        EnqueueMouse("wheel", e.X, e.Y, MouseButtons.None, 0, e.Delta, mods);
                    };

                    // ---- Key events ----
                    form.KeyDown += (_, e) =>
                    {
                        var mods = ModsFromKeys(e.Modifiers);
                        EnqueueKey("down", (int)e.KeyCode, "", mods);
                    };

                    form.KeyUp += (_, e) =>
                    {
                        var mods = ModsFromKeys(e.Modifiers);
                        EnqueueKey("up", (int)e.KeyCode, "", mods);
                    };

                    form.KeyPress += (_, e) =>
                    {
                        var mods = ModsFromKeys(Control.ModifierKeys);
                        EnqueueKey("press", 0, e.KeyChar.ToString(), mods);
                    };

                    // UI Ressourcen anlegen
                    m_pen = new Pen(Color.Black, 1.0f);
                    m_brush = new SolidBrush(Color.Black);
                    m_font = new Font(FontFamily.GenericSansSerif, 10.0f);

                    m_form = form;

                    _uiReady.Set();

                    // BLOCKT bis Form geschlossen wird
                    Application.Run(form);
                }
                catch (Exception ex)
                {
                    _uiStartError = ex;
                    _uiReady.Set();
                }
                finally
                {
                    try { m_pen?.Dispose(); } catch { }
                    try { m_brush?.Dispose(); } catch { }
                    try { m_font?.Dispose(); } catch { }

                    m_pen = null;
                    m_brush = null;
                    m_font = null;

                    m_form = null;
                    _uiExited.Set();
                }
            });

            _uiThread.IsBackground = true;
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();

            _uiReady.Wait();

            return _uiStartError == null && m_form != null;
        }

        private void StopUi()
        {
            var uiThread = _uiThread;
            var form = m_form;

            if (form == null)
                return;

            RunOnUi(() =>
            {
                try
                {
                    if (!form.IsDisposed)
                        form.Close();
                }
                catch { }
            });

            if (uiThread != null && Thread.CurrentThread != uiThread)
                _uiExited.Wait();
        }

        /// <summary>
        /// Wenn UI weg ist: no-op + true zurückgeben (damit Scripts nicht “hängen” bleiben).
        /// </summary>
        private bool NoOpIfClosed()
        {
            if (IsUiAlive) return false;
            if (_everInitialised) return true;
            return true;
        }

        private ScriptStack.Runtime.ArrayList MakeArrayList(params object[] items)
        {
            var al = new ScriptStack.Runtime.ArrayList();
            foreach (var it in items) al.Add(it);
            return al;
        }

        #endregion

        #region Private Methods

        private void OnPaint(object objectSender, PaintEventArgs paintEventArgs)
        {
            Graphics graphics = paintEventArgs.Graphics;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            DrawingInstruction[] snapshot;
            lock (_sync)
            {
                snapshot = m_listDrawingInstructions.ToArray();
            }

            if (m_pen == null || m_brush == null || m_font == null)
                return;

            foreach (var drawingInstruction in snapshot)
            {
                switch (drawingInstruction.m_drawingPrimitive)
                {
                    case DrawingPrimitive.Colour:
                        {
                            Color color = Color.FromArgb(
                                drawingInstruction.m_iValue0,
                                drawingInstruction.m_iValue1,
                                drawingInstruction.m_iValue2);

                            m_pen.Color = color;
                            m_brush.Color = color; // kein Leak
                            break;
                        }
                    case DrawingPrimitive.LineWidth:
                        m_pen.Width = Math.Max(1, drawingInstruction.m_iValue0);
                        break;

                    case DrawingPrimitive.Line:
                        graphics.DrawLine(m_pen,
                            drawingInstruction.m_iValue0, drawingInstruction.m_iValue1,
                            drawingInstruction.m_iValue2, drawingInstruction.m_iValue3);
                        break;

                    case DrawingPrimitive.DrawRectangle:
                        graphics.DrawRectangle(m_pen,
                            drawingInstruction.m_iValue0, drawingInstruction.m_iValue1,
                            drawingInstruction.m_iValue2, drawingInstruction.m_iValue3);
                        break;

                    case DrawingPrimitive.FillRectangle:
                        graphics.FillRectangle(m_brush,
                            drawingInstruction.m_iValue0, drawingInstruction.m_iValue1,
                            drawingInstruction.m_iValue2, drawingInstruction.m_iValue3);
                        break;

                    case DrawingPrimitive.DrawEllipse:
                        graphics.DrawEllipse(m_pen,
                            drawingInstruction.m_iValue0, drawingInstruction.m_iValue1,
                            drawingInstruction.m_iValue2, drawingInstruction.m_iValue3);
                        break;

                    case DrawingPrimitive.FillEllipse:
                        graphics.FillEllipse(m_brush,
                            drawingInstruction.m_iValue0, drawingInstruction.m_iValue1,
                            drawingInstruction.m_iValue2, drawingInstruction.m_iValue3);
                        break;

                    case DrawingPrimitive.DrawString:
                        graphics.DrawString(drawingInstruction.m_strValue4 ?? string.Empty, m_font, m_brush,
                            drawingInstruction.m_iValue0, drawingInstruction.m_iValue1);
                        break;
                }
            }
        }

        #endregion

        #region Public Methods

        public GFX()
        {
            

            if (s_listRoutines != null) return;

            m_form = null;
            m_listDrawingInstructions = new List<DrawingInstruction>();

            var listRoutines = new List<Routine>();
            Routine Routine = null;

            Type typeBool = typeof(bool);
            Type typeInt = typeof(int);
            List<Type> listFourInts = new List<Type> { typeInt, typeInt, typeInt, typeInt };

            Routine = new Routine(typeBool, "Gfx_Initialise", typeInt, typeInt);
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_Shutdown");
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_Clear");
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_SetColour", typeInt, typeInt, typeInt);
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_SetLineWidth", typeInt);
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_DrawLine", listFourInts);
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_DrawRectangle", listFourInts);
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_FillRectangle", listFourInts);
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_DrawEllipse", listFourInts);
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_FillEllipse", listFourInts);
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_DrawString", typeInt, typeInt, typeof(string));
            listRoutines.Add(Routine);

            // Fensterstatus
            Routine = new Routine(typeBool, "Gfx_IsOpen");
            listRoutines.Add(Routine);

            // X-Klick erkennen
            Routine = new Routine(typeBool, "Gfx_PollUserClosed");
            listRoutines.Add(Routine);

            // Input polling (object/null)
            Routine = new Routine((Type)null, "Gfx_PollMouse");
            listRoutines.Add(Routine);

            Routine = new Routine((Type)null, "Gfx_PollKey");
            listRoutines.Add(Routine);

            Routine = new Routine((Type)null, "Gfx_GetMouseState");
            listRoutines.Add(Routine);

            // UI setter
            Routine = new Routine(typeBool, "Gfx_SetTitle", typeof(string));
            listRoutines.Add(Routine);

            Routine = new Routine(typeBool, "Gfx_SetSize", typeInt, typeInt);
            listRoutines.Add(Routine);

            s_listRoutines = listRoutines.AsReadOnly();
        }

        public object Invoke(string strFunctionName, List<object> listParameters)
        {
            // ---- Status / Close ----
            if (strFunctionName == "Gfx_IsOpen")
                return IsUiAlive;

            if (strFunctionName == "Gfx_PollUserClosed")
            {
                if (_closeEventPending && _closedByUser)
                {
                    _closeEventPending = false;
                    _closedByUser = false;
                    return true;
                }
                return false;
            }

            // ---- Mouse polling ----
            if (strFunctionName == "Gfx_PollMouse")
            {
                MouseEvt evt = null;
                lock (_sync)
                {
                    if (_mouseQueue.Count > 0)
                        evt = _mouseQueue.Dequeue();
                }

                if (evt == null) return null;

                // [type, x, y, button, clicks, wheelDelta, modifiers]
                return MakeArrayList(evt.Type, evt.X, evt.Y, evt.Button, evt.Clicks, evt.WheelDelta, evt.Modifiers);
            }

            // ---- Key polling ----
            if (strFunctionName == "Gfx_PollKey")
            {
                KeyEvt evt = null;
                lock (_sync)
                {
                    if (_keyQueue.Count > 0)
                        evt = _keyQueue.Dequeue();
                }

                if (evt == null) return null;

                // [type, keyCode, keyChar, modifiers]
                return MakeArrayList(evt.Type, evt.KeyCode, evt.KeyChar ?? "", evt.Modifiers);
            }

            // ---- Mouse state ----
            if (strFunctionName == "Gfx_GetMouseState")
            {
                if (!IsUiAlive) return null;

                int x, y, mask, wheel, mods;
                lock (_sync)
                {
                    x = _mouseX;
                    y = _mouseY;
                    mask = _mouseButtonsMask;
                    wheel = _wheelAccum;
                    mods = _lastModifiers;
                }

                // [x, y, buttonsMask, wheelAccum, modifiers]
                return MakeArrayList(x, y, mask, wheel, mods);
            }

            // ---- UI setter ----
            if (strFunctionName == "Gfx_SetTitle")
            {
                if (NoOpIfClosed()) return true;

                var title = (string)listParameters[0];
                RunOnUi(() =>
                {
                    if (m_form != null && !m_form.IsDisposed)
                        m_form.Text = title ?? "Gfx";
                });
                return true;
            }

            if (strFunctionName == "Gfx_SetSize")
            {
                if (NoOpIfClosed()) return true;

                int w = (int)listParameters[0];
                int h = (int)listParameters[1];
                if (w < 16 || h < 16) return false;

                RunOnUi(() =>
                {
                    if (m_form != null && !m_form.IsDisposed)
                        m_form.ClientSize = new Size(w, h);
                });
                InvalidateUi();
                return true;
            }

            // ---- Lifecycle ----
            if (strFunctionName == "Gfx_Initialise")
            {
                if (IsUiAlive) return false;

                int iWidth = (int)listParameters[0];
                int iHeight = (int)listParameters[1];
                if (iWidth < 16) return false;
                if (iHeight < 16) return false;

                lock (_sync)
                {
                    m_listDrawingInstructions.Clear();
                }

                bool ok = StartUi(iWidth, iHeight);
                if (!ok) return false;

                InvalidateUi();
                return true;
            }
            else if (strFunctionName == "Gfx_Shutdown")
            {
                lock (_sync)
                {
                    m_listDrawingInstructions.Clear();
                    _mouseQueue.Clear();
                    _keyQueue.Clear();
                }

                StopUi();
                return true;
            }

            // ---- Drawing / Commands ----
            else if (strFunctionName == "Gfx_Clear")
            {
                if (NoOpIfClosed()) return true;

                lock (_sync)
                {
                    m_listDrawingInstructions.Clear();
                }

                InvalidateUi();
                return true;
            }
            else if (strFunctionName == "Gfx_SetColour")
            {
                if (NoOpIfClosed()) return true;

                var drawingInstruction = new DrawingInstruction
                {
                    m_drawingPrimitive = DrawingPrimitive.Colour,
                    m_iValue0 = (int)listParameters[0],
                    m_iValue1 = (int)listParameters[1],
                    m_iValue2 = (int)listParameters[2]
                };

                lock (_sync)
                {
                    m_listDrawingInstructions.Add(drawingInstruction);
                }

                InvalidateUi();
                return true;
            }
            else if (strFunctionName == "Gfx_SetLineWidth")
            {
                if (NoOpIfClosed()) return true;

                var drawingInstruction = new DrawingInstruction
                {
                    m_drawingPrimitive = DrawingPrimitive.LineWidth,
                    m_iValue0 = (int)listParameters[0]
                };

                lock (_sync)
                {
                    m_listDrawingInstructions.Add(drawingInstruction);
                }

                InvalidateUi();
                return true;
            }
            else if (strFunctionName == "Gfx_DrawLine")
            {
                if (NoOpIfClosed()) return true;

                var drawingInstruction = new DrawingInstruction
                {
                    m_drawingPrimitive = DrawingPrimitive.Line,
                    m_iValue0 = (int)listParameters[0],
                    m_iValue1 = (int)listParameters[1],
                    m_iValue2 = (int)listParameters[2],
                    m_iValue3 = (int)listParameters[3]
                };

                lock (_sync)
                {
                    m_listDrawingInstructions.Add(drawingInstruction);
                }

                InvalidateUi();
                return true;
            }
            else if (strFunctionName == "Gfx_DrawRectangle")
            {
                if (NoOpIfClosed()) return true;

                var drawingInstruction = new DrawingInstruction
                {
                    m_drawingPrimitive = DrawingPrimitive.DrawRectangle,
                    m_iValue0 = (int)listParameters[0],
                    m_iValue1 = (int)listParameters[1],
                    m_iValue2 = (int)listParameters[2],
                    m_iValue3 = (int)listParameters[3]
                };

                lock (_sync)
                {
                    m_listDrawingInstructions.Add(drawingInstruction);
                }

                InvalidateUi();
                return true;
            }
            else if (strFunctionName == "Gfx_FillRectangle")
            {
                if (NoOpIfClosed()) return true;

                var drawingInstruction = new DrawingInstruction
                {
                    m_drawingPrimitive = DrawingPrimitive.FillRectangle,
                    m_iValue0 = (int)listParameters[0],
                    m_iValue1 = (int)listParameters[1],
                    m_iValue2 = (int)listParameters[2],
                    m_iValue3 = (int)listParameters[3]
                };

                lock (_sync)
                {
                    m_listDrawingInstructions.Add(drawingInstruction);
                }

                InvalidateUi();
                return true;
            }
            else if (strFunctionName == "Gfx_DrawEllipse")
            {
                if (NoOpIfClosed()) return true;

                var drawingInstruction = new DrawingInstruction
                {
                    m_drawingPrimitive = DrawingPrimitive.DrawEllipse,
                    m_iValue0 = (int)listParameters[0],
                    m_iValue1 = (int)listParameters[1],
                    m_iValue2 = (int)listParameters[2],
                    m_iValue3 = (int)listParameters[3]
                };

                lock (_sync)
                {
                    m_listDrawingInstructions.Add(drawingInstruction);
                }

                InvalidateUi();
                return true;
            }
            else if (strFunctionName == "Gfx_FillEllipse")
            {
                if (NoOpIfClosed()) return true;

                var drawingInstruction = new DrawingInstruction
                {
                    m_drawingPrimitive = DrawingPrimitive.FillEllipse,
                    m_iValue0 = (int)listParameters[0],
                    m_iValue1 = (int)listParameters[1],
                    m_iValue2 = (int)listParameters[2],
                    m_iValue3 = (int)listParameters[3]
                };

                lock (_sync)
                {
                    m_listDrawingInstructions.Add(drawingInstruction);
                }

                InvalidateUi();
                return true;
            }
            else if (strFunctionName == "Gfx_DrawString")
            {
                if (NoOpIfClosed()) return true;

                var drawingInstruction = new DrawingInstruction
                {
                    m_drawingPrimitive = DrawingPrimitive.DrawString,
                    m_iValue0 = (int)listParameters[0],
                    m_iValue1 = (int)listParameters[1],
                    m_strValue4 = (string)listParameters[2]
                };

                lock (_sync)
                {
                    m_listDrawingInstructions.Add(drawingInstruction);
                }

                InvalidateUi();
                return true;
            }

            return false;
        }

        #endregion

        #region Public Properties

        public ReadOnlyCollection<Routine> Routines => s_listRoutines;

        #endregion
    }
}
