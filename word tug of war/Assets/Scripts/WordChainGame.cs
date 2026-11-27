using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class WordChainGame : MonoBehaviour
{
    [Serializable]
    public class GameState
    {
        public string currentWord;
        public int turn;
        public int score;
        public string usedWords;
        public float roundDuration;
        public double turnStartUnix;
    }

    [Header("Firebase Configuration")]
    [Tooltip("Base URL of your Firebase Realtime Database (e.g. https://your-project-default-rtdb.firebaseio.com)")]
    public string firebaseUrl = "https://your-project-default-rtdb.firebaseio.com";

    [Header("Timer Settings")]
    [Tooltip("Starting timer duration in seconds for turn 1")]
    public float initialRoundDuration = 60f;
    [Tooltip("How many seconds to subtract after each completed turn")]
    public float timerDecreasePerTurn = 5f;
    [Tooltip("Minimum allowed timer duration per turn in seconds")]
    public float minRoundDuration = 10f;

    [Header("Tug-of-War Sprites")]
    [Tooltip("Transform for Player 1's character (works for both 2D SpriteRenderer and UI Image)")]
    public RectTransform player1Sprite;
    [Tooltip("Transform for Player 2's character (works for both 2D SpriteRenderer and UI Image)")]
    public RectTransform player2Sprite;
    [Tooltip("Optional center anchor; if null, each sprite's starting position is used as center")]
    public RectTransform tugTrackCenter;
    [Tooltip("Max horizontal distance (in UI units or world units) sprites shift at +/-15 score")]
    public float maxPullDistance = 220f;
    [Tooltip("How fast sprites slide toward the current score")]
    public float moveSpeed = 8f;

    [Header("Krillion-Inspired Theme")]
    [Tooltip("Applies a clean, high-contrast midnight-violet Krillion-style UI palette on start")]
    public bool useKrillionTheme = true;
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
    [Tooltip("AudioSource used to play sound effects (auto-added if left empty)")]
    public AudioSource sfxSource;

    public AudioClip buttonClickClip;
    [Range(0f, 2f)] public float buttonClickVolume = 1f;
    [Tooltip("Random +/- pitch variation on button click sounds (0 for exact pitch)")]
    public float buttonPitchJitter = 0.06f;

    public AudioClip typingClip;
    [Range(0f, 2f)] public float typingVolume = 0.35f;
    [Tooltip("Random +/- pitch variation on typing sounds (0 for exact pitch)")]
    public float typingPitchJitter = 0.05f;

    public AudioClip validWordClip;
    [Range(0f, 2f)] public float validWordVolume = 1.5f;

    public AudioClip invalidWordClip;
    [Range(0f, 2f)] public float invalidWordVolume = 1.5f;

    public AudioClip winClip;
    [Range(0f, 2f)] public float winVolume = 1.5f;

    [Tooltip("UI Image or Graphic that flashes color on valid, invalid, or win events")]
    public Graphic flashTarget;
    public Color validFlashColor = new Color(0.18f, 0.85f, 0.55f, 0.5f);
    public Color invalidFlashColor = new Color(0.95f, 0.26f, 0.42f, 0.5f);
    public Color winFlashColor = new Color(0.56f, 0.48f, 1f, 0.6f);
    [Tooltip("How long each color flash lasts in seconds")]
    public float flashDuration = 0.25f;

    [Header("Panels")]
    public GameObject rolePanel;
    public GameObject gamePanel;

    [Header("Role Selection UI")]
    public Button p1Button;
    public Button p2Button;

    [Header("Game UI")]
    public TMP_Text scoreText;
    public TMP_Text promptText;
    public TMP_Text statusText;
    [Tooltip("Optional: displays the current round number.")]
    public TMP_Text roundText;
    [Tooltip("Optional: assign a TMP_Text for the timer. If null, timer shows in statusText.")]
    public TMP_Text timerText;
    [Tooltip("Optional: UI Image set to Type = Filled (Horizontal) for a Krillion-style timer bar.")]
    public Image timerBar;
    public TMP_InputField wordInput;

    private const float PollInterval = 0.6f;
    private const int WinThreshold = 15;
    private const string DictionaryApiBase = "https://api.dictionaryapi.dev/api/v2/entries/en/";

    private int _playerId;
    private GameState _currentState;
    private string _previousInputText = string.Empty;
    private float _remainingTime;
    private bool _timeoutHandledForTurn;
    private bool _winSoundPlayed;
    private bool _isSubmitting;
    private bool _isPolling;
    private Coroutine _pollCoroutine;
    private Coroutine _flashCoroutine;

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

        if (useKrillionTheme)
        {
            ApplyKrillionTheme();
        }

        CaptureBaseSpritePositions();
        CaptureFlashBaseColor();

        if (rolePanel != null) rolePanel.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);

        if (p1Button != null)
        {
            p1Button.onClick.AddListener(PlayButtonClick);
            p1Button.onClick.AddListener(() => JoinAsPlayer(1));
        }

        if (p2Button != null)
        {
            p2Button.onClick.AddListener(PlayButtonClick);
            p2Button.onClick.AddListener(() => JoinAsPlayer(2));
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
        UpdateTugSprites();

        if (_currentState == null || gamePanel == null || !gamePanel.activeInHierarchy)
            return;

        if (Mathf.Abs(_currentState.score) >= WinThreshold)
            return;

        double now = GetUnixTimeSeconds();
        double elapsed = Math.Max(0.0, now - _currentState.turnStartUnix);
        _remainingTime = Mathf.Max(0f, _currentState.roundDuration - (float)elapsed);

        UpdateTimerDisplay();

        if (_remainingTime <= 0f && !_isSubmitting && _currentState.turn == _playerId && !_timeoutHandledForTurn)
        {
            _timeoutHandledForTurn = true;
            StartCoroutine(HandleTurnTimeout());
        }
    }

    private void OnDestroy()
    {
        if (p1Button != null) p1Button.onClick.RemoveAllListeners();
        if (p2Button != null) p2Button.onClick.RemoveAllListeners();
        if (wordInput != null)
        {
            wordInput.onValueChanged.RemoveListener(OnWordValueChanged);
            wordInput.onSubmit.RemoveListener(OnWordSubmitted);
        }
    }

    private void ApplyKrillionTheme()
    {
        StylePanel(rolePanel, backdropColor);
        StylePanel(gamePanel, backdropColor);

        StyleButton(p1Button);
        StyleButton(p2Button);

        if (scoreText != null) scoreText.color = primaryTextColor;
        if (promptText != null) promptText.color = primaryTextColor;
        if (statusText != null) statusText.color = promptAccentColor;
        if (roundText != null) roundText.color = promptAccentColor;
        if (timerText != null) timerText.color = primaryTextColor;
        if (timerBar != null) timerBar.color = timerBarColor;

        if (wordInput != null)
        {
            if (wordInput.targetGraphic != null)
            {
                wordInput.targetGraphic.color = cardSurfaceColor;
            }
            if (wordInput.textComponent != null)
            {
                wordInput.textComponent.color = primaryTextColor;
            }
            if (wordInput.placeholder is TMP_Text placeholderTmp)
            {
                placeholderTmp.color = new Color(promptAccentColor.r, promptAccentColor.g, promptAccentColor.b, 0.5f);
            }
        }
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

        if (_currentState != null && _currentState.turn == _playerId && Mathf.Abs(_currentState.score) < WinThreshold)
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

    private void UpdateTugSprites()
    {
        if (player1Sprite == null && player2Sprite == null) return;
        CaptureBaseSpritePositions();

        int score = _currentState != null ? Mathf.Clamp(_currentState.score, -WinThreshold, WinThreshold) : 0;
        float normalizedOffset = (float)score / WinThreshold;
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

    public void JoinAsPlayer(int playerNumber)
    {
        _playerId = playerNumber;

        if (rolePanel != null) rolePanel.SetActive(false);
        if (gamePanel != null) gamePanel.SetActive(true);

        if (string.IsNullOrWhiteSpace(firebaseUrl) || firebaseUrl.Contains("your-project"))
        {
            SetStatus("Set firebaseUrl on GameManager in the Inspector.");
        }

        if (_playerId == 1)
        {
            StartCoroutine(ResetAndStartPolling());
        }
        else
        {
            StartPolling();
        }

        FocusInput();
    }

    private IEnumerator ResetAndStartPolling()
    {
        SetStatus("Initializing room...");
        yield return StartCoroutine(PutGameState(CreateDefaultState()));
        StartPolling();
    }

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
            if (!_isSubmitting && !_isPolling)
            {
                yield return StartCoroutine(FetchGameState());
            }
            yield return wait;
        }
    }

    private IEnumerator FetchGameState()
    {
        string url = GetRoomUrl();
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
                    GameState fetched = JsonUtility.FromJson<GameState>(json);
                    if (fetched != null && !string.IsNullOrEmpty(fetched.currentWord))
                    {
                        int prevScore = _currentState != null ? _currentState.score : 0;
                        bool turnChanged = _currentState == null ||
                                           fetched.turn != _currentState.turn ||
                                           fetched.currentWord != _currentState.currentWord ||
                                           Math.Abs(fetched.turnStartUnix - _currentState.turnStartUnix) > 0.01;

                        if (fetched.roundDuration <= 0f)
                        {
                            fetched.roundDuration = initialRoundDuration;
                        }

                        if (Mathf.Abs(fetched.score) < WinThreshold)
                        {
                            _winSoundPlayed = false;
                        }

                        _currentState = fetched;
                        if (turnChanged)
                        {
                            _timeoutHandledForTurn = false;
                            TriggerPullFeedback(prevScore, fetched.score);
                        }

                        UpdateUI();
                    }
                }
                else if (_currentState == null)
                {
                    SetStatus("Waiting for Player 1 to initialize room...");
                }
            }
            else
            {
                SetStatus($"Sync error: {request.error}");
            }
        }
        _isPolling = false;
    }

    private IEnumerator PutGameState(GameState state)
    {
        string url = GetRoomUrl();
        if (string.IsNullOrEmpty(url)) yield break;

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
                if (Mathf.Abs(state.score) < WinThreshold)
                {
                    _winSoundPlayed = false;
                }

                _currentState = state;
                _timeoutHandledForTurn = false;
                TriggerPullFeedback(prevScore, state.score);
                UpdateUI();
            }
            else
            {
                SetStatus($"Database error: {request.error}");
            }
        }
    }

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

        if (_currentState == null)
        {
            SetStatus("Waiting for room state...");
            FocusInput();
            return;
        }

        if (Mathf.Abs(_currentState.score) >= WinThreshold)
        {
            SetStatus("Game over! Type RESET to start a new match.");
            FocusInput();
            return;
        }

        if (_currentState.turn != _playerId)
        {
            PlayClip(invalidWordClip, invalidWordVolume);
            TriggerFlash(invalidFlashColor);
            SetStatus($"Not your turn! Waiting for Player {_currentState.turn}.");
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
        _isSubmitting = true;
        _winSoundPlayed = false;
        SetStatus("Resetting match...");
        ClearInput();

        yield return StartCoroutine(PutGameState(CreateDefaultState()));

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

        float nextDuration = GetNextRoundDuration(_currentState.roundDuration);

        GameState nextState = new GameState
        {
            currentWord = _currentState.currentWord,
            turn = _playerId == 1 ? 2 : 1,
            score = _currentState.score,
            usedWords = _currentState.usedWords,
            roundDuration = nextDuration,
            turnStartUnix = GetUnixTimeSeconds()
        };

        yield return StartCoroutine(PutGameState(nextState));

        _isSubmitting = false;
        FocusInput();
    }

    private IEnumerator ValidateAndSubmitWord(string upperWord)
    {
        _isSubmitting = true;
        SetStatus($"Checking '{upperWord}'...");

        string dictUrl = DictionaryApiBase + upperWord.ToLowerInvariant();
        bool isValidWord = false;

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

        int pull = Mathf.Max(1, upperWord.Length - 2);
        int nextScore = _playerId == 1
            ? Mathf.Max(-WinThreshold, _currentState.score - pull)
            : Mathf.Min(WinThreshold, _currentState.score + pull);

        bool wonMatch = Mathf.Abs(nextScore) >= WinThreshold;
        PlayClip(wonMatch ? winClip : validWordClip, wonMatch ? winVolume : validWordVolume);
        TriggerFlash(wonMatch ? winFlashColor : validFlashColor);
        if (wonMatch)
        {
            _winSoundPlayed = true;
        }

        float nextDuration = GetNextRoundDuration(_currentState.roundDuration);

        GameState nextState = new GameState
        {
            currentWord = upperWord,
            turn = _playerId == 1 ? 2 : 1,
            score = nextScore,
            usedWords = string.IsNullOrEmpty(_currentState.usedWords)
                ? upperWord + ","
                : _currentState.usedWords + upperWord + ",",
            roundDuration = nextDuration,
            turnStartUnix = GetUnixTimeSeconds()
        };

        ClearInput();
        yield return StartCoroutine(PutGameState(nextState));

        _isSubmitting = false;
        FocusInput();
    }

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

    private void UpdateUI()
    {
        if (_currentState == null) return;

        if (scoreText != null)
        {
            scoreText.text = BuildTugOfWarDisplay(_currentState.score);
        }

        if (roundText != null)
        {
            roundText.text = $"ROUND {GetCurrentRoundNumber()}";
        }

        if (promptText != null)
        {
            string word = _currentState.currentWord.ToUpperInvariant();
            char nextLetter = word[word.Length - 1];
            promptText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(promptAccentColor)}>CHAIN WORD</color>\n{word}\n<size=52%><color=#{ColorUtility.ToHtmlStringRGB(promptAccentColor)}>NEXT LETTER: {nextLetter}</color></size>";
        }

        UpdateTimerDisplay();

        if (!_isSubmitting)
        {
            if (_currentState.score <= -WinThreshold)
            {
                if (!_winSoundPlayed)
                {
                    _winSoundPlayed = true;
                    PlayClip(winClip, winVolume);
                    TriggerFlash(winFlashColor);
                }
                SetStatus("PLAYER 1 WINS  ·  TYPE RESET");
            }
            else if (_currentState.score >= WinThreshold)
            {
                if (!_winSoundPlayed)
                {
                    _winSoundPlayed = true;
                    PlayClip(winClip, winVolume);
                    TriggerFlash(winFlashColor);
                }
                SetStatus("PLAYER 2 WINS  ·  TYPE RESET");
            }
            else if (_currentState.turn == _playerId)
            {
                SetStatus($"YOUR TURN  ·  PLAYER {_playerId}");
            }
            else
            {
                SetStatus($"PLAYER {_currentState.turn}'S TURN");
            }
        }
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
            if (useKrillionTheme)
            {
                timerBar.color = isUrgent ? timerUrgentColor : timerBarColor;
            }
        }

        if (timerText != null)
        {
            timerText.text = $"{secondsCeil}s";
            if (useKrillionTheme)
            {
                timerText.color = isUrgent ? timerUrgentColor : primaryTextColor;
            }
        }
        else if (statusText != null && !_isSubmitting && Mathf.Abs(_currentState.score) < WinThreshold)
        {
            string turnLabel = _currentState.turn == _playerId
                ? $"YOUR TURN (P{_playerId})"
                : $"P{_currentState.turn}'S TURN";
            statusText.text = $"{turnLabel}  ·  {secondsCeil}s";
        }
    }

    private static double GetUnixTimeSeconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    private static string BuildTugOfWarDisplay(int score)
    {
        const int halfWidth = 15;
        int clamped = Mathf.Clamp(score, -WinThreshold, WinThreshold);
        int markerIndex = clamped + halfWidth;

        char[] track = new char[halfWidth * 2 + 1];
        for (int i = 0; i < track.Length; i++)
        {
            track[i] = i == halfWidth ? '|' : '-';
        }
        track[markerIndex] = 'O';

        string sign = clamped > 0 ? "+" : "";
        return $"P1 [-15]  {new string(track)}  [+15] P2\nPULL: {sign}{clamped}";
    }

    private GameState CreateDefaultState()
    {
        return new GameState
        {
            currentWord = "START",
            turn = 1,
            score = 0,
            usedWords = "START,",
            roundDuration = initialRoundDuration,
            turnStartUnix = GetUnixTimeSeconds()
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

    private string GetRoomUrl()
    {
        if (string.IsNullOrWhiteSpace(firebaseUrl)) return null;
        return $"{firebaseUrl.Trim().TrimEnd('/')}/room1.json";
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