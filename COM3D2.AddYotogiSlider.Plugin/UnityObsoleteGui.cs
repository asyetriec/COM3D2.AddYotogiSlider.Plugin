using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using PV = UnityObsoleteGui.PixelValuesCM3D2;

namespace UnityObsoleteGui
{

    public abstract class Element : IComparable<Element>
    {
        protected readonly int id;

        protected string name;
        protected Rect rect;
        protected bool visible;

        public string Name { get { return name; } }

        public virtual Rect Rectangle { get { return rect; } }
        public virtual float Left { get { return rect.x; } }
        public virtual float Top { get { return rect.y; } }
        public virtual float Width { get { return rect.width; } }
        public virtual float Height { get { return rect.height; } }
        public virtual bool Visible
        {
            get { return visible; }
            set
            {
                visible = value;
                if (Parent != null) notifyParent(false, true);
            }
        }

        public Container Parent = null;
        public event EventHandler<ElementEventArgs> NotifyParent = delegate { };


        public Element() { }
        public Element(string name, Rect rect)
        {
            this.id = this.GetHashCode();
            this.name = name;
            this.rect = rect;
            this.visible = true;
        }

        public virtual void Draw() { Draw(this.rect); }
        public virtual void Draw(Rect outRect) { }
        public virtual void Resize() { Resize(false); }
        public virtual void Resize(bool broadCast) { if (!broadCast) notifyParent(true, false); }

        public virtual int CompareTo(Element e) { return this.name.CompareTo(e.Name); }

        protected virtual void notifyParent(bool sizeChanged, bool visibleChanged)
        {
            NotifyParent(this, new ElementEventArgs(name, sizeChanged, visibleChanged));
        }
    }


    public abstract class Container : Element, IEnumerable<Element>
    {
        public static Element Find(Container parent, string s) { return Container.Find<Element>(parent, s); }
        public static T Find<T>(Container parent, string s) where T : Element
        {
            if (parent == null) return null;

            foreach (Element e in parent)
            {
                if (e is T && e.Name == s) return e as T;
                if (e is Container)
                {
                    T e2 = Find<T>(e as Container, s);
                    if (e2 != null) return e2 as T;
                }
            }

            return null;
        }

        //----

        protected List<Element> children = new List<Element>();

        public int ChildCount { get { return children.Count; } }


        public Container(string name, Rect rect) : base(name, rect) { }

        public Element this[string s]
        {
            get { return GetChild<Element>(s); }
            set { if (value is Element) AddChild(value); }
        }

        public Element AddChild(Element child) { return AddChild<Element>(child); }
        public T AddChild<T>(T child) where T : Element
        {
            if (child != null && !children.Contains(child))
            {
                child.Parent = this;
                child.NotifyParent += this.onChildChenged;
                children.Add(child);
                Resize();

                return child;
            }

            return null;
        }

        public Element GetChild(string s) { return GetChild<Element>(s); }
        public T GetChild<T>() where T : Element { return GetChild<T>(""); }
        public T GetChild<T>(string s) where T : Element
        {
            return children.FirstOrDefault(e => e is T && (s == "" ? true : e.Name == s)) as T;
        }

        public void RemoveChild(string s)
        {
            Element child = children.FirstOrDefault(e => e.Name == s);
            if (child != null)
            {
                child.Parent = null;
                child.NotifyParent -= this.onChildChenged;
                children.Remove(child);
                Resize();
            }
        }

        public void RemoveChildren()
        {
            foreach (Element child in children)
            {
                child.Parent = null;
                child.NotifyParent -= this.onChildChenged;
            }
            children.Clear();
            Resize();
        }

        public virtual void onChildChenged(object sender, EventArgs e) { Resize(); }

        IEnumerator IEnumerable.GetEnumerator() { return this.GetEnumerator(); }
        public IEnumerator<Element> GetEnumerator() { return children.GetEnumerator(); }

    }


    public class Window : Container
    {

        #region Constants
        public const float AutoLayout = -1f;

        [Flags]
        public enum Scroll
        {
            None = 0x00,
            HScroll = 0x01,
            VScroll = 0x02
        }

        #endregion



        #region Nested classes

        private class HorizontalSpacer : Element
        {
            public HorizontalSpacer(float height)
            : base("Spacer:", new Rect(Window.AutoLayout, Window.AutoLayout, Window.AutoLayout, height))
            {
                this.name += this.id;
            }
        }

        #endregion



        #region Variables

        private Rect sizeRatio;
        private Rect baseRect;
        private Rect titleRect;
        private Rect contentRect;
        private Vector2 autoSize = Vector2.zero;
        private Vector2 hScrollViewPos = Vector2.zero;
        private Vector2 vScrollViewPos = Vector2.zero;
        private Vector2 lastScreenSize;
        private int colums = 1;

        public GUIStyle WindowStyle = "window";
        public GUIStyle LabelStyle = "label";
        public string HeaderText;
        public int HeaderFontSize;
        public string TitleText;
        public float TitleHeight;
        public int TitleFontSize;
        public Scroll scroll = Scroll.None;

        #endregion



        #region Methods

        public Window(Rect ratio, string header, string title) : this(title, ratio, header, title, null) { }
        public Window(string name, Rect ratio, string header, string title) : this(name, ratio, header, title, null) { }
        public Window(string name, Rect ratio, string header, string title, List<Element> children) : base(name, PV.PropScreenMH(ratio))
        {
            this.sizeRatio = ratio;
            this.HeaderText = header;
            this.TitleText = title;
            this.TitleHeight = PV.Line("C1");

            if (children != null && children.Count > 0)
            {
                this.children = new List<Element>(children);
                foreach (Element child in children)
                {
                    child.Parent = this;
                    child.NotifyParent += this.onChildChenged;

                }
                Resize();
            }

            lastScreenSize = new Vector2(Screen.width, Screen.height);
        }

        public override void Draw(Rect outRect)
        {
            if (propScreen())
            {
                resizeAllChildren(this);
                Resize();
                outRect = rect;
            }

            WindowStyle.fontSize = PV.Font("C2");
            WindowStyle.alignment = TextAnchor.UpperRight;

            rect = GUI.Window(id, outRect, drawWindow, HeaderText, WindowStyle);
        }

        public override void Resize()
        {
            calcAutoSize();
        }

        public Element AddHorizontalSpacer() { return AddHorizontalSpacer((float)PV.Margin); }
        public Element AddHorizontalSpacer(float height) { return AddChild(new HorizontalSpacer(height)); }

        //----

        private void drawWindow(int id)
        {
            TitleHeight = PV.Line("C1");
            TitleFontSize = PV.Font("C2");

            LabelStyle.fontSize = TitleFontSize;
            LabelStyle.alignment = TextAnchor.UpperLeft;
            GUI.Label(titleRect, TitleText, LabelStyle);

            GUI.BeginGroup(contentRect);
            {
                Rect cur = new Rect(0f, 0f, 0f, 0f);

                foreach (Element child in children)
                {
                    if (!child.Visible) continue;

                    if (child.Left >= 0 || child.Top >= 0)
                    {
                        Rect tmp = new Rect((child.Left >= 0) ? child.Left : cur.x,
                                              (child.Top >= 0) ? child.Top : cur.y,
                                              (child.Width > 0) ? child.Width : autoSize.x,
                                              (child.Height > 0) ? child.Height : autoSize.y);

                        child.Draw(tmp);
                    }
                    else
                    {
                        cur.width = (child.Width > 0) ? child.Width : autoSize.x;
                        cur.height = (child.Height > 0) ? child.Height : autoSize.y;
                        child.Draw(cur);
                        cur.y += cur.height;
                    }
                }
            }
            GUI.EndGroup();

            GUI.DragWindow();
        }

        private bool propScreen()
        {
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            if (lastScreenSize != screenSize)
            {
                rect = PV.PropScreenMH(rect.x, rect.y, sizeRatio.width, sizeRatio.height, lastScreenSize);
                lastScreenSize = screenSize;
                calcRectSize();
                return true;
            }
            return false;
        }

        private void calcRectSize()
        {
            baseRect = PV.InsideRect(rect);
            titleRect = new Rect(PV.Margin, 0, baseRect.width, TitleHeight);
            contentRect = new Rect(baseRect.x, baseRect.y + titleRect.height, baseRect.width, baseRect.height - titleRect.height);
        }

        public void calcAutoSize()
        {
            Vector2 used = Vector2.zero;
            Vector2 count = Vector2.zero;

            foreach (Element child in children)
            {
                if (!child.Visible) continue;

                if (!(child.Left > 0 || child.Top > 0) && child.Width > 0) used.x += child.Width;
                else count.x += 1;

                if (!(child.Left > 0 || child.Top > 0) && child.Height > 0) used.y += child.Height;
                else count.y += 1;
            }

            {
                bool rectChanged = false;

                if ((scroll & Window.Scroll.HScroll) == 0x00)
                {
                    if (contentRect.width < used.x || (contentRect.width > used.x && count.x == 0))
                    {
                        rect.width = used.x + PV.Margin * 2;
                        rectChanged = true;
                    }
                }

                if ((scroll & Window.Scroll.VScroll) == 0x00)
                {
                    if (contentRect.height < used.y || (contentRect.height > used.y && count.y == 0))
                    {
                        rect.height = used.y + titleRect.height + PV.Margin * 3;
                        rectChanged = true;
                    }
                }

                if (rectChanged) calcRectSize();
            }

            autoSize.x = (count.x > 0) ? (contentRect.width - used.x) / colums : contentRect.width;
            autoSize.y = (count.y > 0) ? (contentRect.height - used.y) / (float)Math.Ceiling(count.y / colums) : contentRect.height;
        }

        private void resizeAllChildren(Container parent)
        {
            if (parent == null) return;

            foreach (Element child in parent)
            {
                if (child is Container) resizeAllChildren(child as Container);
                else child.Resize(true);
            }
        }

        #endregion

    }


    public class HSlider : Element
    {
        public GUIStyle Style = "horizontalSlider";
        public GUIStyle ThumbStyle = "horizontalSliderThumb";
        public float Value;
        public float Min;
        public float Max;

        public event EventHandler<SliderEventArgs> OnChange;

        public HSlider(string name, Rect rect, float min, float max, float def, EventHandler<SliderEventArgs> _OnChange) : base(name, rect)
        {
            this.Value = def;
            this.Min = min;
            this.Max = max;
            this.OnChange += _OnChange;
        }

        public override void Draw(Rect outRect)
        {
            onChange(GUI.HorizontalSlider(outRect, Value, Min, Max, Style, ThumbStyle));
        }

        private void onChange(float newValue)
        {
            if (newValue != Value)
            {
                OnChange(this, new SliderEventArgs(name, newValue));
                Value = newValue;
            }
        }
    }

    public class Toggle : Element
    {
        private bool val;

        public GUIStyle Style = "toggle";
        public GUIContent Content;
        public bool Value { get { return val; } set { val = value; } }
        public string Text { get { return Content.text; } set { Content.text = value; } }

        public event EventHandler<ToggleEventArgs> OnChange;

        public Toggle(string name, Rect rect, EventHandler<ToggleEventArgs> _OnChange) : this(name, rect, false, "", _OnChange) { }
        public Toggle(string name, Rect rect, bool def, EventHandler<ToggleEventArgs> _OnChange) : this(name, rect, def, "", _OnChange) { }
        public Toggle(string name, Rect rect, string text, EventHandler<ToggleEventArgs> _OnChange) : this(name, rect, false, text, _OnChange) { }
        public Toggle(string name, Rect rect, bool def, string text, EventHandler<ToggleEventArgs> _OnChange) : base(name, rect)
        {
            this.val = def;
            this.Content = new GUIContent(text);
            this.OnChange += _OnChange;
        }

        public override void Draw(Rect outRect)
        {
            onChange(GUI.Toggle(outRect, Value, Content, Style));
        }

        private void onChange(bool newValue)
        {
            if (newValue != val) OnChange(this, new ToggleEventArgs(name, newValue));
            val = newValue;
        }
    }

    public class SelectButton : Element
    {
        private string[] buttonNames;
        private int selected = 0;

        public int SelectedIndex { get { return selected; } }
        public string Value { get { return buttonNames[selected]; } }

        public event EventHandler<SelectEventArgs> OnSelect;

        public SelectButton(string name, Rect rect, string[] buttonNames, EventHandler<SelectEventArgs> _onSelect) : base(name, rect)
        {
            this.buttonNames = buttonNames;
            this.OnSelect += _onSelect;
        }

        public override void Draw(Rect outRect)
        {
            onSelect(GUI.Toolbar(outRect, selected, buttonNames));
        }

        private void onSelect(int newSelected)
        {
            if (selected != newSelected)
            {
                OnSelect(this, new SelectEventArgs(name, newSelected, buttonNames[newSelected]));
                selected = newSelected;
            }
        }
    }


    public class ElementEventArgs : EventArgs
    {
        public string Name;
        public bool SizeChanged;
        public bool VisibleChanged;

        public ElementEventArgs(string name, bool sizeChanged, bool visibleChanged)
        {
            this.Name = name;
            this.SizeChanged = sizeChanged;
            this.VisibleChanged = visibleChanged;
        }
    }

    public class SliderEventArgs : EventArgs
    {
        public string Name;
        public float Value;

        public SliderEventArgs(string name, float value)
        {
            this.Name = name;
            this.Value = value;
        }
    }

    public class ButtonEventArgs : EventArgs
    {
        public string Name;
        public string ButtonName;

        public ButtonEventArgs(string name, string buttonName)
        {
            this.Name = name;
            this.ButtonName = buttonName;
        }
    }

    public class ToggleEventArgs : EventArgs
    {
        public string Name;
        public bool Value;

        public ToggleEventArgs(string name, bool b)
        {
            this.Name = name;
            this.Value = b;
        }
    }

    public class SelectEventArgs : EventArgs
    {
        public string Name;
        public int Index;
        public string ButtonName;

        public SelectEventArgs(string name, int idx, string buttonName)
        {
            this.Name = name;
            this.Index = idx;
            this.ButtonName = buttonName;
        }
    }


    public static class PixelValuesCM3D2
    {

        #region Variables

        private static int margin = 10;
        private static Dictionary<string, int> font = new Dictionary<string, int>();
        private static Dictionary<string, int> line = new Dictionary<string, int>();
        private static Dictionary<string, int> sys = new Dictionary<string, int>();

        public static float BaseWidth = 1280f;
        public static float PropRatio = 0.6f;
        public static int Margin { get { return PropPx(margin); } set { margin = value; } }

        #endregion



        #region Methods

        static PixelValuesCM3D2()
        {
            font["C1"] = 12;
            font["C2"] = 11;
            font["H1"] = 20;
            font["H2"] = 16;
            font["H3"] = 14;

            line["C1"] = 18;
            line["C2"] = 14;
            line["H1"] = 30;
            line["H2"] = 24;
            line["H3"] = 22;

            sys["Menu.Height"] = 45;
            sys["OkButton.Height"] = 95;

            sys["HScrollBar.Width"] = 15;
        }

        public static int Font(string key) { return PropPx(font[key]); }
        public static int Line(string key) { return PropPx(line[key]); }
        public static int Sys(string key) { return PropPx(sys[key]); }

        public static int Font_(string key) { return font[key]; }
        public static int Line_(string key) { return line[key]; }
        public static int Sys_(string key) { return sys[key]; }

        public static Rect PropScreen(Rect ratio)
        {
            return new Rect((Screen.width - Margin * 2) * ratio.x + Margin
                           , (Screen.height - Margin * 2) * ratio.y + Margin
                           , (Screen.width - Margin * 2) * ratio.width
                           , (Screen.height - Margin * 2) * ratio.height);
        }

        public static Rect PropScreenMH(Rect ratio)
        {
            Rect r = PropScreen(ratio);
            r.y += Sys("Menu.Height");
            r.height -= (Sys("Menu.Height") + Sys("OkButton.Height"));

            return r;
        }

        public static Rect PropScreenMH(float left, float top, float width, float height, Vector2 last)
        {
            Rect r = PropScreen(new Rect((float)(left / (last.x - Margin * 2)), (float)(top / (last.y - Margin * 2)), width, height));
            r.height -= (Sys("Menu.Height") + Sys("OkButton.Height"));

            return r;
        }

        public static Rect InsideRect(Rect rect)
        {
            return new Rect(Margin, Margin, rect.width - Margin * 2, rect.height - Margin * 2);
        }

        public static Rect InsideRect(Rect rect, int height)
        {
            return new Rect(Margin, Margin, rect.width - Margin * 2, height);
        }

        public static Rect InsideRect(Rect rect, Rect padding)
        {
            return new Rect(rect.x + padding.x, rect.y + padding.x, rect.width - padding.width * 2, rect.height - padding.height * 2);
        }

        public static int PropPx(int px)
        {
            return (int)(px * (1f + (Screen.width / BaseWidth - 1f) * PropRatio));
        }

        public static Rect PropRect(int px)
        {
            return new Rect(PropPx(px), PropPx(px), PropPx(px), PropPx(px));
        }
        #endregion

    }

}