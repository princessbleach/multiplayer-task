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
    public class RoomState
    {
        public string roomCode;
        public int hostId;
        public bool gameStarted;
        public List<PlayerInfo> players = new List<PlayerInfo>();
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
    }

    private void Update()
    {
        UpdateTugVisuals();

        if (_currentState == null || !_currentState.gameStarted || gamePanel == null || !gamePanel.activeInHierarchy)
            return;

        if (Mathf.Abs(_currentState.score) >= winThreshold)
            return;

        double now = GetUnixTimeSeconds();
        double elapsed = Math.Max(0.0, now - _currentState.turnStartUnix);
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
    }

    private void ShowPanel(GameObject activePanel)
    {
        if (rolePanel != null) rolePanel.SetActive(rolePanel == activePanel);
        if (lobbyPanel != null) lobbyPanel.SetActive(lobbyPanel == activePanel);
        if (gamePanel != null) gamePanel.SetActive(gamePanel == activePanel);
    }

    private void ApplyMidnightTheme()
    {
        StylePanel(rolePanel, backdropColor);
        StylePanel(lobbyPanel, backdropColor);
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
        }
    }

    public void PlayButtonClick()
    {
        PlayClipWithJitter(buttonClickClip, buttonClickVolume, buttonPitchJitter);
    }

    private void OnWordValueChanged(string currentText)
    {
        string safeText = currentText ?? string.Empty;
        bool addedCharacter = safeText.Length > _previousInputText.Length;
        _previousInputText = safeText;

        if (!addedCharacter || _isSubmitting || typingClip == null || sfxSource == null)
            return;

        if (_currentState != null && _currentState.gameStarted &&
            _currentState.activePlayerId == _localPlayerId &&
            Mathf.Abs(_currentState.score) < winThreshold)
        {
            PlayClipWithJitter(typingClip, typingVolume, typingPitchJitter);
        }
    }

    private void PlayClipWithJitter(AudioClip clip, float volume, float pitchJitter)
    {
        if (sfxSource == null || clip == null) return;

        float originalPitch = sfxSource.pitch;
        sfxSource.pitch = pitchJitter > 0f
            ? 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter)
            : 1f;

        sfxSource.PlayOneShot(clip, volume);
        sfxSource.pitch = originalPitch;
    }

    private void CaptureFlashBaseColor()
    {
        if (_flashBaseCaptured || flashTarget == null) return;
        _flashBaseColor = flashTarget.color;
        _flashBaseCaptured = true;
    }

    private void PlayClip(AudioClip clip, float volume = 1f)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    private void TriggerFlash(Color targetColor)
    {
        if (flashTarget == null) return;
        CaptureFlashBaseColor();

        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }
        _flashCoroutine = StartCoroutine(FlashColorRoutine(targetColor));
    }

    private IEnumerator FlashColorRoutine(Color targetColor)
    {
        flashTarget.color = targetColor;
        float half = Mathf.Max(0.02f, flashDuration * 0.5f);
        float elapsed = 0f;

        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            flashTarget.color = Color.Lerp(targetColor, _flashBaseColor, elapsed / half);
            yield return null;
        }

        flashTarget.color = _flashBaseColor;
        _flashCoroutine = null;
    }

    private void CaptureBaseSpritePositions()
    {
        if (_basePositionsCaptured) return;

        if (tugTrackCenter != null)
        {
            _p1BasePos = tugTrackCenter.anchoredPosition;
            _p2BasePos = tugTrackCenter.anchoredPosition;
        }
        else
        {
            if (player1Sprite != null) _p1BasePos = player1Sprite.anchoredPosition;
            if (player2Sprite != null) _p2BasePos = player2Sprite.anchoredPosition;
        }

        _basePositionsCaptured = true;
    }

    private void UpdateTugVisuals()
    {
        int score = _currentState != null ? Mathf.Clamp(_currentState.score, -winThreshold, winThreshold) : 0;
        float targetNormalized = winThreshold > 0 ? (float)score / winThreshold : 0f;
        _visualNormalizedScore = Mathf.Lerp(_visualNormalizedScore, targetNormalized, Time.deltaTime * moveSpeed);

        if (tugTrackRect != null)
        {
            float halfTrack = tugTrackRect.rect.width * 0.5f;

            if (tugMarker != null)
            {
                Vector2 markerPos = tugMarker.anchoredPosition;
                markerPos.x = _visualNormalizedScore * halfTrack;
                tugMarker.anchoredPosition = markerPos;
            }

            if (tugLeftFill != null)
            {
                float leftAmount = Mathf.Max(0f, -_visualNormalizedScore) * halfTrack;
                tugLeftFill.sizeDelta = new Vector2(leftAmount, tugLeftFill.sizeDelta.y);
            }

            if (tugRightFill != null)
            {
                float rightAmount = Mathf.Max(0f, _visualNormalizedScore) * halfTrack;
                tugRightFill.sizeDelta = new Vector2(rightAmount, tugRightFill.sizeDelta.y);
            }
        }

        UpdateTugSprites(_visualNormalizedScore);
    }

    private void UpdateTugSprites(float normalizedOffset)
    {
        if (player1Sprite == null && player2Sprite == null) return;
        CaptureBaseSpritePositions();

        float shiftX = normalizedOffset * maxPullDistance;

        if (_pullPulseTimer > 0f)
        {
            _pullPulseTimer = Mathf.Max(0f, _pullPulseTimer - Time.deltaTime);
        }

        float pulseWeight = _pullPulseTimer > 0f ? Mathf.Sin((_pullPulseTimer / 0.35f) * Mathf.PI) : 0f;
        float bobY = Mathf.Sin(Time.time * 14f) * 6f * pulseWeight;

        if (player1Sprite != null)
        {
            Vector2 targetP1 = new Vector2(_p1BasePos.x + shiftX, _p1BasePos.y + (_lastPullDirection < 0 ? bobY : 0f));
            player1Sprite.anchoredPosition = Vector2.Lerp(player1Sprite.anchoredPosition, targetP1, Time.deltaTime * moveSpeed);

            float leanZ1 = _lastPullDirection < 0 ? 10f * pulseWeight : 3f;
            player1Sprite.localRotation = Quaternion.Lerp(player1Sprite.localRotation, Quaternion.Euler(0f, 0f, leanZ1), Time.deltaTime * moveSpeed);
        }

        if (player2Sprite != null)
        {
            Vector2 targetP2 = new Vector2(_p2BasePos.x + shiftX, _p2BasePos.y + (_lastPullDirection > 0 ? bobY : 0f));
            player2Sprite.anchoredPosition = Vector2.Lerp(player2Sprite.anchoredPosition, targetP2, Time.deltaTime * moveSpeed);

            float leanZ2 = _lastPullDirection > 0 ? -10f * pulseWeight : -3f;
            player2Sprite.localRotation = Quaternion.Lerp(player2Sprite.localRotation, Quaternion.Euler(0f, 0f, leanZ2), Time.deltaTime * moveSpeed);
        }
    }

    private void TriggerPullFeedback(int previousScore, int nextScore)
    {
        int delta = nextScore - previousScore;
        if (delta < 0)
        {
            _lastPullDirection = -1;
            _pullPulseTimer = 0.35f;
        }
        else if (delta > 0)
        {
            _lastPullDirection = 1;
            _pullPulseTimer = 0.35f;
        }
    }

    // ---------------- LOBBY HOST & JOIN ----------------

    private string ReadLocalPlayerName()
    {
        string raw = playerNameInput != null ? (playerNameInput.text ?? string.Empty).Trim() : string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            return "Player";
        }
        return raw.Length > 14 ? raw.Substring(0, 14) : raw;
    }

    private void SetEntryStatus(string message)
    {
        if (entryStatusText != null)
        {
            entryStatusText.text = message;
        }
    }

    private void SetLobbyStatus(string message)
    {
        if (lobbyStatusText != null)
        {
            lobbyStatusText.text = message;
        }
    }

    private void OnHostLobbyClicked()
    {
        if (_isBusyLobby) return;
        if (string.IsNullOrWhiteSpace(firebaseUrl) || firebaseUrl.Contains("your-project"))
        {
            SetEntryStatus("Set firebaseUrl on GameManager in the Inspector.");
            return;
        }

        _localPlayerName = ReadLocalPlayerName();
        string code = GenerateRoomCode(4);
        StartCoroutine(HostLobbyRoutine(code));
    }

    private void OnJoinLobbyClicked()
    {
        if (_isBusyLobby) return;
        if (string.IsNullOrWhiteSpace(firebaseUrl) || firebaseUrl.Contains("your-project"))
        {
            SetEntryStatus("Set firebaseUrl on GameManager in the Inspector.");
            return;
        }

        string code = roomCodeInput != null ? (roomCodeInput.text ?? string.Empty).Trim().ToUpperInvariant() : string.Empty;
        if (string.IsNullOrEmpty(code) || code.Length < 3)
        {
            SetEntryStatus("Enter a valid lobby code.");
            return;
        }

        _localPlayerName = ReadLocalPlayerName();
        StartCoroutine(JoinLobbyRoutine(code));
    }

    private IEnumerator HostLobbyRoutine(string roomCode)
    {
        _isBusyLobby = true;
        SetEntryStatus($"Creating lobby {roomCode}...");

        _currentRoomCode = roomCode;
        _localPlayerId = 1;

        RoomState newRoom = new RoomState
        {
            roomCode = roomCode,
            hostId = 1,
            gameStarted = false,
            players = new List<PlayerInfo>
            {
                new PlayerInfo { id = 1, name = _localPlayerName, team = 1 }
            },
            currentWord = "START",
            activeTeam = 1,
            activePlayerId = 1,
            team1TurnIndex = 0,
            team2TurnIndex = 0,
            score = 0,
            usedWords = "START,",
            roundDuration = initialRoundDuration,
            turnStartUnix = GetUnixTimeSeconds(),
            lastActionText = "Waiting for players..."
        };

        bool success = false;
        yield return StartCoroutine(PutRoomState(newRoom, ok => success = ok));

        _isBusyLobby = false;
        if (success)
        {
            ShowPanel(lobbyPanel);
            UpdateLobbyUI();
            StartPolling();
        }
        else
        {
            SetEntryStatus("Failed to create lobby. Check connection.");
        }
    }

    private IEnumerator JoinLobbyRoutine(string roomCode)
    {
        _isBusyLobby = true;
        SetEntryStatus($"Joining lobby {roomCode}...");

        string url = GetRoomUrl(roomCode);
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetEntryStatus($"Connection error: {request.error}");
                _isBusyLobby = false;
                yield break;
            }

            string json = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null")
            {
                SetEntryStatus($"Lobby '{roomCode}' not found.");
                _isBusyLobby = false;
                yield break;
            }

            RoomState room = JsonUtility.FromJson<RoomState>(json);
            if (room == null || room.players == null)
            {
                SetEntryStatus($"Lobby '{roomCode}' data is invalid.");
                _isBusyLobby = false;
                yield break;
            }

            if (room.gameStarted)
            {
                SetEntryStatus($"Lobby '{roomCode}' has already started.");
                _isBusyLobby = false;
                yield break;
            }

            if (room.players.Count >= maxPlayers)
            {
                SetEntryStatus($"Lobby '{roomCode}' is full ({maxPlayers}/{maxPlayers}).");
                _isBusyLobby = false;
                yield break;
            }

            int nextId = GetNextAvailablePlayerId(room.players);
            int team1Count = CountPlayersOnTeam(room.players, 1);
            int team2Count = CountPlayersOnTeam(room.players, 2);
            int assignedTeam = team1Count <= team2Count ? 1 : 2;

            room.players.Add(new PlayerInfo
            {
                id = nextId,
                name = _localPlayerName,
                team = assignedTeam
            });

            _currentRoomCode = roomCode;
            _localPlayerId = nextId;

            bool success = false;
            yield return StartCoroutine(PutRoomState(room, ok => success = ok));

            _isBusyLobby = false;
            if (success)
            {
                ShowPanel(lobbyPanel);
                UpdateLobbyUI();
                StartPolling();
            }
            else
            {
                SetEntryStatus("Failed to join lobby.");
            }
        }
    }

    private void OnSwitchTeamClicked(int targetTeam)
    {
        if (_isBusyLobby || _currentState == null || _currentState.gameStarted) return;
        StartCoroutine(SwitchTeamRoutine(targetTeam));
    }

    private IEnumerator SwitchTeamRoutine(int targetTeam)
    {
        _isBusyLobby = true;
        RoomState latest = null;
        yield return StartCoroutine(FetchLatestRoom(r => latest = r));

        if (latest != null && !latest.gameStarted && latest.players != null)
        {
            for (int i = 0; i < latest.players.Count; i++)
            {
                if (latest.players[i].id == _localPlayerId)
                {
                    latest.players[i].team = targetTeam;
                    break;
                }
            }

            yield return StartCoroutine(PutRoomState(latest, null));
            UpdateLobbyUI();
        }

        _isBusyLobby = false;
    }

    private void OnStartGameClicked()
    {
        if (_isBusyLobby || _currentState == null) return;
        if (_localPlayerId != _currentState.hostId)
        {
            SetLobbyStatus("Only the host can start the game.");
            return;
        }

        StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        _isBusyLobby = true;
        RoomState latest = null;
        yield return StartCoroutine(FetchLatestRoom(r => latest = r));

        if (latest == null || latest.players == null)
        {
            SetLobbyStatus("Could not reach lobby.");
            _isBusyLobby = false;
            yield break;
        }

        List<PlayerInfo> team1 = GetTeamPlayers(latest.players, 1);
        List<PlayerInfo> team2 = GetTeamPlayers(latest.players, 2);

        if (team1.Count == 0 || team2.Count == 0)
        {
            SetLobbyStatus("Need at least 1 player on both Team 1 and Team 2.");
            _isBusyLobby = false;
            yield break;
        }

        latest.gameStarted = true;
        latest.currentWord = "START";
        latest.usedWords = "START,";
        latest.score = 0;
        latest.activeTeam = 1;
        latest.team1TurnIndex = 0;
        latest.team2TurnIndex = 0;
        latest.activePlayerId = team1[0].id;
        latest.roundDuration = initialRoundDuration;
        latest.turnStartUnix = GetUnixTimeSeconds();
        latest.lastActionText = $"MATCH STARTED  ·  {team1[0].name.ToUpperInvariant()} GOES FIRST";

        bool ok = false;
        yield return StartCoroutine(PutRoomState(latest, res => ok = res));
        _isBusyLobby = false;

        if (ok)
        {
            EnterGameScreen();
        }
    }

    private void LeaveRoom()
    {
        if (_pollCoroutine != null)
        {
            StopCoroutine(_pollCoroutine);
            _pollCoroutine = null;
        }

        if (_currentState != null && !_currentState.gameStarted && !string.IsNullOrEmpty(_currentRoomCode))
        {
            StartCoroutine(RemoveSelfFromLobby(_currentRoomCode, _localPlayerId));
        }

        _currentState = null;
        _currentRoomCode = string.Empty;
        _localPlayerId = 0;
        _winSoundPlayed = false;
        _visualNormalizedScore = 0f;
        ShowPanel(rolePanel);
        SetEntryStatus("Host a new lobby or enter a code to join.");
    }

    private IEnumerator RemoveSelfFromLobby(string roomCode, int playerId)
    {
        string url = GetRoomUrl(roomCode);
        if (string.IsNullOrEmpty(url)) yield break;

        using (UnityWebRequest getReq = UnityWebRequest.Get(url))
        {
            yield return getReq.SendWebRequest();
            if (getReq.result != UnityWebRequest.Result.Success) yield break;

            string json = getReq.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "null") yield break;

            RoomState room = JsonUtility.FromJson<RoomState>(json);
            if (room == null || room.players == null || room.gameStarted) yield break;

            room.players.RemoveAll(p => p.id == playerId);
            if (room.players.Count > 0 && room.hostId == playerId)
            {
                room.hostId = room.players[0].id;
            }

            string putJson = JsonUtility.ToJson(room);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(putJson);
            using (UnityWebRequest putReq = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
            {
                putReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
                putReq.downloadHandler = new DownloadHandlerBuffer();
                putReq.SetRequestHeader("Content-Type", "application/json");
                yield return putReq.SendWebRequest();
            }
        }
    }

    private void EnterGameScreen()
    {
        ShowPanel(gamePanel);
        UpdateUI();
        FocusInput();
    }

    // ---------------- POLLING & SYNC ----------------

    private void StartPolling()
    {
        if (_pollCoroutine != null)
        {
            StopCoroutine(_pollCoroutine);
        }
        _pollCoroutine = StartCoroutine(PollRoomRoutine());
    }

    private IEnumerator PollRoomRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(PollInterval);

        while (true)
        {
            if (!_isSubmitting && !_isPolling && !string.IsNullOrEmpty(_currentRoomCode))
            {
                yield return StartCoroutine(FetchRoomState());
            }
            yield return wait;
        }
    }

    private IEnumerator FetchLatestRoom(Action<RoomState> callback)
    {
        string url = GetRoomUrl(_currentRoomCode);
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(null);
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                if (!string.IsNullOrWhiteSpace(json) && json.Trim() != "null")
                {
                    callback?.Invoke(JsonUtility.FromJson<RoomState>(json));
                    yield break;
                }
            }
        }
        callback?.Invoke(null);
    }

    private IEnumerator FetchRoomState()
    {
        string url = GetRoomUrl(_currentRoomCode);
        if (string.IsNullOrEmpty(url)) yield break;

        _isPolling = true;
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                if (!string.IsNullOrWhiteSpace(json) && json.Trim() != "null")
                {
                    RoomState fetched = JsonUtility.FromJson<RoomState>(json);
                    if (fetched != null)
                    {
                        int prevScore = _currentState != null ? _currentState.score : 0;
                        bool wasStarted = _currentState != null && _currentState.gameStarted;
                        bool turnChanged = _currentState == null ||
                                           fetched.activePlayerId != _currentState.activePlayerId ||
                                           fetched.activeTeam != _currentState.activeTeam ||
                                           fetched.currentWord != _currentState.currentWord ||
                                           Math.Abs(fetched.turnStartUnix - _currentState.turnStartUnix) > 0.01;

                        if (fetched.roundDuration <= 0f)
                        {
                            fetched.roundDuration = initialRoundDuration;
                        }

                        if (Mathf.Abs(fetched.score) < winThreshold)
                        {
                            _winSoundPlayed = false;
                        }

                        _currentState = fetched;

                        if (fetched.gameStarted)
                        {
                            if (!wasStarted && lobbyPanel != null && lobbyPanel.activeInHierarchy)
                            {
                                EnterGameScreen();
                            }

                            if (turnChanged)
                            {
                                _timeoutHandledForTurn = false;
                                TriggerPullFeedback(prevScore, fetched.score);
                            }

                            UpdateUI();
                        }
                        else
                        {
                            UpdateLobbyUI();
                        }
                    }
                }
            }
            else
            {
                if (gamePanel != null && gamePanel.activeInHierarchy)
                {
                    SetStatus($"Sync error: {request.error}");
                }
                else
                {
                    SetLobbyStatus($"Sync error: {request.error}");
                }
            }
        }
        _isPolling = false;
    }

    private IEnumerator PutRoomState(RoomState state, Action<bool> onDone)
    {
        string url = GetRoomUrl(state.roomCode);
        if (string.IsNullOrEmpty(url))
        {
            onDone?.Invoke(false);
            yield break;
        }

        int prevScore = _currentState != null ? _currentState.score : 0;
        string json = JsonUtility.ToJson(state);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (Mathf.Abs(state.score) < winThreshold)
                {
                    _winSoundPlayed = false;
                }

                _currentState = state;
                _timeoutHandledForTurn = false;
                TriggerPullFeedback(prevScore, state.score);

                if (state.gameStarted)
                {
                    UpdateUI();
                }
                else
                {
                    UpdateLobbyUI();
                }

                onDone?.Invoke(true);
            }
            else
            {
                SetStatus($"Database error: {request.error}");
                onDone?.Invoke(false);
            }
        }
    }

    // ---------------- GAMEPLAY & WORD SUBMISSION ----------------

    private void OnWordSubmitted(string rawInput)
    {
        if (_isSubmitting) return;

        string word = (rawInput ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(word))
        {
            FocusInput();
            return;
        }

        if (word.Equals("RESET", StringComparison.OrdinalIgnoreCase))
        {
            StartCoroutine(HandleReset());
            return;
        }

        if (_currentState == null || !_currentState.gameStarted)
        {
            SetStatus("Waiting for match...");
            FocusInput();
            return;
        }

        if (Mathf.Abs(_currentState.score) >= winThreshold)
        {
            SetStatus("Match over! Type RESET to start a new round.");
            FocusInput();
            return;
        }

        if (_currentState.activePlayerId != _localPlayerId)
        {
            PlayClip(invalidWordClip, invalidWordVolume);
            TriggerFlash(invalidFlashColor);
            string activeName = GetPlayerName(_currentState, _currentState.activePlayerId);
            SetStatus($"Not your turn! Waiting for {activeName} (Team {_currentState.activeTeam}).");
            FocusInput();
            return;
        }

        string upperWord = word.ToUpperInvariant();

        if (!IsAlphabetic(upperWord) || upperWord.Length < 2)
        {
            PlayClip(invalidWordClip, invalidWordVolume);
            TriggerFlash(invalidFlashColor);
            SetStatus("Word must be at least 2 letters (A-Z only).");
            FocusInput();
            return;
        }

        char requiredChar = char.ToUpperInvariant(_currentState.currentWord[_currentState.currentWord.Length - 1]);
        if (upperWord[0] != requiredChar)
        {
            PlayClip(invalidWordClip, invalidWordVolume);
            TriggerFlash(invalidFlashColor);
            SetStatus($"Word must start with '{requiredChar}'.");
            FocusInput();
            return;
        }

        if (IsWordAlreadyUsed(upperWord, _currentState.usedWords))
        {
            PlayClip(invalidWordClip, invalidWordVolume);
            TriggerFlash(invalidFlashColor);
            SetStatus($"'{upperWord}' has already been used.");
            FocusInput();
            return;
        }

        StartCoroutine(ValidateAndSubmitWord(upperWord));
    }

    private IEnumerator HandleReset()
    {
        if (_currentState == null) yield break;

        _isSubmitting = true;
        _winSoundPlayed = false;
        SetStatus("Resetting match...");
        ClearInput();

        List<PlayerInfo> team1 = GetTeamPlayers(_currentState.players, 1);
        int firstId = team1.Count > 0 ? team1[0].id : _localPlayerId;

        RoomState resetState = new RoomState
        {
            roomCode = _currentState.roomCode,
            hostId = _currentState.hostId,
            gameStarted = true,
            players = _currentState.players,
            currentWord = "START",
            activeTeam = 1,
            activePlayerId = firstId,
            team1TurnIndex = 0,
            team2TurnIndex = 0,
            score = 0,
            usedWords = "START,",
            roundDuration = initialRoundDuration,
            turnStartUnix = GetUnixTimeSeconds(),
            lastActionText = "MATCH RESET"
        };

        yield return StartCoroutine(PutRoomState(resetState, null));

        _isSubmitting = false;
        FocusInput();
    }

    private IEnumerator HandleTurnTimeout()
    {
        if (_currentState == null) yield break;

        _isSubmitting = true;
        PlayClip(invalidWordClip, invalidWordVolume);
        TriggerFlash(invalidFlashColor);
        SetStatus("Time's up! Passing turn...");
        ClearInput();

        RoomState nextState = CloneState(_currentState);
        nextState.roundDuration = GetNextRoundDuration(_currentState.roundDuration);
        nextState.turnStartUnix = GetUnixTimeSeconds();
        nextState.lastActionText = $"{_localPlayerName.ToUpperInvariant()} TIMED OUT";
        AdvanceTurn(nextState);

        yield return StartCoroutine(PutRoomState(nextState, null));

        _isSubmitting = false;
        FocusInput();
    }

    private IEnumerator ValidateAndSubmitWord(string upperWord)
    {
        _isSubmitting = true;
        SetStatus($"Checking '{upperWord}'...");

        string dictUrl = DictionaryApiBase + upperWord.ToLowerInvariant();
        bool isValidWord;

        using (UnityWebRequest request = UnityWebRequest.Get(dictUrl))
        {
            yield return request.SendWebRequest();
            isValidWord = request.responseCode == 200;
        }

        if (!isValidWord)
        {
            PlayClip(invalidWordClip, invalidWordVolume);
            TriggerFlash(invalidFlashColor);
            SetStatus($"'{upperWord}' is not a valid English word.");
            _isSubmitting = false;
            FocusInput();
            yield break;
        }

        int pull = CalculateWordPoints(upperWord);
        int myTeam = GetPlayerTeam(_currentState, _localPlayerId);
        int nextScore = myTeam == 1
            ? Mathf.Max(-winThreshold, _currentState.score - pull)
            : Mathf.Min(winThreshold, _currentState.score + pull);

        bool wonMatch = Mathf.Abs(nextScore) >= winThreshold;
        PlayClip(wonMatch ? winClip : validWordClip, wonMatch ? winVolume : validWordVolume);
        TriggerFlash(wonMatch ? winFlashColor : validFlashColor);
        if (wonMatch)
        {
            _winSoundPlayed = true;
        }

        RoomState nextState = CloneState(_currentState);
        nextState.currentWord = upperWord;
        nextState.score = nextScore;
        nextState.usedWords = string.IsNullOrEmpty(_currentState.usedWords)
            ? upperWord + ","
            : _currentState.usedWords + upperWord + ",";
        nextState.roundDuration = GetNextRoundDuration(_currentState.roundDuration);
        nextState.turnStartUnix = GetUnixTimeSeconds();
        nextState.lastActionText = $"{_localPlayerName.ToUpperInvariant()} PLAYED {upperWord} (+{pull} PULL)";
        AdvanceTurn(nextState);

        ClearInput();
        yield return StartCoroutine(PutRoomState(nextState, null));

        _isSubmitting = false;
        FocusInput();
    }

    private static int CalculateWordPoints(string upperWord)
    {
        int len = upperWord.Length;
        if (len <= 3) return 1;
        if (len == 4) return 2;
        if (len == 5) return 3;
        if (len == 6) return 5;
        return len - 1;
    }

    private static void AdvanceTurn(RoomState state)
    {
        List<PlayerInfo> team1 = GetTeamPlayers(state.players, 1);
        List<PlayerInfo> team2 = GetTeamPlayers(state.players, 2);

        if (state.activeTeam == 1)
        {
            if (team1.Count > 0)
            {
                state.team1TurnIndex = (state.team1TurnIndex + 1) % team1.Count;
            }
            if (team2.Count > 0)
            {
                state.activeTeam = 2;
                state.team2TurnIndex %= team2.Count;
                state.activePlayerId = team2[state.team2TurnIndex].id;
            }
            else if (team1.Count > 0)
            {
                state.activePlayerId = team1[state.team1TurnIndex].id;
            }
        }
        else
        {
            if (team2.Count > 0)
            {
                state.team2TurnIndex = (state.team2TurnIndex + 1) % team2.Count;
            }
            if (team1.Count > 0)
            {
                state.activeTeam = 1;
                state.team1TurnIndex %= team1.Count;
                state.activePlayerId = team1[state.team1TurnIndex].id;
            }
            else if (team2.Count > 0)
            {
                state.activePlayerId = team2[state.team2TurnIndex].id;
            }
        }
    }

    // ---------------- UI UPDATES ----------------

    private void UpdateLobbyUI()
    {
        if (_currentState == null) return;

        if (lobbyCodeText != null)
        {
            lobbyCodeText.text = $"LOBBY CODE: {_currentState.roomCode}";
        }

        int count = _currentState.players != null ? _currentState.players.Count : 0;
        if (lobbyPlayerCountText != null)
        {
            lobbyPlayerCountText.text = $"PLAYERS: {count} / {maxPlayers}  ·  PICK A TEAM";
        }

        if (team1RosterText != null)
        {
            team1RosterText.text = BuildLobbyTeamText(_currentState, 1);
        }

        if (team2RosterText != null)
        {
            team2RosterText.text = BuildLobbyTeamText(_currentState, 2);
        }

        bool isHost = _localPlayerId == _currentState.hostId;
        if (startGameButton != null)
        {
            startGameButton.gameObject.SetActive(isHost);
            int t1 = CountPlayersOnTeam(_currentState.players, 1);
            int t2 = CountPlayersOnTeam(_currentState.players, 2);
            startGameButton.interactable = t1 > 0 && t2 > 0;
        }

        if (lobbyStatusText != null)
        {
            int t1 = CountPlayersOnTeam(_currentState.players, 1);
            int t2 = CountPlayersOnTeam(_currentState.players, 2);
            if (t1 == 0 || t2 == 0)
            {
                lobbyStatusText.text = "WAITING FOR AT LEAST 1 PLAYER ON EACH TEAM";
            }
            else if (isHost)
            {
                lobbyStatusText.text = "READY TO START";
            }
            else
            {
                string hostName = GetPlayerName(_currentState, _currentState.hostId);
                lobbyStatusText.text = $"WAITING FOR HOST ({hostName.ToUpperInvariant()}) TO START...";
            }
        }
    }

    private string BuildLobbyTeamText(RoomState state, int team)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"TEAM {team}");
        sb.AppendLine("----------------");

        List<PlayerInfo> list = GetTeamPlayers(state.players, team);
        if (list.Count == 0)
        {
            sb.AppendLine("(empty)");
            return sb.ToString();
        }

        for (int i = 0; i < list.Count; i++)
        {
            PlayerInfo p = list[i];
            string marker = p.id == _localPlayerId ? "> " : "  ";
            string hostTag = p.id == state.hostId ? " [HOST]" : "";
            sb.AppendLine($"{marker}{p.name}{hostTag}");
        }
        return sb.ToString();
    }

    private void UpdateUI()
    {
        if (_currentState == null) return;

        int clamped = Mathf.Clamp(_currentState.score, -winThreshold, winThreshold);

        if (team1ScoreLabel != null)
        {
            team1ScoreLabel.text = $"TEAM 1 [-{winThreshold}]";
        }
        if (team2ScoreLabel != null)
        {
            team2ScoreLabel.text = $"[+{winThreshold}] TEAM 2";
        }
        if (pullValueLabel != null)
        {
            string leader = clamped < 0 ? $"TEAM 1 +{Mathf.Abs(clamped)}"
                : clamped > 0 ? $"TEAM 2 +{clamped}"
                : "EVEN (0)";
            pullValueLabel.text = leader;
        }

        if (scoreText != null)
        {
            scoreText.text = _currentState.lastActionText ?? string.Empty;
        }

        if (gameTeam1RosterText != null)
        {
            gameTeam1RosterText.text = BuildGameTeamRoster(_currentState, 1);
        }

        if (gameTeam2RosterText != null)
        {
            gameTeam2RosterText.text = BuildGameTeamRoster(_currentState, 2);
        }

        if (roundText != null)
        {
            roundText.text = $"ROOM {_currentState.roomCode}  ·  ROUND {GetCurrentRoundNumber()}";
        }

        if (promptText != null)
        {
            string word = _currentState.currentWord.ToUpperInvariant();
            char nextLetter = word[word.Length - 1];
            promptText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(promptAccentColor)}>CHAIN WORD</color>\n{word}\n<size=46%><color=#{ColorUtility.ToHtmlStringRGB(promptAccentColor)}>NEXT LETTER: {nextLetter}  ·  LONGER WORDS = MORE PULL</color></size>";
        }

        UpdateTimerDisplay();

        if (!_isSubmitting)
        {
            if (_currentState.score <= -winThreshold)
            {
                if (!_winSoundPlayed)
                {
                    _winSoundPlayed = true;
                    PlayClip(winClip, winVolume);
                    TriggerFlash(winFlashColor);
                }
                SetStatus("TEAM 1 WINS  ·  TYPE RESET");
            }
            else if (_currentState.score >= winThreshold)
            {
                if (!_winSoundPlayed)
                {
                    _winSoundPlayed = true;
                    PlayClip(winClip, winVolume);
                    TriggerFlash(winFlashColor);
                }
                SetStatus("TEAM 2 WINS  ·  TYPE RESET");
            }
            else
            {
                string activeName = GetPlayerName(_currentState, _currentState.activePlayerId).ToUpperInvariant();
                if (_currentState.activePlayerId == _localPlayerId)
                {
                    SetStatus($"YOUR TURN  ·  TEAM {_currentState.activeTeam} ({activeName})");
                }
                else
                {
                    SetStatus($"TEAM {_currentState.activeTeam}  ·  {activeName}'S TURN");
                }
            }
        }
    }

    private string BuildGameTeamRoster(RoomState state, int team)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"TEAM {team}");
        List<PlayerInfo> list = GetTeamPlayers(state.players, team);
        for (int i = 0; i < list.Count; i++)
        {
            PlayerInfo p = list[i];
            bool isTurn = p.id == state.activePlayerId && Mathf.Abs(state.score) < winThreshold;
            string prefix = isTurn ? ">> " : "   ";
            string youTag = p.id == _localPlayerId ? " (YOU)" : "";
            sb.AppendLine($"{prefix}{p.name}{youTag}");
        }
        return sb.ToString();
    }

    private void UpdateTimerDisplay()
    {
        if (_currentState == null) return;

        int secondsCeil = Mathf.CeilToInt(_remainingTime);
        float ratio = _currentState.roundDuration > 0.01f
            ? Mathf.Clamp01(_remainingTime / _currentState.roundDuration)
            : 0f;
        bool isUrgent = secondsCeil <= 10;

        if (timerBar != null)
        {
            timerBar.fillAmount = ratio;
            if (useMidnightTheme)
            {
                timerBar.color = isUrgent ? timerUrgentColor : timerBarColor;
            }
        }

        if (timerText != null)
        {
            timerText.text = $"{secondsCeil}s";
            if (useMidnightTheme)
            {
                timerText.color = isUrgent ? timerUrgentColor : primaryTextColor;
            }
        }
        else if (statusText != null && !_isSubmitting && Mathf.Abs(_currentState.score) < winThreshold)
        {
            string activeName = GetPlayerName(_currentState, _currentState.activePlayerId).ToUpperInvariant();
            string turnLabel = _currentState.activePlayerId == _localPlayerId
                ? $"YOUR TURN (TEAM {_currentState.activeTeam})"
                : $"TEAM {_currentState.activeTeam} · {activeName}";
            statusText.text = $"{turnLabel}  ·  {secondsCeil}s";
        }
    }

    // ---------------- HELPERS ----------------

    private float GetNextRoundDuration(float currentDuration)
    {
        float baseDuration = currentDuration > 0f ? currentDuration : initialRoundDuration;
        return Mathf.Max(minRoundDuration, baseDuration - timerDecreasePerTurn);
    }

    private int GetCurrentRoundNumber()
    {
        if (_currentState == null || timerDecreasePerTurn <= 0.01f) return 1;
        float dropped = Mathf.Max(0f, initialRoundDuration - _currentState.roundDuration);
        return 1 + Mathf.RoundToInt(dropped / timerDecreasePerTurn);
    }

    private static double GetUnixTimeSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    private static string GenerateRoomCode(int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        }
        return new string(result);
    }

    private static int GetNextAvailablePlayerId(List<PlayerInfo> players)
    {
        int maxId = 0;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].id > maxId) maxId = players[i].id;
        }
        return maxId + 1;
    }

    private static int CountPlayersOnTeam(List<PlayerInfo> players, int team)
    {
        if (players == null) return 0;
        int count = 0;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].team == team) count++;
        }
        return count;
    }

    private static List<PlayerInfo> GetTeamPlayers(List<PlayerInfo> players, int team)
    {
        List<PlayerInfo> result = new List<PlayerInfo>();
        if (players == null) return result;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].team == team) result.Add(players[i]);
        }
        return result;
    }

    private static string GetPlayerName(RoomState state, int playerId)
    {
        if (state == null || state.players == null) return $"P{playerId}";
        for (int i = 0; i < state.players.Count; i++)
        {
            if (state.players[i].id == playerId) return state.players[i].name;
        }
        return $"P{playerId}";
    }

    private static int GetPlayerTeam(RoomState state, int playerId)
    {
        if (state == null || state.players == null) return 1;
        for (int i = 0; i < state.players.Count; i++)
        {
            if (state.players[i].id == playerId) return state.players[i].team;
        }
        return 1;
    }

    private static RoomState CloneState(RoomState src)
    {
        return new RoomState
        {
            roomCode = src.roomCode,
            hostId = src.hostId,
            gameStarted = src.gameStarted,
            players = new List<PlayerInfo>(src.players),
            currentWord = src.currentWord,
            activeTeam = src.activeTeam,
            activePlayerId = src.activePlayerId,
            team1TurnIndex = src.team1TurnIndex,
            team2TurnIndex = src.team2TurnIndex,
            score = src.score,
            usedWords = src.usedWords,
            roundDuration = src.roundDuration,
            turnStartUnix = src.turnStartUnix,
            lastActionText = src.lastActionText
        };
    }

    private static bool IsAlphabetic(string word)
    {
        for (int i = 0; i < word.Length; i++)
        {
            if (!char.IsLetter(word[i])) return false;
        }
        return true;
    }

    private static bool IsWordAlreadyUsed(string upperWord, string usedWords)
    {
        if (string.IsNullOrEmpty(usedWords)) return false;

        string[] tokens = usedWords.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Trim().Equals(upperWord, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private string GetRoomUrl(string roomCode)
    {
        if (string.IsNullOrWhiteSpace(firebaseUrl) || string.IsNullOrWhiteSpace(roomCode)) return null;
        return $"{firebaseUrl.Trim().TrimEnd('/')}/rooms/{roomCode.Trim().ToUpperInvariant()}.json";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void ClearInput()
    {
        if (wordInput != null)
        {
            _previousInputText = string.Empty;
            wordInput.text = string.Empty;
        }
    }

    private void FocusInput()
    {
        if (wordInput != null && gamePanel != null && gamePanel.activeInHierarchy)
        {
            wordInput.ActivateInputField();
            wordInput.Select();
        }
    }
}
