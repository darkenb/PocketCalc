using System.Globalization;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PocketCalc
{
    public static class Expr
    {
        public const string DivideByZero = "Cannot divide by zero", Invalid = "Invalid expression", TooBig = "Result too large";

        // Returns null on success, otherwise the message to show. '*' and '/' bind tighter than '+' and '-'.
        public static string Eval(string s, out double value)
        {
            value = 0;
            double sum = 0, term = 1;
            char addOp = '+', mulOp = '*';
            int i = 0;
            while (true)
            {
                bool negative = false;
                for (; i < s.Length && (s[i] == '+' || s[i] == '-'); i++) negative ^= s[i] == '-';
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                if (i < s.Length && s[i] == 'E')  // a previous result can come back in scientific notation
                {
                    i++;
                    if (i < s.Length && (s[i] == '+' || s[i] == '-')) i++;
                    while (i < s.Length && char.IsDigit(s[i])) i++;
                }
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return Invalid;
                if (negative) number = -number;
                if (mulOp == '*') term *= number;
                else if (number == 0) return DivideByZero;
                else term /= number;
                if (i == s.Length) break;
                char op = s[i++];
                if (op == '*' || op == '/') mulOp = op;
                else if (op == '+' || op == '-')
                {
                    sum += addOp == '+' ? term : -term;
                    addOp = op;
                    term = 1;
                    mulOp = '*';
                }
                else return Invalid;
            }
            value = sum + (addOp == '+' ? term : -term);
            return double.IsNaN(value) || double.IsInfinity(value) ? TooBig : null;
        }

        public static string Format(double v) => v == 0 ? "0" : v.ToString("G15", CultureInfo.InvariantCulture);  // 15 digits hide binary noise: 0.1+0.2 shows 0.3
    }

    public class Calculator
    {
        const int MaxLength = 60;
        double? answer;  // kept apart from the typed text so 1/3 then *3 continues from full precision
        string typed = "", last = "";
        public string Error { get; private set; }
        public string Shown => (answer.HasValue ? Expr.Format(answer.Value) : "") + typed;
        public string Top => Error ?? last;

        public void Press(char c)
        {
            if (c >= '0' && c <= '9') { StartNumber(); if (typed.Length < MaxLength) typed += c; }
            else if (c == '.' || c == ',') Decimal();
            else if (c == '+' || c == '-' || c == '*' || c == '/') Operator(c);
            else if (c == '=') Enter();
            else if (c == '\b') { Error = null; if (typed.Length > 0) typed = typed.Substring(0, typed.Length - 1); else { answer = null; last = ""; } }
        }

        void StartNumber()
        {
            Error = null;
            if (typed.Length == 0) answer = null;
        }

        void Decimal()
        {
            StartNumber();
            int start = typed.LastIndexOfAny(new[] { '+', '-', '*', '/' }) + 1;
            if (typed.IndexOf('.', start) >= 0 || typed.Length >= MaxLength) return;
            typed += start == typed.Length ? "0." : ".";
        }

        void Operator(char c)
        {
            Error = null;
            char end = typed.Length > 0 ? typed[typed.Length - 1] : '\0';
            if (c == '-' && (end == '*' || end == '/')) { typed += c; return; }  // 5*-2
            typed = typed.TrimEnd('+', '-', '*', '/');
            if (Shown.Length == 0 && c != '-') return;
            if (typed.Length < MaxLength) typed += c;
        }

        public void Enter()
        {
            string body = typed.TrimEnd('+', '-', '*', '/');
            if (body.Length == 0) { typed = ""; return; }
            string text = (answer.HasValue ? answer.Value.ToString("R", CultureInfo.InvariantCulture) : "") + body;
            Error = Expr.Eval(text, out double value);
            if (Error != null) return;
            last = (answer.HasValue ? Expr.Format(answer.Value) : "") + body + " =";
            answer = value;
            typed = "";
        }

        public void Clear()
        {
            answer = null;
            typed = last = "";
            Error = null;
        }
    }

    [BepInPlugin("bug.dsp.pocketcalc", "PocketCalc", "1.3.2")]
    public class Plugin : BaseUnityPlugin
    {
        const int ShownChars = 30;
        const float Width = 304f, Height = 372f, Left = 20f, Key = 60f, KeyStep = 68f, RowHeight = 38f, RowStep = 46f, FirstRow = -130f;
        static readonly string[] DigitRows = { "789*", "456-", "123+" };
        // Tokens from the game's own window: panel teal, line cyan, selection orange. Glass order: rest, hover, pressed, text.
        static readonly Color Ink = new Color32(232, 246, 252, 255), Soft = new Color32(176, 208, 220, 255), Accent = new Color32(253, 150, 94, 255), AccentInk = new Color32(28, 14, 6, 255), Scrim = new Color32(4, 22, 30, 168), Well = new Color32(0, 10, 16, 120), Line = new Color32(167, 233, 255, 40);
        static readonly Color[] Glass = { new Color32(167, 233, 255, 56), new Color32(167, 233, 255, 92), new Color32(167, 233, 255, 32), Ink };
        static readonly Color[] Operator = { new Color32(167, 233, 255, 104), new Color32(167, 233, 255, 140), new Color32(167, 233, 255, 72), Ink };
        static readonly Color[] Equals_ = { Accent, new Color32(255, 174, 128, 255), new Color32(226, 128, 76, 255), AccentInk };
        static readonly Color[] ClearKey = { new Color32(253, 110, 80, 70), new Color32(253, 110, 80, 108), new Color32(253, 110, 80, 46), new Color32(255, 222, 210, 255) };
        public static bool Blocking, PassThrough;  // PassThrough: this mod's own reads see the real keyboard
        public static bool OtherFieldFocused;
        static ConfigEntry<KeyboardShortcut> hotkey;
        static readonly Calculator calc = new Calculator();
        static KeyCode waitRelease;  // keeps blocking until the closing key is up, or the game would see it as a fresh press
        static GameObject win;
        static Text topText, mainText;

        static bool Open => win != null && win.activeInHierarchy;

        // Keyboard only; mouse and modifiers stay with the game so shift-click and camera drag keep working.
        public static bool IsBlocked(KeyCode key) => key < KeyCode.Mouse0 && (key < KeyCode.RightShift || key > KeyCode.AltGr);

        void Awake()
        {
            hotkey = Config.Bind("General", "Hotkey", new KeyboardShortcut(KeyCode.C, KeyCode.LeftAlt), "Opens and closes the calculator.");
            new Harmony("bug.dsp.pocketcalc").PatchAll(typeof(Patches));
        }

        static void Close(KeyCode pressed)
        {
            win.SetActive(false);
            waitRelease = pressed;
        }

        void Update()
        {
            // Paused means the Esc menu; the game UI is closed on the main menu and while loading.
            if (!GameMain.isRunning || DSPGame.IsMenuDemo || GameMain.isPaused || !UIRoot.instance.uiGame.active)
            {
                if (win != null) win.SetActive(false);
                Blocking = false;
                waitRelease = KeyCode.None;
                return;
            }
            PassThrough = true;
            if (waitRelease != KeyCode.None && !Input.GetKey(waitRelease)) waitRelease = KeyCode.None;
            if (!OtherFieldFocused)
            {
                if (hotkey.Value.IsDown())
                {
                    if (Open) Close(hotkey.Value.MainKey);
                    else
                    {
                        if (win == null) Build();
                        win.SetActive(true);
                        win.transform.SetAsLastSibling();
                    }
                }
                else if (Open)
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) Close(KeyCode.Escape);
                    else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) calc.Enter();
                    else if (Input.GetKeyDown(KeyCode.Delete)) calc.Clear();
                    else foreach (char c in Input.inputString) calc.Press(c);  // layout-aware: Turkish Q has '*' and '+' on other keys than KeyCode says
                }
            }
            PassThrough = false;
            Blocking = Open || waitRelease != KeyCode.None;
            if (Open) ShowState();
        }

        static string Tail(string s) => s.Length > ShownChars ? "…" + s.Substring(s.Length - ShownChars) : s;

        static void ShowState()
        {
            topText.text = Tail(calc.Top);
            topText.color = calc.Error != null ? new Color32(255, 150, 130, 255) : Soft;
            mainText.text = calc.Shown.Length == 0 ? "0" : Tail(calc.Shown);
            mainText.fontSize = mainText.text.Length <= 14 ? 28 : mainText.text.Length <= 20 ? 20 : 16;  // long results shrink instead of leaving the display
        }

        // Same frame and templates as PowerPlantCalc: the generator window with its contents and behaviour removed.
        static void Build()
        {
            var src = UIRoot.instance.uiGame.generatorWindow.transform;
            var buttonTpl = src.Find("ray-receiver/switch-button-1").gameObject;
            var labelTpl = src.Find("power-network-desc/desc-group/label-1").GetComponent<Text>();
            var valueTpl = src.Find("power-network-desc/desc-group/value-1").GetComponent<Text>();

            win = Object.Instantiate(src.gameObject, src.parent, false);
            win.name = "Calculator";
            Object.DestroyImmediate(win.GetComponent<UIPowerGeneratorWindow>());
            foreach (Transform c in win.transform) if (c.name != "panel-bg") Object.Destroy(c.gameObject);
            // One close button left: the box was sized for two (64 wide), so shrink it to the close button and keep its right edge.
            var box = (RectTransform)win.transform.Find("panel-bg/btn-box");
            UnityEngine.Object.DestroyImmediate(box.Find("menu-wnd-btn").gameObject);
            box.sizeDelta = new Vector2(32f, box.sizeDelta.y);
            box.anchoredPosition += new Vector2(16f, 0f);
            ((RectTransform)box.Find("close-wnd-btn")).anchoredPosition = Vector2.zero;
            StripLocalizers(win);
            Backdrop(win.transform.Find("panel-bg"));
            var close = win.transform.Find("panel-bg/btn-box/close-wnd-btn").GetComponent<Button>();
            close.onClick = new Button.ButtonClickedEvent();
            close.onClick.AddListener(() => win.SetActive(false));
            var title = win.transform.Find("panel-bg/title-text").GetComponent<Text>();
            title.text = "Calculator";
            title.rectTransform.sizeDelta = new Vector2(200f, title.rectTransform.sizeDelta.y);
            var root = (RectTransform)win.transform;
            root.anchoredPosition = new Vector2(400f, 250f);
            root.sizeDelta = new Vector2(Width, Height);

            Card(root, new Vector2(Left, -50f), new Vector2(Width - 2 * Left, 68f), Well);
            topText = NewText(labelTpl, root, new Vector2(Left + 10f, -54f), new Vector2(Width - 2 * Left - 20f, 18f));
            topText.fontSize = 13;
            mainText = NewText(valueTpl, root, new Vector2(Left + 10f, -74f), new Vector2(Width - 2 * Left - 20f, 38f));
            mainText.color = Ink;

            // The classic layout: operators down the right, equals at the bottom corner.
            float wide = 2 * Key + (KeyStep - Key);
            NewButton(buttonTpl, root, new Vector2(Left, FirstRow), new Vector2(wide, RowHeight), "Clear (Del)", 14, ClearKey, calc.Clear);
            NewButton(buttonTpl, root, new Vector2(Left + 2 * KeyStep, FirstRow), new Vector2(Key, RowHeight), "Back", 14, Glass, () => calc.Press('\b'));
            NewButton(buttonTpl, root, new Vector2(Left + 3 * KeyStep, FirstRow), new Vector2(Key, RowHeight), "/", 20, Operator, () => calc.Press('/'));
            for (int y = 0; y < DigitRows.Length; y++)
                for (int x = 0; x < 4; x++)
                {
                    char key = DigitRows[y][x];
                    NewButton(buttonTpl, root, new Vector2(Left + x * KeyStep, FirstRow - (y + 1) * RowStep), new Vector2(Key, RowHeight), key.ToString(), 20, x == 3 ? Operator : Glass, () => calc.Press(key));
                }
            float last = FirstRow - 4 * RowStep;
            NewButton(buttonTpl, root, new Vector2(Left, last), new Vector2(wide, RowHeight), "0", 20, Glass, () => calc.Press('0'));
            NewButton(buttonTpl, root, new Vector2(Left + 2 * KeyStep, last), new Vector2(Key, RowHeight), ".", 20, Glass, () => calc.Press('.'));
            NewButton(buttonTpl, root, new Vector2(Left + 3 * KeyStep, last), new Vector2(Key, RowHeight), "=", 22, Equals_, calc.Enter);
            win.SetActive(false);
        }

        static Text NewText(Text tpl, Transform parent, Vector2 pos, Vector2 size)
        {
            var t = Object.Instantiate(tpl, parent, false);
            StripLocalizers(t.gameObject);
            Place(t.rectTransform, pos, size);
            t.material = null;  // the value template's material overdrives bright colours into a glow
            t.alignment = TextAnchor.MiddleRight;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static void NewButton(GameObject tpl, Transform parent, Vector2 pos, Vector2 size, string label, int fontSize, Color[] palette, UnityEngine.Events.UnityAction onClick)
        {
            var go = Object.Instantiate(tpl, parent, false);
            StripLocalizers(go);
            go.SetActive(true);
            Place((RectTransform)go.transform, pos, size);
            go.GetComponentInChildren<Text>().text = label;
            var b = go.GetComponent<Button>();
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(onClick);
            var ub = go.GetComponent<UIButton>();
            ub.tips.tipTitle = "";
            ub.tips.tipText = "";
            Style(go, palette, fontSize);
        }

        static void Style(GameObject go, Color[] palette, int fontSize)
        {
            var label = go.GetComponentInChildren<Text>();
            foreach (var t in go.GetComponent<UIButton>().transitions)
            {
                if (t.target == null) continue;
                bool isText = t.target is Text;
                t.normalColor = t.disabledColor = isText ? palette[3] : palette[0];
                t.mouseoverColor = isText ? palette[3] : palette[1];
                t.pressedColor = isText ? palette[3] : palette[2];
                t.highlightColorOverride = isText ? AccentInk : Accent;
                t.alphaOnly = false;
                t.mouseoverSize = t.pressedSize = t.highlightSizeMultiplier = 1f;
            }
            go.GetComponent<UIButton>().highlighted = false;  // the cloned template is the receiver's selected mode
            go.GetComponent<Button>().transition = Selectable.Transition.None;
            var fill = go.GetComponent<Image>();
            fill.material = null;  // the template's widget material boosts alpha, so colours would not read as written
            fill.color = palette[0];
            label.color = palette[3];
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = rt.sizeDelta = Vector2.zero;
        }

        // Deepens the game's glass so text holds on bright ground. The panel's own sprite keeps the cut corners; first child, so border, title and close button stay on top.
        static void Backdrop(Transform panel)
        {
            var bg = panel.GetComponent<Image>();
            var scrim = new GameObject("scrim", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            scrim.transform.SetParent(panel, false);
            scrim.transform.SetAsFirstSibling();
            scrim.rectTransform.anchorMin = Vector2.zero;
            scrim.rectTransform.anchorMax = Vector2.one;
            scrim.rectTransform.anchoredPosition = scrim.rectTransform.sizeDelta = Vector2.zero;
            scrim.sprite = bg.sprite;
            scrim.type = bg.type;
            scrim.color = Scrim;
            scrim.raycastTarget = false;
        }

        static Image Card(Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var image = new GameObject("card", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            Place(image.rectTransform, pos, size);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        // Cloned game texts would otherwise be reset to their original translation keys.
        static void StripLocalizers(GameObject go)
        {
            foreach (var l in go.GetComponentsInChildren<Localizer>(true)) Object.DestroyImmediate(l);
        }
    }

    public static class Patches
    {
        // Sets the game's own "the player is typing" flag, which gates nearly all its shortcuts.
        [HarmonyPostfix, HarmonyPatch(typeof(VFInput), nameof(VFInput.UpdateGameStates))]
        static void BlockGame()
        {
            if (UIRoot.instance == null) return;
            Plugin.OtherFieldFocused = VFInput.inputing;
            if (!Plugin.Blocking) return;
            VFInput.inputing = true;
            // Enter also fires Unity's "Submit" on the selected UI object, which would re-click the last pressed button.
            if (!Plugin.OtherFieldFocused && EventSystem.current.currentSelectedGameObject != null) EventSystem.current.SetSelectedGameObject(null);
        }

        // inputing is not enough: UIBeltBuildTip, UIGridSplit, UIDispenserWindow and many mods read keys without it.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Input), nameof(Input.GetKey), typeof(KeyCode))]
        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
        [HarmonyPatch(typeof(Input), nameof(Input.GetKeyUp), typeof(KeyCode))]
        static bool FilterKey(KeyCode key, ref bool __result)
        {
            if (!Plugin.Blocking || Plugin.PassThrough || !Plugin.IsBlocked(key)) return true;
            __result = false;
            return false;
        }
    }
}
