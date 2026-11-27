using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class WordChainGame : MonoBehaviour
{
    [Serializable]
    public class PlayerInfo
    {
        public int id;
        public string name;
        public int team;
    }

    [Serializable]
    public class FaceOffEntry
    {
        public int playerId;
        public string word;
        public int wordLength;
        public int team;
    }

    [Serializable]
    public class RoomState
    {
        public string roomCode;
        public int hostId;
        public bool gameStarted;
        public List<PlayerInfo> players = new List<PlayerInfo>();

        // Face-off fields (string used instead of char for Unity JsonUtility compatibility)
        public bool faceOffActive;
        public string faceOffLetter;
        public double faceOffDeadline;
        public List<FaceOffEntry> faceOffEntries = new List<FaceOffEntry>();

        // Main game fields
        public string currentWord;
        public int activeTeam;
        public int activePlayerId;
        public int team1TurnIndex;
        public int team2TurnIndex;
        public int score;
        public string usedWords;
        public float roundDuration;
        public double turnStartUnix;
        public string lastActionText;
    }

    [Header("Firebase Configuration")]
    [Tooltip("Base URL of your Firebase Realtime Database (e.g. https://your-project-default-rtdb.firebaseio.com)")]
    public string firebaseUrl = "https://your-project-default-rtdb.firebaseio.com";

    [Header("Match Settings")]
    public int maxPlayers = 8;
    public int winThreshold = 15;
    public float faceOffDuration = 20f;

    [Header("Timer Settings")]
    [Tooltip("Starting timer duration in seconds for turn 1")]
    public float initialRoundDuration = 60f;
    [Tooltip("How many seconds to subtract after each completed turn")]
    public float timerDecreasePerTurn = 5f;
    [Tooltip("Minimum allowed timer duration per turn in seconds")]
    public float minRoundDuration = 10f;

    [Header("Tug-of-War Bar UI")]
    public RectTransform tugTrackRect;
    public RectTransform tugLeftFill;
    public RectTransform tugRightFill;
    public RectTransform tugMarker;
    public TMP_Text team1ScoreLabel;
    public TMP_Text team2ScoreLabel;
    public TMP_Text pullValueLabel;
    public float moveSpeed = 10f;

    [Header("Tug-of-War Sprites (Optional)")]
    public RectTransform player1Sprite;
    public RectTransform player2Sprite;
    public RectTransform tugTrackCenter;
    public float maxPullDistance = 220f;

    [Header("Midnight Purple Theme")]
    public bool useMidnightTheme = true;
    public Color backdropColor = new Color(0.043f, 0.051f, 0.110f, 0.98f);
    public Color cardSurfaceColor = new Color(0.086f, 0.102f, 0.204f, 1f);
    public Color buttonNormalColor = new Color(0.34f, 0.24f, 0.78f, 1f);
    public Color buttonHighlightedColor = new Color(0.45f, 0.34f, 0.92f, 1f);
    public Color buttonPressedColor = new Color(0.24f, 0.16f, 0.56f, 1f);
    public Color primaryTextColor = new Color(0.96f, 0.97f, 1f, 1f);
    public Color promptAccentColor = new Color(0.62f, 0.56f, 1f, 1f);
    public Color timerBarColor = new Color(0.55f, 0.48f, 1f, 1f);
    public Color timerUrgentColor = new Color(1f, 0.42f, 0.56f, 1f);

    [Header("Audio & Flash Feedback")]
    public AudioSource sfxSource;
    public AudioClip buttonClickClip;
    [Range(0f, 2f)] public float buttonClickVolume = 1f;
    public float buttonPitchJitter = 0.06f;

    public AudioClip typingClip;
    [Range(0f, 2f)] public float typingVolume = 0.35f;
    public float typingPitchJitter = 0.05f;

    public AudioClip validWordClip;
    [Range(0f, 2f)] public float validWordVolume = 1.5f;

    public AudioClip invalidWordClip;
    [Range(0f, 2f)] public float invalidWordVolume = 1.5f;

    public AudioClip winClip;
    [Range(0f, 2f)] public float winVolume = 1.5f;

    public Graphic flashTarget;
    public Color validFlashColor = new Color(0.18f, 0.85f, 0.55f, 0.5f);
    public Color invalidFlashColor = new Color(0.95f, 0.26f, 0.42f, 0.5f);
    public Color winFlashColor = new Color(0.56f, 0.48f, 1f, 0.6f);
    public float flashDuration = 0.25f;

    [Header("Panels")]
    public GameObject rolePanel;
    public GameObject lobbyPanel;
    public GameObject faceOffPanel;
    public GameObject gamePanel;

    [Header("Entry / Host & Join UI")]
    public TMP_InputField playerNameInput;
    public TMP_InputField roomCodeInput;
    public Button hostLobbyButton;
    public Button joinLobbyButton;
    public TMP_Text entryStatusText;

    [Header("Lobby UI")]
    public TMP_Text lobbyCodeText;
    public TMP_Text lobbyPlayerCountText;
    public TMP_Text team1RosterText;
    public TMP_Text team2RosterText;
    public TMP_Text lobbyStatusText;
    public Button joinTeam1Button;
    public Button joinTeam2Button;
    public Button startGameButton;
    public Button leaveLobbyButton;

    [Header("Face-Off UI")]
    public TMP_Text faceOffLetterText;
    public TMP_Text faceOffTimerText;
    public TMP_Text faceOffStatusText;
    public TMP_InputField faceOffWordInput;

    [Header("Game UI")]
    public TMP_Text scoreText;
    public TMP_Text promptText;
    public TMP_Text statusText;
    public TMP_Text roundText;
    public TMP_Text timerText;
    public Image timerBar;
    public TMP_InputField wordInput;
    public TMP_Text gameTeam1RosterText;
    public TMP_Text gameTeam2RosterText;
    public Button gameLeaveButton;

    private const float PollInterval = 0.6f;
    private const string DictionaryApiBase = "https://api.dictionaryapi.dev/api/v2/entries/en/";

    private int _localPlayerId;
    private string _localPlayerName = "Player";
    private string _currentRoomCode = string.Empty;
    private RoomState _currentState;
    private string _previousInputText = string.Empty;
    private string _previousFaceOffText = string.Empty;
    private float _remainingTime;
    private bool _timeoutHandledForTurn;
    private bool _winSoundPlayed;
    private bool _isSubmitting;
    private bool _isPolling;
    private bool _isBusyLobby;
    private Coroutine _pollCoroutine;
    private Coroutine _flashCoroutine;

    private float _visualNormalizedScore;
    private Vector2 _p1BasePos;
    private Vector2 _p2BasePos;
    private bool _basePositionsCaptured;
    private float _pullPulseTimer;
    private int _lastPullDirection;
    private Color _flashBaseColor;
    private bool _flashBaseCaptured;

    // Face-off local state
    private bool _faceOffSubmitted;
    private float _faceOffRemaining;
    private bool _faceOffTimeoutHandled;

    // Juice / Punch state
    private Coroutine _wordInputPunchCoroutine;
    private Coroutine _faceOffInputPunchCoroutine;
    private Coroutine _promptPopCoroutine;
    private Vector3 _wordInputBaseScale = Vector3.one;
    private Vector3 _faceOffInputBaseScale = Vector3.one;
    private Vector3 _promptBaseScale = Vector3.one;

    private void Start()
    {
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (useMidnightTheme)
        {
            ApplyMidnightTheme();
        }

        CaptureBaseSpritePositions();
        CaptureFlashBaseColor();

        if (wordInput != null) _wordInputBaseScale = wordInput.transform.localScale;
        if (faceOffWordInput != null) _faceOffInputBaseScale = faceOffWordInput.transform.localScale;
        if (promptText != null) _promptBaseScale = promptText.transform.localScale;

        ShowPanel(rolePanel);

        if (hostLobbyButton != null)
        {
            hostLobbyButton.onClick.AddListener(PlayButtonClick);
            hostLobbyButton.onClick.AddListener(OnHostLobbyClicked);
        }

        if (joinLobbyButton != null)
        {
            joinLobbyButton.onClick.AddListener(PlayButtonClick);
            joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
        }

        if (joinTeam1Button != null)
        {
            joinTeam1Button.onClick.AddListener(PlayButtonClick);
            joinTeam1Button.onClick.AddListener(() => OnSwitchTeamClicked(1));
        }

        if (joinTeam2Button != null)
        {
            joinTeam2Button.onClick.AddListener(PlayButtonClick);
            joinTeam2Button.onClick.AddListener(() => OnSwitchTeamClicked(2));
        }

        if (startGameButton != null)
        {
            startGameButton.onClick.AddListener(PlayButtonClick);
            startGameButton.onClick.AddListener(OnStartGameClicked);
        }

        if (leaveLobbyButton != null)
        {
            leaveLobbyButton.onClick.AddListener(PlayButtonClick);
            leaveLobbyButton.onClick.AddListener(LeaveRoom);
        }

        if (gameLeaveButton != null)
        {
            gameLeaveButton.onClick.AddListener(PlayButtonClick);
            gameLeaveButton.onClick.AddListener(LeaveRoom);
        }

        if (wordInput != null)
        {
            _previousInputText = wordInput.text ?? string.Empty;
            wordInput.onValueChanged.AddListener(OnWordValueChanged);
            wordInput.onSubmit.AddListener(OnWordSubmitted);
        }

        if (faceOffWordInput != null)
        {
            _previousFaceOffText = faceOffWordInput.text ?? string.Empty;
            faceOffWordInput.onValueChanged.AddListener(OnFaceOffValueChanged);
            faceOffWordInput.onSubmit.AddListener(OnFaceOffSubmitted);
        }
    }

    private void Update()
    {
        UpdateTugVisuals();

        // Face-off countdown & host resolution
        if (_currentState != null && _currentState.gameStarted && _currentState.faceOffActive &&
            faceOffPanel != null && faceOffPanel.activeInHierarchy)
        {
            double now = GetUnixTimeSeconds();
            _faceOffRemaining = Mathf.Max(0f, (float)(_currentState.faceOffDeadline - now));

            if (faceOffTimerText != null)
            {
                int secs = Mathf.CeilToInt(_faceOffRemaining);
                faceOffTimerText.text = $"{secs}s";
                if (useMidnightTheme)
                {
                    faceOffTimerText.color = secs <= 5 ? timerUrgentColor : promptAccentColor;
                }
            }

            int totalPlayers = _currentState.players != null ? _currentState.players.Count : 0;
            int totalEntries = _currentState.faceOffEntries != null ? _currentState.faceOffEntries.Count : 0;
            bool allSubmitted = totalPlayers > 0 && totalEntries >= totalPlayers;

            if ((_faceOffRemaining <= 0f || allSubmitted) &&
                !_faceOffTimeoutHandled &&
                _localPlayerId == _currentState.hostId &&
                !_isSubmitting)
            {
                _faceOffTimeoutHandled = true;
                StartCoroutine(ResolveFaceOff());
            }
        }

        if (_currentState == null || !_currentState.gameStarted || _currentState.faceOffActive ||
            gamePanel == null || !gamePanel.activeInHierarchy)
            return;

        if (Mathf.Abs(_currentState.score) >= winThreshold)
            return;

        double gameNow = GetUnixTimeSeconds();
        double elapsed = Math.Max(0.0, gameNow - _currentState.turnStartUnix);
        _remainingTime = Mathf.Max(0f, _currentState.roundDuration - (float)elapsed);

        UpdateTimerDisplay();

        if (_remainingTime <= 0f && !_isSubmitting && _currentState.activePlayerId == _localPlayerId && !_timeoutHandledForTurn)
        {
            _timeoutHandledForTurn = true;
            StartCoroutine(HandleTurnTimeout());
        }
    }

    private void OnDestroy()
    {
        if (hostLobbyButton != null) hostLobbyButton.onClick.RemoveAllListeners();
        if (joinLobbyButton != null) joinLobbyButton.onClick.RemoveAllListeners();
        if (joinTeam1Button != null) joinTeam1Button.onClick.RemoveAllListeners();
        if (joinTeam2Button != null) joinTeam2Button.onClick.RemoveAllListeners();
        if (startGameButton != null) startGameButton.onClick.RemoveAllListeners();
        if (leaveLobbyButton != null) leaveLobbyButton.onClick.RemoveAllListeners();
        if (gameLeaveButton != null) gameLeaveButton.onClick.RemoveAllListeners();

        if (wordInput != null)
        {
            wordInput.onValueChanged.RemoveListener(OnWordValueChanged);
            wordInput.onSubmit.RemoveListener(OnWordSubmitted);
        }
        if (faceOffWordInput != null)
        {
            faceOffWordInput.onValueChanged.RemoveListener(OnFaceOffValueChanged);
            faceOffWordInput.onSubmit.RemoveListener(OnFaceOffSubmitted);
        }
    }

    private void ShowPanel(GameObject activePanel)
    {
        if (rolePanel != null) rolePanel.SetActive(rolePanel == activePanel);
        if (lobbyPanel != null) lobbyPanel.SetActive(lobbyPanel == activePanel);
        if (faceOffPanel != null) faceOffPanel.SetActive(faceOffPanel == activePanel);
        if (gamePanel != null) gamePanel.SetActive(gamePanel == activePanel);
    }

    private void ApplyMidnightTheme()
    {
        StylePanel(rolePanel, backdropColor);
        StylePanel(lobbyPanel, backdropColor);
        StylePanel(faceOffPanel, backdropColor);
        StylePanel(gamePanel, backdropColor);

        StyleButton(hostLobbyButton);
        StyleButton(joinLobbyButton);
        StyleButton(joinTeam1Button);
        StyleButton(joinTeam2Button);
        StyleButton(startGameButton);
        StyleButton(leaveLobbyButton);
        StyleButton(gameLeaveButton);

        StyleInput(playerNameInput);
        StyleInput(roomCodeInput);
        StyleInput(faceOffWordInput);
        StyleInput(wordInput);

        if (scoreText != null) scoreText.color = primaryTextColor;
        if (promptText != null) promptText.color = primaryTextColor;
        if (statusText != null) statusText.color = promptAccentColor;
        if (roundText != null) roundText.color = promptAccentColor;
        if (timerText != null) timerText.color = primaryTextColor;
        if (timerBar != null) timerBar.color = timerBarColor;
        if (entryStatusText != null) entryStatusText.color = promptAccentColor;
        if (lobbyCodeText != null) lobbyCodeText.color = primaryTextColor;
        if (lobbyPlayerCountText != null) lobbyPlayerCountText.color = promptAccentColor;
        if (lobbyStatusText != null) lobbyStatusText.color = promptAccentColor;
        if (team1RosterText != null) team1RosterText.color = primaryTextColor;
        if (team2RosterText != null) team2RosterText.color = primaryTextColor;
        if (faceOffLetterText != null) faceOffLetterText.color = primaryTextColor;
        if (faceOffTimerText != null) faceOffTimerText.color = promptAccentColor;
        if (faceOffStatusText != null) faceOffStatusText.color = promptAccentColor;
        if (gameTeam1RosterText != null) gameTeam1RosterText.color = promptAccentColor;
        if (gameTeam2RosterText != null) gameTeam2RosterText.color = promptAccentColor;
    }

    private void StylePanel(GameObject panel, Color color)
    {
        if (panel == null) return;
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = color;
        }
    }

    private void StyleButton(Button button)
    {
        if (button == null) return;

        ColorBlock colors = button.colors;
        colors.normalColor = buttonNormalColor;
        colors.highlightedColor = buttonHighlightedColor;
        colors.selectedColor = buttonHighlightedColor;
        colors.pressedColor = buttonPressedColor;
        button.colors = colors;

        if (button.targetGraphic != null)
        {
            button.targetGraphic.color = Color.white;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.color = primaryTextColor;
        }
    }

    private void StyleInput(TMP_InputField input)
    {
        if (input == null) return;
        if (input.targetGraphic != null)
        {
            input.targetGraphic.color = cardSurfaceColor;
        }
        if (input.textComponent != null)
        {
            input.textComponent.color = primaryTextColor;
        }
        if (input.placeholder is TMP_Text placeholderTmp)
        {
            placeholderTmp.color = new Color(promptAccentColor.r, promptAccentColor.g, promptAccentColor.b, 0.5f);