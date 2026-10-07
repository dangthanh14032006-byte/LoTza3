using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ============================================================
// Dữ liệu hình khối. '#' là ô có khối, '.' là ô trống.
// Viết từ trên xuống dưới.
// ============================================================
public static class BlockShapes
{
    static readonly string[][] All =
    {
        new[] { "#" },
        new[] { "##" },
        new[] { "###" },
        new[] { "####" },
        new[] { "#####" },
        new[] { "#", "#" },
        new[] { "#", "#", "#" },
        new[] { "#", "#", "#", "#" },
        new[] { "##", "##" },
        new[] { "###", "###", "###" },
        new[] { "#.", "#.", "##" },
        new[] { ".#", ".#", "##" },
        new[] { "##", "#.", "#." },
        new[] { "##", ".#", ".#" },
        new[] { "###", ".#." },
        new[] { ".#.", "###" },
        new[] { "##.", ".##" },
        new[] { ".##", "##." },
        new[] { "#.", "##" },
        new[] { "##", "#." },
    };

    public static bool[,] Parse(string[] rows)
    {
        int h = rows.Length;
        int w = 0;
        foreach (var s in rows) w = Mathf.Max(w, s.Length);

        var shape = new bool[h, w];
        for (int i = 0; i < h; i++)
            for (int j = 0; j < rows[i].Length; j++)
                shape[h - 1 - i, j] = rows[i][j] == '#';
        return shape;
    }

    public static bool[,] Random()
    {
        return Parse(All[UnityEngine.Random.Range(0, All.Length)]);
    }
}

// ============================================================
// Giao diện: điểm, điểm cao, chữ combo, màn hình Game Over.
// Tự tạo bằng code, không cần dựng gì trong Unity.
// ============================================================
public class GameUI : MonoBehaviour
{
    const float PopupTime = 1.2f;

    Font font;
    Text scoreText, bestText, popupText, overText, muteLabel;
    GameObject overPanel, menuPanel, pausePanel;
    Button pauseButton;
    Text menuBest;

    // BlockSpawner gán các hàm này để xử lý khi bấm nút
    public System.Action OnPlay, OnPause, OnResume, OnRestart, OnMenu;
    float popupTimer;

    public void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Cần EventSystem để bấm được nút (dùng Input System mới)
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var topCenter = new Vector2(0.5f, 1f);
        var center = new Vector2(0.5f, 0.5f);

        scoreText = MakeText("Score", canvasGo.transform, 120, topCenter, new Vector2(0, -150), new Vector2(900, 200));
        bestText = MakeText("Best", canvasGo.transform, 50, topCenter, new Vector2(0, -270), new Vector2(900, 100));
        bestText.color = new Color(1f, 1f, 1f, 0.6f);

        popupText = MakeText("Popup", canvasGo.transform, 100, center, new Vector2(0, 300), new Vector2(900, 300));
        popupText.color = new Color(1f, 0.85f, 0.3f, 1f);
        popupText.gameObject.SetActive(false);

        // Nút bật/tắt âm thanh (góc trên bên phải)
        var btnGo = new GameObject("MuteButton", typeof(RectTransform));
        btnGo.transform.SetParent(canvasGo.transform, false);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(1f, 1f, 1f, 0.12f);
        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(() => { AudioManager.ToggleMute(); RefreshMuteLabel(); });
        var brt = btnGo.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(1f, 1f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 1f);
        brt.anchoredPosition = new Vector2(-40f, -60f);
        brt.sizeDelta = new Vector2(340f, 100f);
        muteLabel = MakeText("Label", btnGo.transform, 38, center, Vector2.zero, new Vector2(340f, 100f));
        RefreshMuteLabel();

        // Nút tạm dừng (góc trên bên trái)
        pauseButton = MakeButton("Tạm dừng", canvasGo.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -60f), new Vector2(340f, 100f), new Color(1f, 1f, 1f, 0.12f), 38,
            () => OnPause?.Invoke());
        pauseButton.gameObject.SetActive(false);

        overPanel = new GameObject("GameOver", typeof(RectTransform));
        overPanel.transform.SetParent(canvasGo.transform, false);
        var img = overPanel.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.8f);
        var rt = overPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        overText = MakeText("OverText", overPanel.transform, 80, center, Vector2.zero, new Vector2(900, 1000));
        overPanel.SetActive(false);

        var accent = new Color(0.30f, 0.75f, 1f, 1f);

        // Màn hình tạm dừng
        pausePanel = MakePanel("Pause", canvasGo.transform, new Color(0f, 0f, 0f, 0.85f));
        MakeText("Title", pausePanel.transform, 110, center, new Vector2(0f, 380f), new Vector2(900f, 200f)).text = "TẠM DỪNG";
        MakeButton("Tiếp tục", pausePanel.transform, center, center, new Vector2(0f, 120f), new Vector2(560f, 130f), accent, 56, () => OnResume?.Invoke());
        MakeButton("Chơi lại", pausePanel.transform, center, center, new Vector2(0f, -50f), new Vector2(560f, 130f), new Color(1f, 1f, 1f, 0.18f), 56, () => OnRestart?.Invoke());
        MakeButton("Về menu", pausePanel.transform, center, center, new Vector2(0f, -220f), new Vector2(560f, 130f), new Color(1f, 1f, 1f, 0.18f), 56, () => OnMenu?.Invoke());
        pausePanel.SetActive(false);

        // Menu chính
        menuPanel = MakePanel("Menu", canvasGo.transform, new Color(0.06f, 0.08f, 0.16f, 0.97f));
        var title = MakeText("Title", menuPanel.transform, 150, center, new Vector2(0f, 420f), new Vector2(950f, 400f));
        title.text = "BLOCK\nBLAST";
        title.color = accent;
        menuBest = MakeText("Best", menuPanel.transform, 56, center, new Vector2(0f, 100f), new Vector2(900f, 100f));
        MakeButton("CHƠI", menuPanel.transform, center, center, new Vector2(0f, -120f), new Vector2(560f, 160f), accent, 80, () => OnPlay?.Invoke());
        menuPanel.SetActive(false);
    }

    Text MakeText(string name, Transform parent, int size, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        return t;
    }

    GameObject MakePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    Button MakeButton(string label, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos,
                      Vector2 size, Color bg, int fontSize, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label + "Btn", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(onClick);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        MakeText("Label", go.transform, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, size).text = label;
        return btn;
    }

    public void ShowMenu(int best)
    {
        menuBest.text = "Cao nhất: " + best;
        menuPanel.SetActive(true);
        pauseButton.gameObject.SetActive(false);
    }

    public void HideMenu()
    {
        menuPanel.SetActive(false);
        pauseButton.gameObject.SetActive(true);
    }

    public void ShowPause() { pausePanel.SetActive(true); }
    public void HidePause() { pausePanel.SetActive(false); }
    public void SetPauseVisible(bool visible) { pauseButton.gameObject.SetActive(visible); }

    void RefreshMuteLabel()
    {
        muteLabel.text = AudioManager.Muted ? "Âm thanh: Tắt" : "Âm thanh: Bật";
    }

    public void SetScore(int score, int best)
    {
        scoreText.text = score.ToString();
        bestText.text = "Cao nhất: " + best;
    }

    public void ShowPopup(string msg)
    {
        popupText.text = msg;
        popupText.gameObject.SetActive(true);
        popupTimer = PopupTime;
    }

    public void ShowGameOver(int score, int best, bool newRecord)
    {
        overText.text = "GAME OVER\n\nĐiểm: " + score + "\nCao nhất: " + best +
                        (newRecord ? "\n\nKỷ lục mới!" : "") +
                        "\n\nChạm để chơi lại";
        overPanel.SetActive(true);
    }

    void Update()
    {
        if (popupTimer <= 0f) return;
        popupTimer -= Time.deltaTime;
        if (popupTimer <= 0f)
        {
            popupText.gameObject.SetActive(false);
            return;
        }
        float alpha = Mathf.Clamp01(popupTimer / 0.5f);
        popupText.color = new Color(1f, 0.85f, 0.3f, alpha);
        popupText.rectTransform.anchoredPosition = new Vector2(0, 300 + (PopupTime - popupTimer) * 80f);
    }
}

// ============================================================
// Một khối có thể kéo thả (Input System mới, chạy cả chuột và cảm ứng)
// ============================================================
public class DraggableBlock : MonoBehaviour
{
    const float TrayScale = 0.55f;   // kích thước khối khi nằm ở khay
    const float LiftY = 1.8f;        // nhấc khối lên trên ngón tay để không bị che

    [System.NonSerialized] public bool[,] shape;
    public int cellCount;

    int w, h;
    GridManager grid;
    BlockSpawner spawner;
    Vector3 homePos;
    Color blockColor;
    bool dragging;
    static DraggableBlock active;    // chỉ cho kéo một khối một lúc

    // Bóng mờ gợi ý chỗ đặt
    GameObject ghost;
    SpriteRenderer[] ghostCells;

    public void Init(bool[,] s, Color color, GridManager g, BlockSpawner sp, Vector3 home)
    {
        shape = s;
        grid = g;
        spawner = sp;
        homePos = home;
        blockColor = color;
        h = shape.GetLength(0);
        w = shape.GetLength(1);

        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                if (!shape[r, c]) continue;
                cellCount++;
                var go = new GameObject("c");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(c - (w - 1) / 2f, r - (h - 1) / 2f, 0f);
                go.transform.localScale = Vector3.one * 0.92f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GridManager.GetSquareSprite();
                sr.color = color;
                sr.sortingOrder = 5;
            }
        }

        // Vùng bấm, làm to hơn khối một chút cho dễ chạm
        var box = gameObject.AddComponent<BoxCollider2D>();
        box.size = new Vector2(Mathf.Max(w, 2) + 0.5f, Mathf.Max(h, 2) + 0.5f);

        transform.position = homePos;
        transform.localScale = Vector3.one * TrayScale;
    }

    void OnDestroy()
    {
        if (active == this) active = null;
        DestroyGhost();
    }

    // ---------- Bóng mờ gợi ý ----------

    // Tìm ô trên lưới mà khối sẽ rơi vào. Trả về true nếu đặt được.
    bool GetTarget(out int row, out int col)
    {
        Vector3 origin = transform.position +
            new Vector3(-(w - 1) / 2f, -(h - 1) / 2f, 0f) * grid.cellSize;
        return grid.WorldToCell(origin, out row, out col) && grid.CanPlace(shape, row, col);
    }

    void CreateGhost()
    {
        ghost = new GameObject("Ghost");
        ghostCells = new SpriteRenderer[cellCount];
        int i = 0;
        for (int r = 0; r < h; r++)
        {
            for (int c = 0; c < w; c++)
            {
                if (!shape[r, c]) continue;
                var go = new GameObject("g");
                go.transform.SetParent(ghost.transform, false);
                go.transform.localScale = Vector3.one * (grid.cellSize - grid.gap);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GridManager.GetSquareSprite();
                sr.color = new Color(blockColor.r, blockColor.g, blockColor.b, 0.45f);
                sr.sortingOrder = 2;   // trên lưới, dưới khối đang kéo
                ghostCells[i++] = sr;
            }
        }
        ghost.SetActive(false);
    }

    void UpdateGhost()
    {
        if (ghost == null) return;
        if (GetTarget(out int row, out int col))
        {
            ghost.SetActive(true);
            int i = 0;
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    if (shape[r, c])
                        ghostCells[i++].transform.position = grid.CellToWorld(row + r, col + c);
        }
        else
        {
            ghost.SetActive(false);
        }
    }

    void DestroyGhost()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
    }

    void Update()
    {
        if (spawner == null || spawner.InputLocked) return;

        var p = Pointer.current;
        if (p == null || Camera.main == null) return;

        Vector3 world = Camera.main.ScreenToWorldPoint(p.position.ReadValue());
        world.z = 0f;

        if (!dragging && active == null && p.press.wasPressedThisFrame)
        {
            var hit = Physics2D.OverlapPoint(world);
            if (hit != null && hit.gameObject == gameObject)
            {
                dragging = true;
                active = this;
                transform.localScale = Vector3.one * grid.cellSize;
                CreateGhost();
                AudioManager.Play(Sfx.Pick);
            }
        }

        if (dragging)
        {
            transform.position = world + Vector3.up * LiftY;
            UpdateGhost();
            if (p.press.wasReleasedThisFrame)
            {
                dragging = false;
                active = null;
                DestroyGhost();
                TryPlace();
            }
        }
    }

    void TryPlace()
    {
        if (GetTarget(out int r, out int c))
        {
            grid.Place(shape, r, c, blockColor);
            AudioManager.Play(Sfx.Place);
            spawner.OnBlockPlaced(this);
            Destroy(gameObject);
        }
        else
        {
            // Đặt sai chỗ: trả về khay
            AudioManager.Play(Sfx.Invalid);
            transform.position = homePos;
            transform.localScale = Vector3.one * TrayScale;
        }
    }
}

// ============================================================
// Sinh 3 khối mỗi lượt, tính điểm, combo, Game Over, chơi lại
// ============================================================
public class BlockSpawner : MonoBehaviour
{
    const string BestKey = "BestScore";

    public GridManager grid;
    public float trayY = -5.5f;
    public float trayGap = 3.2f;

    enum State { Menu, Playing, Paused, GameOver }
    State state = State.Menu;
    static bool skipMenu;   // true khi bấm "Chơi lại": bỏ qua menu chính

    public bool InputLocked => state != State.Playing;

    DraggableBlock[] slots = new DraggableBlock[3];
    GameUI ui;
    int remaining;
    int score;
    int best;
    int bestAtStart;
    int streak;          // số lượt liên tiếp có xóa hàng/cột
    float gameOverTime;

    static readonly Color[] palette =
    {
        new Color(0.30f, 0.75f, 1.00f),
        new Color(1.00f, 0.55f, 0.30f),
        new Color(0.55f, 0.85f, 0.40f),
        new Color(0.95f, 0.40f, 0.60f),
        new Color(0.70f, 0.50f, 1.00f),
        new Color(1.00f, 0.85f, 0.30f),
    };

    void Start()
    {
        best = PlayerPrefs.GetInt(BestKey, 0);
        bestAtStart = best;

        AudioManager.Create();

        ui = new GameObject("GameUI").AddComponent<GameUI>();
        ui.Build();
        ui.SetScore(0, best);
        ui.OnPlay = StartGame;
        ui.OnPause = Pause;
        ui.OnResume = Resume;
        ui.OnRestart = Restart;
        ui.OnMenu = ToMenu;

        Time.timeScale = 1f;
        SpawnSet();

        if (skipMenu)
        {
            skipMenu = false;
            StartGame();
        }
        else
        {
            state = State.Menu;
            ui.ShowMenu(best);
        }
    }

    void StartGame()
    {
        state = State.Playing;
        ui.HideMenu();
    }

    void Pause()
    {
        if (state != State.Playing) return;
        state = State.Paused;
        Time.timeScale = 0f;
        ui.ShowPause();
    }

    void Resume()
    {
        if (state != State.Paused) return;
        state = State.Playing;
        Time.timeScale = 1f;
        ui.HidePause();
    }

    void Restart()
    {
        Time.timeScale = 1f;
        skipMenu = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void ToMenu()
    {
        Time.timeScale = 1f;
        skipMenu = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void Update()
    {
        // Phím Esc (hoặc nút Back trên Android): tạm dừng / tiếp tục
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (state == State.Playing) Pause();
            else if (state == State.Paused) Resume();
        }

        // Chạm màn hình sau khi thua để chơi lại
        if (state != State.GameOver || Time.time - gameOverTime < 0.6f) return;
        var p = Pointer.current;
        if (p != null && p.press.wasPressedThisFrame) Restart();
    }

    void SpawnSet()
    {
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("Block");
            var b = go.AddComponent<DraggableBlock>();
            var color = palette[Random.Range(0, palette.Length)];
            b.Init(BlockShapes.Random(), color, grid, this, new Vector3((i - 1) * trayGap, trayY, 0f));
            slots[i] = b;
        }
        remaining = 3;
    }

    public void OnBlockPlaced(DraggableBlock b)
    {
        int idx = System.Array.IndexOf(slots, b);
        if (idx >= 0) slots[idx] = null;

        int lines = grid.ClearFullLines();
        streak = lines > 0 ? streak + 1 : 0;

        int gained = b.cellCount;
        if (lines > 0)
            gained += lines * lines * 10 + (streak > 1 ? streak * 5 : 0);
        score += gained;

        if (score > best)
        {
            best = score;
            PlayerPrefs.SetInt(BestKey, best);
        }
        ui.SetScore(score, best);

        if (lines > 0)
        {
            string msg = lines == 1 ? "Tốt!" : lines == 2 ? "Tuyệt vời!" : "Siêu đỉnh!";
            if (streak > 1) msg += "\nCombo x" + streak;
            ui.ShowPopup(msg);
            AudioManager.Play(lines == 1 ? Sfx.Clear1 : lines == 2 ? Sfx.Clear2 : Sfx.Clear3);
        }

        remaining--;
        if (remaining == 0) SpawnSet();

        if (!AnyBlockCanPlace()) GameOver();
    }

    bool AnyBlockCanPlace()
    {
        foreach (var b in slots)
            if (b != null && grid.CanPlaceAnywhere(b.shape)) return true;
        return false;
    }

    void GameOver()
    {
        state = State.GameOver;
        ui.SetPauseVisible(false);
        gameOverTime = Time.time;
        PlayerPrefs.Save();
        ui.ShowGameOver(score, best, best > bestAtStart);
        AudioManager.Play(Sfx.GameOver);
    }
}

// ============================================================
// Âm thanh. Tất cả được tổng hợp bằng code nên không cần file nhạc.
// ============================================================
public enum Sfx { Pick, Place, Invalid, Clear1, Clear2, Clear3, GameOver }

public class AudioManager : MonoBehaviour
{
    const int SfxRate = 44100;
    const int MusicRate = 22050;
    const string MuteKey = "Muted";

    public static AudioManager Instance { get; private set; }

    static int muted = -1;
    public static bool Muted
    {
        get
        {
            if (muted < 0) muted = PlayerPrefs.GetInt(MuteKey, 0);
            return muted == 1;
        }
    }

    AudioSource sfxSource, musicSource;
    AudioClip[] clips;

    public static AudioManager Create()
    {
        var go = new GameObject("Audio");
        return go.AddComponent<AudioManager>();
    }

    public static void Play(Sfx sfx)
    {
        if (Instance != null) Instance.PlayInternal(sfx);
    }

    public static void ToggleMute()
    {
        muted = Muted ? 0 : 1;
        PlayerPrefs.SetInt(MuteKey, muted);
        if (Instance != null) Instance.ApplyMute();
    }

    void Awake()
    {
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.volume = 0.8f;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.5f;

        BuildClips();
        musicSource.clip = BuildMusic();
        ApplyMute();
        musicSource.Play();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void ApplyMute()
    {
        sfxSource.mute = Muted;
        musicSource.mute = Muted;
    }

    void PlayInternal(Sfx sfx)
    {
        if (Muted || clips == null) return;
        sfxSource.PlayOneShot(clips[(int)sfx]);
    }

    // ---------- Hiệu ứng âm thanh ----------

    void BuildClips()
    {
        clips = new AudioClip[7];

        clips[(int)Sfx.Pick] = Compose("pick", 0.12f, new[]
        {
            (700f, 0f, 0.08f, 0.35f),
        });

        clips[(int)Sfx.Place] = Compose("place", 0.2f, new[]
        {
            (150f, 0f, 0.15f, 0.7f),
            (300f, 0f, 0.06f, 0.3f),
        });

        clips[(int)Sfx.Invalid] = Compose("invalid", 0.3f, new[]
        {
            (220f, 0f, 0.1f, 0.4f),
            (165f, 0.08f, 0.12f, 0.4f),
        });

        clips[(int)Sfx.Clear1] = Compose("clear1", 0.7f, new[]
        {
            (523.25f, 0f, 0.35f, 0.4f),
            (659.25f, 0.07f, 0.35f, 0.4f),
            (783.99f, 0.14f, 0.4f, 0.4f),
        });

        clips[(int)Sfx.Clear2] = Compose("clear2", 0.8f, new[]
        {
            (523.25f, 0f, 0.35f, 0.4f),
            (659.25f, 0.07f, 0.35f, 0.4f),
            (783.99f, 0.14f, 0.4f, 0.4f),
            (1046.5f, 0.21f, 0.45f, 0.4f),
        });

        float[] penta = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f };
        var n3 = new (float, float, float, float)[penta.Length];
        for (int i = 0; i < penta.Length; i++)
            n3[i] = (penta[i], i * 0.06f, 0.5f, 0.35f);
        clips[(int)Sfx.Clear3] = Compose("clear3", 1.0f, n3);

        clips[(int)Sfx.GameOver] = Compose("gameover", 1.6f, new[]
        {
            (392f, 0f, 0.6f, 0.5f),
            (329.63f, 0.25f, 0.6f, 0.5f),
            (261.63f, 0.5f, 0.6f, 0.5f),
            (196f, 0.75f, 0.8f, 0.5f),
        });
    }

    static AudioClip Compose(string name, float duration, (float f, float start, float len, float vol)[] notes)
    {
        int n = Mathf.CeilToInt(duration * SfxRate);
        var data = new float[n];
        foreach (var note in notes)
            AddNote(data, SfxRate, note.f, note.start, note.len, note.vol, false);
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);

        var clip = AudioClip.Create(name, n, 1, SfxRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Thêm một nốt (sóng sin + hòa âm bậc 2, tắt dần) vào mảng mẫu âm thanh
    static void AddNote(float[] data, int rate, float freq, float start, float len, float vol, bool wrap)
    {
        int n = data.Length;
        int s0 = Mathf.RoundToInt(start * rate);
        int count = Mathf.RoundToInt(len * rate);
        float twoPiF = 2f * Mathf.PI * freq;

        for (int i = 0; i < count; i++)
        {
            int idx = s0 + i;
            if (idx >= n)
            {
                if (!wrap) break;
                idx %= n;
            }
            float t = (float)i / rate;
            float env = Mathf.Min(1f, t / 0.005f)                       // vào nhanh
                      * Mathf.Exp(-4f * i / count)                       // tắt dần
                      * Mathf.Clamp01((count - i) / (0.01f * rate));     // dứt êm
            float v = Mathf.Sin(twoPiF * t) + 0.25f * Mathf.Sin(2f * twoPiF * t);
            data[idx] += v * env * vol;
        }
    }

    // ---------- Nhạc nền: vòng 16 giây, nhẹ nhàng (Am - F - C - G) ----------

    AudioClip BuildMusic()
    {
        const float chordLen = 4f;
        float[][] chords =
        {
            new[] { 220f, 261.63f, 329.63f },   // Am
            new[] { 174.61f, 220f, 261.63f },   // F
            new[] { 261.63f, 329.63f, 392f },   // C
            new[] { 196f, 246.94f, 293.66f },   // G
        };
        int perChord = Mathf.RoundToInt(chordLen * MusicRate);
        int n = perChord * chords.Length;
        var data = new float[n];

        // Nền (pad): các hợp âm ngân dài, vào/ra từ từ
        for (int ci = 0; ci < chords.Length; ci++)
        {
            for (int i = 0; i < perChord; i++)
            {
                float t = (float)i / MusicRate;
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Min(t, chordLen - t) / 1.2f);
                float v = 0f;
                foreach (float f in chords[ci])
                    v += Mathf.Sin(2f * Mathf.PI * f * t);
                data[ci * perChord + i] = v * 0.08f * env;
            }
        }

        // Giai điệu thưa: các nốt gảy ngẫu nhiên (cố định) trên thang ngũ cung
        float[] scale = { 440f, 523.25f, 587.33f, 659.25f, 783.99f };
        var rng = new System.Random(7);
        int steps = Mathf.RoundToInt(chordLen * chords.Length / 0.5f);
        for (int step = 0; step < steps; step++)
        {
            if (rng.NextDouble() > 0.55) continue;
            AddNote(data, MusicRate, scale[rng.Next(scale.Length)], step * 0.5f, 1.5f, 0.12f, true);
        }

        var clip = AudioClip.Create("music", n, 1, MusicRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}