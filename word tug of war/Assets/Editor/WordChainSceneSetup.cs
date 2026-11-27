using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WordChainSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private static readonly Color BgColor = new Color(0.043f, 0.051f, 0.110f, 1f);
    private static readonly Color CardColor = new Color(0.086f, 0.102f, 0.204f, 1f);
    private static readonly Color SurfaceColor = new Color(0.34f, 0.24f, 0.78f, 1f);
    private static readonly Color BorderColor = new Color(0.35f, 0.30f, 0.62f, 1f);
    private static readonly Color TextColor = new Color(0.96f, 0.97f, 1f, 1f);
    private static readonly Color MutedTextColor = new Color(0.62f, 0.56f, 1f, 1f);
    private static readonly Color TrackFillLeft = new Color(0.55f, 0.48f, 1f, 1f);
    private static readonly Color TrackFillRight = new Color(0.42f, 0.34f, 0.85f, 1f);
    private static readonly Color TickColor = new Color(0.28f, 0.24f, 0.50f, 1f);

    [MenuItem("Tools/Word Chain/Build Scene")]
    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = BgColor;
            mainCam.orthographic = true;
        }

        // Preserve existing GameManager component & audio/firebase settings if present
        GameObject gmGo = GameObject.Find("GameManager");
        if (gmGo == null)
        {
            gmGo = new GameObject("GameManager");
        }
        WordChainGame game = gmGo.GetComponent<WordChainGame>();
        if (game == null)
        {
            game = gmGo.AddComponent<WordChainGame>();
        }

        // Rebuild Canvas cleanly
        DestroyIfExists("Canvas");

        EnsureTmpEssentialResources();
        TMP_FontAsset fontAsset = ResolveFontAsset();
        if (fontAsset == null)
        {
            Debug.LogError("[WordChainSceneSetup] Could not resolve a TMP_FontAsset!");
        }

        EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            eventSystem = esGo.AddComponent<EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }
        else if (eventSystem.GetComponent<BaseInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        // Canvas
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Solid #111111 Background
        GameObject bgGo = CreateUIObject("Background", canvasGo.transform);
        StretchFull(bgGo.GetComponent<RectTransform>());
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = BgColor;
        bgImg.raycastTarget = false;

        // ---------------- ROLE / ENTRY PANEL ----------------
        GameObject rolePanel = CreateUIObject("RolePanel", canvasGo.transform);
        StretchFull(rolePanel.GetComponent<RectTransform>());
        rolePanel.SetActive(true);

        TMP_Text titleText = CreateTMPText("TitleText", rolePanel.transform, fontAsset,
            "WORD CHAIN TUG-OF-WAR", 54, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        SetRect(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 240f), new Vector2(1200f, 90f));

        TMP_Text subtitleText = CreateTMPText("SubtitleText", rolePanel.transform, fontAsset,
            "UP TO 8 PLAYERS  ·  TEAM BATTLE", 24, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(subtitleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 178f), new Vector2(900f, 44f));

        TMP_InputField playerNameInput = CreateTMPInputField("PlayerNameInput", rolePanel.transform, fontAsset,
            "ENTER YOUR NAME...", new Vector2(0f, 80f), new Vector2(560f, 74f));
        playerNameInput.characterLimit = 14;

        Button hostLobbyButton = CreateButton("HostLobbyButton", rolePanel.transform, fontAsset,
            "HOST LOBBY", new Vector2(0f, -20f), new Vector2(560f, 78f));

        TMP_InputField roomCodeInput = CreateTMPInputField("RoomCodeInput", rolePanel.transform, fontAsset,
            "LOBBY CODE...", new Vector2(-120f, -130f), new Vector2(320f, 74f));
        roomCodeInput.characterLimit = 6;

        Button joinLobbyButton = CreateButton("JoinLobbyButton", rolePanel.transform, fontAsset,
            "JOIN LOBBY", new Vector2(170f, -130f), new Vector2(220f, 74f));

        TMP_Text entryStatusText = CreateTMPText("EntryStatusText", rolePanel.transform, fontAsset,
            "Host a new lobby or enter a code to join.", 22, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(entryStatusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -225f), new Vector2(1000f, 50f));

        // ---------------- LOBBY PANEL ----------------
        GameObject lobbyPanel = CreateUIObject("LobbyPanel", canvasGo.transform);
        StretchFull(lobbyPanel.GetComponent<RectTransform>());
        lobbyPanel.SetActive(false);

        TMP_Text lobbyCodeText = CreateTMPText("LobbyCodeText", lobbyPanel.transform, fontAsset,
            "LOBBY CODE: ----", 48, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        SetRect(lobbyCodeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 360f), new Vector2(900f, 70f));

        TMP_Text lobbyPlayerCountText = CreateTMPText("LobbyPlayerCountText", lobbyPanel.transform, fontAsset,
            "PLAYERS: 1 / 8  ·  PICK A TEAM", 24, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(lobbyPlayerCountText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 298f), new Vector2(900f, 44f));

        // Team 1 Card
        GameObject t1Card = CreateCard("Team1Card", lobbyPanel.transform, new Vector2(-280f, 40f), new Vector2(480f, 400f));
        TMP_Text team1RosterText = CreateTMPText("Team1RosterText", t1Card.transform, fontAsset,
            "TEAM 1\n----------------\n(empty)", 26, FontStyles.Normal, TextAlignmentOptions.TopLeft, TextColor);
        SetRect(team1RosterText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 40f), new Vector2(410f, 260f));
        Button joinTeam1Button = CreateButton("JoinTeam1Button", t1Card.transform, fontAsset,
            "JOIN TEAM 1", new Vector2(0f, -145f), new Vector2(400f, 64f));

        // Team 2 Card
        GameObject t2Card = CreateCard("Team2Card", lobbyPanel.transform, new Vector2(280f, 40f), new Vector2(480f, 400f));
        TMP_Text team2RosterText = CreateTMPText("Team2RosterText", t2Card.transform, fontAsset,
            "TEAM 2\n----------------\n(empty)", 26, FontStyles.Normal, TextAlignmentOptions.TopLeft, TextColor);
        SetRect(team2RosterText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 40f), new Vector2(410f, 260f));
        Button joinTeam2Button = CreateButton("JoinTeam2Button", t2Card.transform, fontAsset,
            "JOIN TEAM 2", new Vector2(0f, -145f), new Vector2(400f, 64f));

        Button startGameButton = CreateButton("StartGameButton", lobbyPanel.transform, fontAsset,
            "START GAME", new Vector2(-120f, -245f), new Vector2(320f, 74f));
        Button leaveLobbyButton = CreateButton("LeaveLobbyButton", lobbyPanel.transform, fontAsset,
            "LEAVE", new Vector2(180f, -245f), new Vector2(200f, 74f));

        TMP_Text lobbyStatusText = CreateTMPText("LobbyStatusText", lobbyPanel.transform, fontAsset,
            "WAITING FOR PLAYERS...", 22, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(lobbyStatusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -330f), new Vector2(1100f, 50f));

        // ---------------- GAME PANEL ----------------
        GameObject gamePanel = CreateUIObject("GamePanel", canvasGo.transform);
        StretchFull(gamePanel.GetComponent<RectTransform>());
        gamePanel.SetActive(false);

        TMP_Text roundText = CreateTMPText("RoundText", gamePanel.transform, fontAsset,
            "ROOM ----  ·  ROUND 1", 22, FontStyles.Bold, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(roundText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 445f), new Vector2(900f, 40f));

        Button gameLeaveButton = CreateButton("GameLeaveButton", gamePanel.transform, fontAsset,
            "LEAVE", new Vector2(820f, 455f), new Vector2(150f, 50f));

        // Monochrome Tug-of-War Bar
        GameObject tugContainer = CreateUIObject("TugBarContainer", gamePanel.transform);
        SetRect(tugContainer.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 340f), new Vector2(960f, 110f));

        TMP_Text team1ScoreLabel = CreateTMPText("Team1ScoreLabel", tugContainer.transform, fontAsset,
            "TEAM 1 [-15]", 22, FontStyles.Bold, TextAlignmentOptions.Left, TextColor);
        SetRect(team1ScoreLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-310f, 36f), new Vector2(300f, 36f));

        TMP_Text pullValueLabel = CreateTMPText("PullValueLabel", tugContainer.transform, fontAsset,
            "EVEN (0)", 22, FontStyles.Bold, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(pullValueLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 36f), new Vector2(300f, 36f));

        TMP_Text team2ScoreLabel = CreateTMPText("Team2ScoreLabel", tugContainer.transform, fontAsset,
            "[+15] TEAM 2", 22, FontStyles.Bold, TextAlignmentOptions.Right, TextColor);
        SetRect(team2ScoreLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(310f, 36f), new Vector2(300f, 36f));

        // Track
        GameObject trackGo = CreateUIObject("TugTrack", tugContainer.transform);
        RectTransform trackRt = trackGo.GetComponent<RectTransform>();
        SetRect(trackRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -6f), new Vector2(920f, 26f));
        Image trackBg = trackGo.AddComponent<Image>();
        trackBg.color = CardColor;
        trackBg.raycastTarget = false;
        Outline trackOutline = trackGo.AddComponent<Outline>();
        trackOutline.effectColor = BorderColor;
        trackOutline.effectDistance = new Vector2(2f, -2f);

        // Left fill (grows left from center)
        GameObject leftFillGo = CreateUIObject("TugLeftFill", trackRt);
        RectTransform leftFillRt = leftFillGo.GetComponent<RectTransform>();
        leftFillRt.anchorMin = new Vector2(0.5f, 0f);
        leftFillRt.anchorMax = new Vector2(0.5f, 1f);
        leftFillRt.pivot = new Vector2(1f, 0.5f);
        leftFillRt.anchoredPosition = Vector2.zero;
        leftFillRt.sizeDelta = new Vector2(0f, 0f);
        Image leftFillImg = leftFillGo.AddComponent<Image>();
        leftFillImg.color = TrackFillLeft;
        leftFillImg.raycastTarget = false;

        // Right fill (grows right from center)
        GameObject rightFillGo = CreateUIObject("TugRightFill", trackRt);
        RectTransform rightFillRt = rightFillGo.GetComponent<RectTransform>();
        rightFillRt.anchorMin = new Vector2(0.5f, 0f);
        rightFillRt.anchorMax = new Vector2(0.5f, 1f);
        rightFillRt.pivot = new Vector2(0f, 0.5f);
        rightFillRt.anchoredPosition = Vector2.zero;
        rightFillRt.sizeDelta = new Vector2(0f, 0f);
        Image rightFillImg = rightFillGo.AddComponent<Image>();
        rightFillImg.color = TrackFillRight;
        rightFillImg.raycastTarget = false;

        // Tick marks (-2/3, -1/3, 0, +1/3, +2/3)
        float[] tickPositions = { -306f, -153f, 0f, 153f, 306f };
        for (int i = 0; i < tickPositions.Length; i++)
        {
            bool isCenter = Mathf.Approximately(tickPositions[i], 0f);
            GameObject tickGo = CreateUIObject(isCenter ? "CenterTick" : $"Tick_{i}", trackRt);
            RectTransform tickRt = tickGo.GetComponent<RectTransform>();
            SetRect(tickRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(tickPositions[i], 0f), new Vector2(isCenter ? 3f : 2f, isCenter ? 36f : 18f));
            Image tickImg = tickGo.AddComponent<Image>();
            tickImg.color = isCenter ? TextColor : TickColor;
            tickImg.raycastTarget = false;
        }

        // Marker knob
        GameObject markerGo = CreateUIObject("TugMarker", trackRt);
        RectTransform markerRt = markerGo.GetComponent<RectTransform>();
        SetRect(markerRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(14f, 38f));
        Image markerImg = markerGo.AddComponent<Image>();
        markerImg.color = TextColor;
        markerImg.raycastTarget = false;
        Outline markerOutline = markerGo.AddComponent<Outline>();
        markerOutline.effectColor = BgColor;
        markerOutline.effectDistance = new Vector2(2f, -2f);

        TMP_Text scoreText = CreateTMPText("ScoreText", gamePanel.transform, fontAsset,
            string.Empty, 22, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(scoreText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 255f), new Vector2(1100f, 40f));

        // Side Team Rosters
        TMP_Text gameTeam1RosterText = CreateTMPText("GameTeam1RosterText", gamePanel.transform, fontAsset,
            "TEAM 1", 22, FontStyles.Normal, TextAlignmentOptions.TopLeft, MutedTextColor);
        SetRect(gameTeam1RosterText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-680f, 20f), new Vector2(320f, 300f));

        TMP_Text gameTeam2RosterText = CreateTMPText("GameTeam2RosterText", gamePanel.transform, fontAsset,
            "TEAM 2", 22, FontStyles.Normal, TextAlignmentOptions.TopRight, MutedTextColor);
        SetRect(gameTeam2RosterText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(680f, 20f), new Vector2(320f, 300f));

        // Prompt
        TMP_Text promptText = CreateTMPText("PromptText", gamePanel.transform, fontAsset,
            "START\n<size=50%>NEXT LETTER: T</size>",
            84, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        SetRect(promptText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 70f), new Vector2(1100f, 220f));

        // Timer Bar + Text
        GameObject timerBgGo = CreateUIObject("TimerBarBg", gamePanel.transform);
        SetRect(timerBgGo.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -68f), new Vector2(680f, 8f));
        Image timerBgImg = timerBgGo.AddComponent<Image>();
        timerBgImg.color = SurfaceColor;
        timerBgImg.raycastTarget = false;

        GameObject timerBarGo = CreateUIObject("TimerBar", timerBgGo.transform);
        StretchFull(timerBarGo.GetComponent<RectTransform>());
        Image timerBar = timerBarGo.AddComponent<Image>();
        timerBar.color = TextColor;
        timerBar.type = Image.Type.Filled;
        timerBar.fillMethod = Image.FillMethod.Horizontal;
        timerBar.fillOrigin = (int)Image.OriginHorizontal.Left;
        timerBar.fillAmount = 1f;
        timerBar.raycastTarget = false;

        TMP_Text timerText = CreateTMPText("TimerText", gamePanel.transform, fontAsset,
            "60s", 22, FontStyles.Bold, TextAlignmentOptions.Right, TextColor);
        SetRect(timerText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(390f, -68f), new Vector2(80f, 34f));

        TMP_InputField wordInput = CreateTMPInputField("WordInput", gamePanel.transform, fontAsset,
            "TYPE WORD + ENTER (OR 'RESET')...", new Vector2(0f, -145f), new Vector2(680f, 84f));

        TMP_Text statusText = CreateTMPText("StatusText", gamePanel.transform, fontAsset,
            "Connecting...", 26, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(statusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -255f), new Vector2(1200f, 70f));

        // Flash Overlay
        GameObject flashGo = CreateUIObject("FlashOverlay", canvasGo.transform);
        StretchFull(flashGo.GetComponent<RectTransform>());
        Image flashImg = flashGo.AddComponent<Image>();
        flashImg.color = new Color(1f, 1f, 1f, 0f);
        flashImg.raycastTarget = false;

        // Wire references into WordChainGame
        game.useMidnightTheme = true;
        game.rolePanel = rolePanel;
        game.lobbyPanel = lobbyPanel;
        game.gamePanel = gamePanel;

        game.playerNameInput = playerNameInput;
        game.roomCodeInput = roomCodeInput;
        game.hostLobbyButton = hostLobbyButton;
        game.joinLobbyButton = joinLobbyButton;
        game.entryStatusText = entryStatusText;

        game.lobbyCodeText = lobbyCodeText;
        game.lobbyPlayerCountText = lobbyPlayerCountText;
        game.team1RosterText = team1RosterText;
        game.team2RosterText = team2RosterText;
        game.lobbyStatusText = lobbyStatusText;
        game.joinTeam1Button = joinTeam1Button;
        game.joinTeam2Button = joinTeam2Button;
        game.startGameButton = startGameButton;
        game.leaveLobbyButton = leaveLobbyButton;

        game.tugTrackRect = trackRt;
        game.tugLeftFill = leftFillRt;
        game.tugRightFill = rightFillRt;
        game.tugMarker = markerRt;
        game.team1ScoreLabel = team1ScoreLabel;
        game.team2ScoreLabel = team2ScoreLabel;
        game.pullValueLabel = pullValueLabel;

        game.scoreText = scoreText;
        game.promptText = promptText;
        game.statusText = statusText;
        game.roundText = roundText;
        game.timerText = timerText;
        game.timerBar = timerBar;
        game.wordInput = wordInput;
        game.gameTeam1RosterText = gameTeam1RosterText;
        game.gameTeam2RosterText = gameTeam2RosterText;
        game.gameLeaveButton = gameLeaveButton;
        game.flashTarget = flashImg;

        EditorUtility.SetDirty(game);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log("[WordChainSceneSetup] SampleScene built and saved successfully.");
    }

    private static void EnsureTmpEssentialResources()
    {
        if (AssetDatabase.FindAssets("t:TMP_FontAsset").Length > 0) return;

        var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");
        if (pkgInfo != null)
        {
            string packagePath = System.IO.Path.Combine(pkgInfo.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
            if (System.IO.File.Exists(packagePath))
            {
                AssetDatabase.ImportPackage(packagePath, false);
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogWarning($"[WordChainSceneSetup] TMP Essential Resources not found at: {packagePath}");
            }
        }
    }

    private static TMP_FontAsset ResolveFontAsset()
    {
        TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (liberation != null) return liberation;

        string[] guids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            TMP_FontAsset found = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (found != null) return found;
        }

        if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
        {
            return TMP_Settings.defaultFontAsset;
        }

        return null;
    }

    private static void DestroyIfExists(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject CreateCard(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = CreateUIObject(name, parent);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            anchoredPos, sizeDelta);
        Image img = go.AddComponent<Image>();
        img.color = CardColor;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(2f, -2f);
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    private static TMP_Text CreateTMPText(string name, Transform parent, TMP_FontAsset fontAsset,
        string text, float fontSize, FontStyles style, TextAlignmentOptions alignment, Color color)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) tmp.font = fontAsset;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset fontAsset,
        string label, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = CreateUIObject(name, parent);
        SetRect(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            anchoredPos, sizeDelta);

        Image img = go.AddComponent<Image>();
        img.color = SurfaceColor;

        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        Button btn = go.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = SurfaceColor;
        colors.highlightedColor = new Color(0.45f, 0.34f, 0.92f, 1f);
        colors.pressedColor = new Color(0.24f, 0.16f, 0.56f, 1f);
        colors.selectedColor = new Color(0.45f, 0.34f, 0.92f, 1f);
        btn.colors = colors;

        TMP_Text labelText = CreateTMPText("Text (TMP)", go.transform, fontAsset,
            label, 24, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        StretchFull(labelText.rectTransform);

        return btn;
    }

    private static TMP_InputField CreateTMPInputField(string name, Transform parent, TMP_FontAsset fontAsset,
        string placeholderText, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject root = CreateUIObject(name, parent);
        SetRect(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            anchoredPos, sizeDelta);

        Image bg = root.AddComponent<Image>();
        bg.color = CardColor;

        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = BorderColor;
        outline.effectDistance = new Vector2(2f, -2f);

        TMP_InputField inputField = root.AddComponent<TMP_InputField>();

        GameObject textArea = CreateUIObject("Text Area", root.transform);
        RectTransform textAreaRt = textArea.GetComponent<RectTransform>();
        StretchFull(textAreaRt);
        textAreaRt.offsetMin = new Vector2(20f, 10f);
        textAreaRt.offsetMax = new Vector2(-20f, -10f);
        textArea.AddComponent<RectMask2D>();

        TMP_Text placeholder = CreateTMPText("Placeholder", textArea.transform, fontAsset,
            placeholderText, 22, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        StretchFull(placeholder.rectTransform);

        TMP_Text textComponent = CreateTMPText("Text", textArea.transform, fontAsset,
            string.Empty, 28, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        StretchFull(textComponent.rectTransform);

        inputField.textViewport = textAreaRt;
        inputField.textComponent = textComponent;
        inputField.placeholder = placeholder;
        inputField.pointSize = 28;
        inputField.caretColor = TextColor;
        inputField.customCaretColor = true;
        inputField.selectionColor = new Color(1f, 1f, 1f, 0.25f);
        if (fontAsset != null) inputField.fontAsset = fontAsset;

        return inputField;
    }
}
