using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FlipRun.Game
{
    /// <summary>
    /// uGUI の画面（ラン中の表示・タイトル・リザルト）。数字中心、日本語のみ。
    /// 基準解像度 960×540。
    /// </summary>
    public sealed class GameHud
    {
        public GameObject Root { get; }
        public GameObject TitlePanel { get; }
        public GameObject ResultPanel { get; }
        public GameObject RunPanel { get; }

        readonly Text _distance, _coins, _score;
        readonly Text _titleBest;
        readonly Text _resultBody, _resultNew;
        readonly Text _nearMiss;
        readonly Text _muteLabel;
        float _nearMissTimer;

        /// <summary>ミュートボタンが押された。</summary>
        public event System.Action MuteClicked;
        public bool NearMissVisible => _nearMiss.gameObject.activeSelf;
        public const float NearMissShowTime = 0.5f;

        public GameHud(Transform parent)
        {
            Root = new GameObject("HUD");
            Root.transform.SetParent(parent, false);
            var canvas = Root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = Root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.matchWidthOrHeight = 0.5f;

            // ラン中：左上に距離とコイン、右上にスコア
            RunPanel = Panel("Run", false);
            _distance = Label(RunPanel, "Distance", 36, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24, -16), new Vector2(300, 50));
            _coins = Label(RunPanel, "Coins", 26, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(24, -60), new Vector2(300, 40));
            _score = Label(RunPanel, "Score", 36, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(-24, -16), new Vector2(300, 50));
            // 色反転で背景が明るくなっても読めるように縁取りする
            foreach (var t in new[] { _distance, _coins, _score }) AddOutline(t);
            _nearMiss = Label(RunPanel, "NearMiss", 40, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(600, 60));
            _nearMiss.color = GreyboxArt.Coin;
            var outline = _nearMiss.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);
            _nearMiss.gameObject.SetActive(false);

            // タイトル
            TitlePanel = Panel("Title", true);
            Label(TitlePanel, "Logo", 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(800, 100)).text = UiText.Logo;
            Label(TitlePanel, "Hint", 28, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(800, 40)).text = UiText.TitleHint;
            _titleBest = Label(TitlePanel, "Best", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(800, 40));

            // リザルト
            ResultPanel = Panel("Result", true);
            _resultBody = Label(ResultPanel, "Body", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(800, 220));
            _resultBody.lineSpacing = 1.1f;
            _resultNew = Label(ResultPanel, "New", 40, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(800, 60));
            _resultNew.text = UiText.New;
            _resultNew.color = GreyboxArt.Coin;
            Label(ResultPanel, "Hint", 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(800, 40)).text = UiText.RetryHint;

            // ミュート切替ボタン（右下）。キーボードの M でも切り替えられる
            _muteLabel = MuteButton();
            EnsureEventSystem();
        }

        public void SetMuted(bool muted) => _muteLabel.text = muted ? UiText.SoundOff : UiText.SoundOn;

        public void ShowNearMiss()
        {
            _nearMiss.text = $"{UiText.NearMiss} {UiText.NearMissBonus}";
            _nearMiss.gameObject.SetActive(true);
            _nearMissTimer = NearMissShowTime;
        }

        public void Tick(float dt)
        {
            if (_nearMissTimer <= 0f) return;
            _nearMissTimer -= dt;
            if (_nearMissTimer <= 0f) _nearMiss.gameObject.SetActive(false);
        }

        Text MuteButton()
        {
            var go = new GameObject("Mute", typeof(RectTransform));
            go.transform.SetParent(Root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-16f, 16f);
            rt.sizeDelta = new Vector2(110f, 40f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.35f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => MuteClicked?.Invoke());
            var label = Label(go, "Label", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 40f));
            label.text = UiText.SoundOn;
            return label;
        }

        static void AddOutline(Text t)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.08f, 0.1f, 0.23f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        public void ShowTitle(int best)
        {
            _titleBest.text = best > 0 ? $"{UiText.HighScore} {best}" : "";
            Set(title: true, run: false, result: false);
        }

        public void ShowRun()
        {
            _nearMiss.gameObject.SetActive(false);
            _nearMissTimer = 0f;
            Set(title: false, run: true, result: false);
        }

        public void UpdateRun(int meters, int coins, int score)
        {
            _distance.text = $"{meters}{UiText.Meter}";
            _coins.text = $"{UiText.CoinMark} {coins}";
            _score.text = score.ToString();
        }

        public void ShowResult(int meters, int coins, int score, int best, bool isNew)
        {
            _resultBody.text = $"{UiText.Distance} {meters}{UiText.Meter}\n{UiText.Coins} {coins}\n{UiText.Score} {score}\n{UiText.HighScore} {best}";
            _resultNew.gameObject.SetActive(isNew);
            Set(title: false, run: true, result: true);
        }

        /// <summary>テスト用：リザルトに表示している文字。</summary>
        public string ResultText => _resultBody.text;
        public bool ResultIsNew => _resultNew.gameObject.activeSelf;

        void Set(bool title, bool run, bool result)
        {
            TitlePanel.SetActive(title);
            RunPanel.SetActive(run);
            ResultPanel.SetActive(result);
        }

        GameObject Panel(string name, bool dim)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(Root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (dim)
            {
                var img = go.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.55f);
                img.raycastTarget = false;
            }
            return go;
        }

        static Font _font;
        /// <summary>Noto Sans JP（UIで使う文字だけを入れたもの）。見つからなければ内蔵フォント。</summary>
        public static Font UiFont
        {
            get
            {
                if (_font) return _font;
                _font = Resources.Load<Font>(UiText.FontResource);
                if (!_font)
                {
                    Debug.LogWarning("[FlipRun] UIフォントが見つかりません: Resources/" + UiText.FontResource);
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        static Text Label(GameObject parent, string name, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.AddComponent<Text>();
            t.font = UiFont;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
