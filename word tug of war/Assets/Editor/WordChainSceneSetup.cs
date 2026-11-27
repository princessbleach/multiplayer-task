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
    private static readonly Color BgColor = new Color32(0x11, 0x11, 0x11, 0xFF);
    private static readonly Color SurfaceColor = new Color32(0x2A, 0x2A, 0x2A, 0xFF);
    private static readonly Color BorderColor = new Color32(0x66, 0x66, 0x66, 0xFF);
    private static readonly Color TextColor = Color.white;
    private static readonly Color MutedTextColor = new Color32(0xAA, 0xAA, 0xAA, 0xFF);

    [MenuItem("Tools/Word Chain/Build Scene")]
    public static void BuildScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Configure Main Camera background
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = BgColor;
            mainCam.orthographic = true;
        }

        // Preserve existing firebaseUrl if already configured
        string existingFirebaseUrl = null;
        GameObject existingGm = GameObject.Find("GameManager");
        if (existingGm != null)
        {
            WordChainGame existingGame = existingGm.GetComponent<WordChainGame>();
            if (existingGame != null && !string.IsNullOrWhiteSpace(existingGame.firebaseUrl)
                && !existingGame.firebaseUrl.Contains("your-project"))
            {
                existingFirebaseUrl = existingGame.firebaseUrl;
            }
        }

        // Remove any previous generated objects to keep idempotent
        DestroyIfExists("GameManager");
        DestroyIfExists("Canvas");

        // Ensure TMP Essential Resources are imported
        EnsureTmpEssentialResources();
        TMP_FontAsset fontAsset = ResolveFontAsset();
        if (fontAsset == null)
        {
            Debug.LogError("[WordChainSceneSetup] Could not resolve a TMP_FontAsset!");
        }

        // Ensure EventSystem exists
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

        // Create GameManager
        GameObject gmGo = new GameObject("GameManager");
        WordChainGame game = gmGo.AddComponent<WordChainGame>();
        if (!string.IsNullOrEmpty(existingFirebaseUrl))
        {
            game.firebaseUrl = existingFirebaseUrl;
        }

        // Create Canvas
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

        // RolePanel (active by default)
        GameObject rolePanel = CreateUIObject("RolePanel", canvasGo.transform);
        StretchFull(rolePanel.GetComponent<RectTransform>());
        rolePanel.SetActive(true);

        TMP_Text titleText = CreateTMPText("TitleText", rolePanel.transform, fontAsset,
            "WORD CHAIN TUG-OF-WAR", 56, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        SetRect(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 160f), new Vector2(1200f, 100f));

        TMP_Text subtitleText = CreateTMPText("SubtitleText", rolePanel.transform, fontAsset,
            "SELECT YOUR ROLE", 28, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(subtitleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 80f), new Vector2(800f, 50f));

        Button p1Button = CreateButton("P1Button", rolePanel.transform, fontAsset,
            "Join as Player 1", new Vector2(-220f, -40f), new Vector2(360f, 84f));
        Button p2Button = CreateButton("P2Button", rolePanel.transform, fontAsset,
            "Join as Player 2", new Vector2(220f, -40f), new Vector2(360f, 84f));

        // GamePanel (inactive by default)
        GameObject gamePanel = CreateUIObject("GamePanel", canvasGo.transform);
        StretchFull(gamePanel.GetComponent<RectTransform>());
        gamePanel.SetActive(false);

        TMP_Text scoreText = CreateTMPText("ScoreText", gamePanel.transform, fontAsset,
            "P1 [-15]  ---------------|---------------  [+15] P2\nSCORE: 0",
            34, FontStyles.Normal, TextAlignmentOptions.Center, TextColor);
        SetRect(scoreText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 260f), new Vector2(1500f, 140f));

        TMP_Text promptText = CreateTMPText("PromptText", gamePanel.transform, fontAsset,
            "START\n<size=55%>NEXT LETTER: T</size>",
            88, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        SetRect(promptText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 60f), new Vector2(1400f, 200f));

        TMP_InputField wordInput = CreateTMPInputField("WordInput", gamePanel.transform, fontAsset,
            new Vector2(0f, -120f), new Vector2(680f, 84f));

        TMP_Text statusText = CreateTMPText("StatusText", gamePanel.transform, fontAsset,
            "Connecting...", 28, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        SetRect(statusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -240f), new Vector2(1400f, 80f));

        // Wire references into WordChainGame
        game.rolePanel = rolePanel;
        game.gamePanel = gamePanel;
        game.p1Button = p1Button;
        game.p2Button = p2Button;
        game.scoreText = scoreText;
        game.promptText = promptText;
        game.statusText = statusText;
        game.wordInput = wordInput;

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
        colors.highlightedColor = new Color32(0x2E, 0x2E, 0x2E, 0xFF);
        colors.pressedColor = new Color32(0x3E, 0x3E, 0x3E, 0xFF);
        colors.selectedColor = new Color32(0x2E, 0x2E, 0x2E, 0xFF);
        btn.colors = colors;

        TMP_Text labelText = CreateTMPText("Text (TMP)", go.transform, fontAsset,
            label, 28, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        StretchFull(labelText.rectTransform);

        return btn;
    }

    private static TMP_InputField CreateTMPInputField(string name, Transform parent, TMP_FontAsset fontAsset,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject root = CreateUIObject(name, parent);
        SetRect(root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            anchoredPos, sizeDelta);

        Image bg = root.AddComponent<Image>();
        bg.color = SurfaceColor;

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
            "TYPE WORD + ENTER (OR 'RESET')...", 26, FontStyles.Normal, TextAlignmentOptions.Center, MutedTextColor);
        StretchFull(placeholder.rectTransform);

        TMP_Text textComponent = CreateTMPText("Text", textArea.transform, fontAsset,
            string.Empty, 32, FontStyles.Bold, TextAlignmentOptions.Center, TextColor);
        StretchFull(textComponent.rectTransform);

        inputField.textViewport = textAreaRt;
        inputField.textComponent = textComponent;
        inputField.placeholder = placeholder;
        inputField.pointSize = 32;
        inputField.caretColor = TextColor;
        inputField.customCaretColor = true;
        inputField.selectionColor = new Color(1f, 1f, 1f, 0.25f);
        if (fontAsset != null) inputField.fontAsset = fontAsset;

        return inputField;
    }
}
