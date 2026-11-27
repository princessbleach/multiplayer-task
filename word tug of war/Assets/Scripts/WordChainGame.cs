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
    }

    [Header("Firebase Configuration")]
    [Tooltip("Base URL of your Firebase Realtime Database (e.g. https://your-project-default-rtdb.firebaseio.com)")]
    public string firebaseUrl = "https://your-project-default-rtdb.firebaseio.com";

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
    public TMP_InputField wordInput;

    private const float PollInterval = 0.6f;
    private const int WinThreshold = 15;
    private const string DictionaryApiBase = "https://api.dictionaryapi.dev/api/v2/entries/en/";

    private int _playerId;
    private GameState _currentState;
    private bool _isSubmitting;
    private bool _isPolling;
    private Coroutine _pollCoroutine;

    private void Start()
    {
        if (rolePanel != null) rolePanel.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);

        if (p1Button != null) p1Button.onClick.AddListener(() => JoinAsPlayer(1));
        if (p2Button != null) p2Button.onClick.AddListener(() => JoinAsPlayer(2));

        if (wordInput != null)
        {
            wordInput.onSubmit.AddListener(OnWordSubmitted);
        }
    }

    private void OnDestroy()
    {
        if (p1Button != null) p1Button.onClick.RemoveAllListeners();
        if (p2Button != null) p2Button.onClick.RemoveAllListeners();
        if (wordInput != null) wordInput.onSubmit.RemoveListener(OnWordSubmitted);
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
                        _currentState = fetched;
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
                _currentState = state;
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
            SetStatus($"Not your turn! Waiting for Player {_currentState.turn}.");
            FocusInput();
            return;
        }

        string upperWord = word.ToUpperInvariant();

        if (!IsAlphabetic(upperWord) || upperWord.Length < 2)
        {
            SetStatus("Word must be at least 2 letters (A-Z only).");
            FocusInput();
            return;
        }

        char requiredChar = char.ToUpperInvariant(_currentState.currentWord[_currentState.currentWord.Length - 1]);
        if (upperWord[0] != requiredChar)
        {
            SetStatus($"Word must start with '{requiredChar}'.");
            FocusInput();
            return;
        }

        if (IsWordAlreadyUsed(upperWord, _currentState.usedWords))
        {
            SetStatus($"'{upperWord}' has already been used.");
            FocusInput();
            return;
        }

        StartCoroutine(ValidateAndSubmitWord(upperWord));
    }

    private IEnumerator HandleReset()
    {
        _isSubmitting = true;
        SetStatus("Resetting match...");
        ClearInput();

        yield return StartCoroutine(PutGameState(CreateDefaultState()));

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
            SetStatus($"'{upperWord}' is not a valid English word.");
            _isSubmitting = false;
            FocusInput();
            yield break;
        }

        int pull = Mathf.Max(1, upperWord.Length - 2);
        int nextScore = _playerId == 1
            ? Mathf.Max(-WinThreshold, _currentState.score - pull)
            : Mathf.Min(WinThreshold, _currentState.score + pull);

        GameState nextState = new GameState
        {
            currentWord = upperWord,
            turn = _playerId == 1 ? 2 : 1,
            score = nextScore,
            usedWords = string.IsNullOrEmpty(_currentState.usedWords)
                ? upperWord + ","
                : _currentState.usedWords + upperWord + ","
        };

        ClearInput();
        yield return StartCoroutine(PutGameState(nextState));

        _isSubmitting = false;
        FocusInput();
    }

    private void UpdateUI()
    {
        if (_currentState == null) return;

        if (scoreText != null)
        {
            scoreText.text = BuildTugOfWarDisplay(_currentState.score);
        }

        if (promptText != null)
        {
            string word = _currentState.currentWord.ToUpperInvariant();
            char nextLetter = word[word.Length - 1];
            promptText.text = $"{word}\n<size=55%>NEXT LETTER: {nextLetter}</size>";
        }

        if (!_isSubmitting)
        {
            if (_currentState.score <= -WinThreshold)
            {
                SetStatus("PLAYER 1 WINS!  Type RESET to play again.");
            }
            else if (_currentState.score >= WinThreshold)
            {
                SetStatus("PLAYER 2 WINS!  Type RESET to play again.");
            }
            else if (_currentState.turn == _playerId)
            {
                SetStatus($"YOUR TURN (PLAYER {_playerId})");
            }
            else
            {
                SetStatus($"PLAYER {_currentState.turn}'S TURN...");
            }
        }
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
        return $"P1 [-15]  {new string(track)}  [+15] P2\nSCORE: {sign}{clamped}";
    }

    private static GameState CreateDefaultState()
    {
        return new GameState
        {
            currentWord = "START",
            turn = 1,
            score = 0,
            usedWords = "START,"
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
