using System.Collections.Generic;
using Plemu.Content;
using Plemu.Core;
using Plemu.Emulation;
using UnityEngine;
using UnityEngine.UI;

namespace Plemu.App
{
    /// <summary>
    /// The minimal editor UI shell (UGUI, keyboard-navigable): Boot → Library → Detail → Player.
    /// It builds its whole canvas hierarchy at runtime so the scene file stays a single bootstrap
    /// object, and it is the only place that turns a user's "Play" into a
    /// <see cref="LaunchGate.Authorize"/> call — a denied decision shows an on-screen reason and
    /// never reaches the emulator; an authorized ticket boots <see cref="EmulationSession"/>.
    /// </summary>
    /// <remarks>
    /// Not the full product UI — enough to navigate by keyboard and prove the money-shot. Screens
    /// are plain panels toggled active; navigation is arrow keys + Enter/Z (select) + Esc (back).
    /// </remarks>
    public sealed class PlemuApp : MonoBehaviour
    {
        private enum Screen
        {
            Boot,
            Library,
            Detail,
            Player,
        }

        private readonly IClock _clock = new SystemClock();

        private Canvas _canvas;
        private GameObject _bootPanel;
        private GameObject _libraryPanel;
        private GameObject _detailPanel;
        private GameObject _playerPanel;

        private Text _bootStatus;
        private Text _detailTitle;
        private Text _detailBody;
        private Text _playerStatus;
        private RawImage _videoImage;
        private RectTransform _videoArea;

        private readonly List<CatalogEntry> _catalog = new List<CatalogEntry>();
        private readonly List<Button> _cardButtons = new List<Button>();
        private int _selectedIndex;
        private Screen _screen = Screen.Boot;

        private EmulationSession _session;
        private VideoSurface _video;
        private AudioSurface _audio;
        private EmulationInputSource _input;

        private void Start()
        {
            BuildCanvas();
            LoadCatalogIntoLibrary();
            ShowScreen(Screen.Boot);
        }

        private void Update()
        {
            switch (_screen)
            {
                case Screen.Boot:
                    if (AnyConfirm())
                        ShowScreen(Screen.Library);
                    break;
                case Screen.Library:
                    HandleLibraryInput();
                    break;
                case Screen.Detail:
                    if (AnyConfirm())
                        Play();
                    else if (Input.GetKeyDown(KeyCode.Escape))
                        ShowScreen(Screen.Library);
                    break;
                case Screen.Player:
                    // Esc / RightShift(Select) returns to the library (Select is also a pad button,
                    // but Esc is the unambiguous "leave" key here).
                    if (Input.GetKeyDown(KeyCode.Escape))
                        StopPlaying();
                    else
                        UpdatePlayerStatus();
                    break;
            }
        }

        // --- navigation ----------------------------------------------------------------------

        private void HandleLibraryInput()
        {
            if (_catalog.Count == 0)
                return;

            if (Input.GetKeyDown(KeyCode.DownArrow))
                SetSelection(_selectedIndex + 1);
            else if (Input.GetKeyDown(KeyCode.UpArrow))
                SetSelection(_selectedIndex - 1);
            else if (AnyConfirm())
                ShowScreen(Screen.Detail);
        }

        private static bool AnyConfirm()
        {
            return Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Z);
        }

        private void SetSelection(int index)
        {
            if (_catalog.Count == 0)
                return;
            _selectedIndex = (index % _catalog.Count + _catalog.Count) % _catalog.Count;
            for (int i = 0; i < _cardButtons.Count; i++)
            {
                ColorBlock colors = _cardButtons[i].colors;
                colors.normalColor = i == _selectedIndex
                    ? new Color(0.18f, 0.42f, 0.75f)
                    : new Color(0.15f, 0.15f, 0.18f);
                _cardButtons[i].colors = colors;
            }
        }

        private void ShowScreen(Screen screen)
        {
            _screen = screen;
            _bootPanel.SetActive(screen == Screen.Boot);
            _libraryPanel.SetActive(screen == Screen.Library);
            _detailPanel.SetActive(screen == Screen.Detail);
            _playerPanel.SetActive(screen == Screen.Player);

            if (screen == Screen.Detail)
                PopulateDetail();
        }

        // --- data ----------------------------------------------------------------------------

        private void LoadCatalogIntoLibrary()
        {
            _catalog.Clear();
            _catalog.AddRange(ContentPaths.LoadCatalog(_clock));
            BuildLibraryCards();

            _bootStatus.text = _catalog.Count > 0
                ? $"Plemu — trust-gated emulation\n\n{_catalog.Count} title(s) verified.\n\nPress Enter to continue."
                : "Plemu\n\nNo verified titles found.\nRun stage-content.sh, then reopen.";
        }

        private void PopulateDetail()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _catalog.Count)
                return;
            CatalogEntry entry = _catalog[_selectedIndex];
            _detailTitle.text = entry.Title;
            _detailBody.text =
                $"System: {entry.System}\nLicense: [{entry.License}]\nId: {entry.Id}\n\n" +
                "Press Enter / Z to Play (through LaunchGate).\nEsc to go back.";
        }

        // --- play (through the gate) ---------------------------------------------------------

        private void Play()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _catalog.Count)
                return;
            CatalogEntry entry = _catalog[_selectedIndex];

            LaunchDecision decision = LaunchGate.Authorize(
                entry.Id,
                ContentPaths.ManifestSource(),
                ContentPaths.ContentStore(),
                _clock);

            if (!decision.IsAuthorized)
            {
                // Fail-closed: surface the typed denial, do not touch the emulator.
                _detailBody.text =
                    $"DENIED: {decision.Denial.Reason}\n{decision.Denial.Detail}\n\nEsc to go back.";
                return;
            }

            StartPlaying(decision.Ticket);
        }

        private void StartPlaying(LaunchTicket ticket)
        {
            ShowScreen(Screen.Player);

            _session = new EmulationSession();
            try
            {
                _session.Load(ticket);
            }
            catch (EmulationException ex)
            {
                _playerStatus.text = $"Load failed: {ex.Message}";
                _session.Dispose();
                _session = null;
                return;
            }

            _video.Bind(_session);
            _audio.Bind(_session);
            _input.Bind(_session);

            AudioSource src = _audio.GetComponent<AudioSource>();
            if (src != null && !src.isPlaying)
                src.Play();

            _playerStatus.text = $"{ticket.TitleId}   (Esc = back)";
        }

        private void StopPlaying()
        {
            _video.Bind(null);
            _audio.Bind(null);
            _input.Bind(null);

            AudioSource src = _audio.GetComponent<AudioSource>();
            if (src != null && src.isPlaying)
                src.Stop();

            _session?.Dispose();
            _session = null;

            ShowScreen(Screen.Library);
        }

        private void UpdatePlayerStatus()
        {
            if (_session != null && _session.IsRunning)
                _playerStatus.text = $"{_session.Ticket.TitleId}   frames:{_session.FramesRun}   (Esc = back)";
        }

        private void OnDestroy()
        {
            _session?.Dispose();
            _session = null;
        }

        // --- UGUI construction (built at runtime) --------------------------------------------

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("PlemuCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1240, 1080); // PlaySolana PSG1 portrait display
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            _bootPanel = BuildBootPanel();
            _libraryPanel = BuildLibraryPanel();
            _detailPanel = BuildDetailPanel();
            _playerPanel = BuildPlayerPanel();
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
                return;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        private GameObject BuildBootPanel()
        {
            GameObject panel = MakePanel("Boot", new Color(0.06f, 0.06f, 0.09f));
            _bootStatus = MakeText(panel.transform, "BootStatus", "Plemu", 34, TextAnchor.MiddleCenter);
            Stretch(_bootStatus.rectTransform, 40, 40, 40, 40);
            return panel;
        }

        private GameObject BuildLibraryPanel()
        {
            GameObject panel = MakePanel("Library", new Color(0.08f, 0.08f, 0.11f));
            Text header = MakeText(panel.transform, "Header", "Library  (↑/↓ select · Enter open)", 28, TextAnchor.UpperLeft);
            TopBar(header.rectTransform, 24, 80);

            var listGo = new GameObject("CardList");
            listGo.transform.SetParent(panel.transform, false);
            var listRect = listGo.AddComponent<RectTransform>();
            Stretch(listRect, 24, 120, 24, 24);
            var layout = listGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(4, 4, 4, 4);
            _libraryListParent = listGo.transform;
            return panel;
        }

        private Transform _libraryListParent;

        private void BuildLibraryCards()
        {
            foreach (Button b in _cardButtons)
            {
                if (b != null)
                    Destroy(b.gameObject);
            }
            _cardButtons.Clear();

            for (int i = 0; i < _catalog.Count; i++)
            {
                CatalogEntry entry = _catalog[i];
                int index = i;

                var cardGo = new GameObject($"Card_{entry.Id}");
                cardGo.transform.SetParent(_libraryListParent, false);
                var img = cardGo.AddComponent<Image>();
                img.color = new Color(0.15f, 0.15f, 0.18f);
                var le = cardGo.AddComponent<LayoutElement>();
                le.preferredHeight = 110;
                var btn = cardGo.AddComponent<Button>();

                Text label = MakeText(cardGo.transform, "Label",
                    $"{entry.Title}\n<{entry.System}>  [{entry.License}]", 24, TextAnchor.MiddleLeft);
                Stretch(label.rectTransform, 18, 8, 18, 8);

                btn.onClick.AddListener(() =>
                {
                    _selectedIndex = index;
                    ShowScreen(Screen.Detail);
                });
                _cardButtons.Add(btn);
            }

            _selectedIndex = 0;
            SetSelection(0);
        }

        private GameObject BuildDetailPanel()
        {
            GameObject panel = MakePanel("Detail", new Color(0.07f, 0.09f, 0.12f));
            _detailTitle = MakeText(panel.transform, "Title", "Title", 34, TextAnchor.UpperLeft);
            TopBar(_detailTitle.rectTransform, 24, 90);
            _detailBody = MakeText(panel.transform, "Body", "", 24, TextAnchor.UpperLeft);
            Stretch(_detailBody.rectTransform, 24, 130, 24, 24);
            return panel;
        }

        private GameObject BuildPlayerPanel()
        {
            GameObject panel = MakePanel("Player", Color.black);

            var videoAreaGo = new GameObject("VideoArea");
            videoAreaGo.transform.SetParent(panel.transform, false);
            _videoArea = videoAreaGo.AddComponent<RectTransform>();
            Stretch(_videoArea, 12, 60, 12, 12);

            var imgGo = new GameObject("Video");
            imgGo.transform.SetParent(_videoArea, false);
            _videoImage = imgGo.AddComponent<RawImage>();
            var imgRect = _videoImage.rectTransform;
            imgRect.anchorMin = new Vector2(0.5f, 0.5f);
            imgRect.anchorMax = new Vector2(0.5f, 0.5f);
            imgRect.pivot = new Vector2(0.5f, 0.5f);
            imgRect.sizeDelta = new Vector2(160, 144);

            _playerStatus = MakeText(panel.transform, "Status", "", 22, TextAnchor.UpperLeft);
            TopBar(_playerStatus.rectTransform, 16, 44);

            // Emulation surfaces live on the panel; audio needs its own AudioSource.
            _video = panel.AddComponent<VideoSurface>();
            _video.Configure(_videoImage, _videoArea);

            var audioGo = new GameObject("Audio");
            audioGo.transform.SetParent(panel.transform, false);
            AudioSource audioSource = audioGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f;
            _audio = audioGo.AddComponent<AudioSurface>();

            _input = panel.AddComponent<EmulationInputSource>();
            return panel;
        }

        // --- tiny UGUI helpers ---------------------------------------------------------------

        private GameObject MakePanel(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);
            var rect = go.AddComponent<RectTransform>();
            Stretch(rect, 0, 0, 0, 0);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static Text MakeText(Transform parent, string name, string value, int size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void TopBar(RectTransform rect, float inset, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(inset, -height - inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
