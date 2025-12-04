using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameBehavior : MonoBehaviour
{
    // === Singleton ===
    public static GameBehavior Instance { get; private set; }

    // === UI References ===
    [Header("Score UI")]
    [SerializeField] private TMP_Text _scoreTextUI;
    [SerializeField] private TMP_Text _targetScoreUI;

    [Header("State UI")]
    [SerializeField] private TMP_Text _pauseUI;
    [SerializeField] private TMP_Text _timesUpUI;
    [SerializeField] private TMP_Text _winUI;

    // === Game Logic ===
    private int _score;
    private int _winScoreThreshold;
    private Utilities.GameState _state;

    // === Bullet Time ===
    [Header("Bullet Time Settings")]
    [SerializeField, Range(0.05f, 1f)] private float _bulletSlowFactor = 0.2f;
    [SerializeField] private float _bulletDuration = 10f;
    [SerializeField, Range(0f, 1f)] private float _swingFactorInBullet = 0.6f;

    [Header("Bullet Time Visual Cue")]
    [SerializeField] private Renderer _playerRenderer;
    [SerializeField] private Material _matPowerUp;
    [SerializeField] private Material _matStandard;

    private bool _bulletTimeActive = false;
    private bool _bulletUsed = false;
    private float _defaultFixedDelta;

    // === Wwise RTPC ===
    private float _scoreRatio01 = 0f;

    // === Public read-only properties ===
    public bool PauseTimerForBulletTime { get; private set; } = false;
    public bool IsBulletTimeActive => _bulletTimeActive;
    public float SwingFactorInBullet => _swingFactorInBullet;

    // === WWise Low Pass Filter Event ===
    private bool _musicFiltered = false;

    // === Speed Run Mode ===
    private const string SPEED_RUN_SCENE = "SpeedRun";
    private const string SPEED_RUN_HIGHSCORE_KEY = "SpeedRunHighScore";

    private void ApplyFilterOn()
    {
        if (_musicFiltered) return;
        AudioManager.Instance?.ApplyPauseFilter();
        _musicFiltered = true;
    }

    private void ApplyFilterOff()
    {
        if (!_musicFiltered) return;
        AudioManager.Instance?.ResetPauseFilter();
        _musicFiltered = false;
    }

    public int Score
    {
        get => _score;
        private set
        {
            _score = value;
            if (_scoreTextUI != null)
                _scoreTextUI.text = _score.ToString();
            UpdateMusicByScore();
        }
    }

    public Utilities.GameState State
    {
        get => _state;
        private set
        {
            _state = value;

            if (_pauseUI != null)   _pauseUI.enabled   = (_state == Utilities.GameState.Pause);
            if (_timesUpUI != null) _timesUpUI.enabled = (_state == Utilities.GameState.TimesUp);
            if (_winUI != null)     _winUI.enabled     = (_state == Utilities.GameState.Win);

            //离开Play状态时还原子弹时间以防出bug！！！
            if (_state != Utilities.GameState.Play && _bulletTimeActive)
                ForceEndBulletTime();

            switch (_state)
            {
                case Utilities.GameState.Pause:
                case Utilities.GameState.TimesUp:
                case Utilities.GameState.Win:
                    ApplyFilterOn();
                    break;

                case Utilities.GameState.Play:
                default:
                    ApplyFilterOff();
                    break;
            }
        }
    }

    // === Unity Lifecycle ===
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("GameBehavior instance initialized.");
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            Debug.Log("Duplicate GameBehavior destroyed.");
            return;
        }

        _defaultFixedDelta = Time.fixedDeltaTime;
    }

    private void Start()
    {
        State = Utilities.GameState.Play;

        if (_pauseUI)   _pauseUI.enabled   = false;
        if (_timesUpUI) _timesUpUI.enabled = false;
        if (_winUI)     _winUI.enabled     = false;

        Score = 0;
        SetWinThreshold();
        _bulletUsed = false;
        AudioManager.Instance?.SetScoreRatio01(0f);
    }

    private void Update()
    {
        switch (State)
        {
            case Utilities.GameState.Play:
                if (Input.GetKeyDown(KeyCode.P))
                    State = Utilities.GameState.Pause;

                if (Input.GetKeyDown(KeyCode.Escape))
                    State = Utilities.GameState.Pause;

                if (Input.GetKeyDown(KeyCode.B))
                    ActivateBulletTime(_bulletDuration);
                break;

            case Utilities.GameState.Pause:
                if (Input.GetKeyDown(KeyCode.P))
                    State = Utilities.GameState.Play;

                //Pause时按Esc退出游戏
                if (Input.GetKeyDown(KeyCode.Escape))
                    QuitGame();
                break;

            case Utilities.GameState.TimesUp:
                if (Score >= _winScoreThreshold)
                    State = Utilities.GameState.Win;

                string scene01 = SceneManager.GetActiveScene().name;
                if (Input.GetKeyDown(KeyCode.R))
                    ReloadScene(scene01);

                if (Input.GetKeyDown(KeyCode.Escape))
                    QuitGame();
                break;

            case Utilities.GameState.Win:
                string scene02 = SceneManager.GetActiveScene().name;
                if (scene02 == SPEED_RUN_SCENE)
                {
                    if (Input.GetKeyDown(KeyCode.R))
                        ReloadScene(scene02);

                }

                if (Input.GetKeyDown(KeyCode.Return))
                    LoadNextScene(scene02);

                if (Input.GetKeyDown(KeyCode.Escape))
                    QuitGame();

                break;

            default:
                if (Input.GetKeyDown(KeyCode.P))
                    State = Utilities.GameState.Play;
                break;
        }
    }

    private void QuitGame()
    {
        Debug.Log("Quit Game triggered by ESC");
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

    // === Core Logic ===
    public void AddScore(int amount) => Score += amount;

    private void SetWinThreshold()
    {
        string scene = SceneManager.GetActiveScene().name;

        if (scene == SPEED_RUN_SCENE)
        {
            // Speed Run：目标分数 = 历史最高分（第一次玩默认为 0）
            _winScoreThreshold = PlayerPrefs.GetInt(SPEED_RUN_HIGHSCORE_KEY, 0);
        }
        else
        {
            _winScoreThreshold = scene switch
            {
                "Level 1" => 200,
                "Level 2" => 500,
                "Level 3" => 1000,
                _ => 200
            };
        }

        if (_targetScoreUI != null)
            _targetScoreUI.text = _winScoreThreshold.ToString();

        Debug.Log($"Win/target threshold for {scene}: {_winScoreThreshold}");
    }


    public void CheckEndCondition()
    {
        string scene = SceneManager.GetActiveScene().name;

        // Speed Run 特殊处理：不再是“过关分数”，而是“打破纪录没”
        if (scene == SPEED_RUN_SCENE)
        {
            int previousRecord = PlayerPrefs.GetInt(SPEED_RUN_HIGHSCORE_KEY, 0);
            bool beatRecord = Score > previousRecord;

            if (beatRecord)
            {
                // 存新纪录
                PlayerPrefs.SetInt(SPEED_RUN_HIGHSCORE_KEY, Score);
                PlayerPrefs.Save();
            }

            // 更新当前这一局之后的 Record 显示
            _winScoreThreshold = PlayerPrefs.GetInt(SPEED_RUN_HIGHSCORE_KEY, 0);
            if (_targetScoreUI != null)
                _targetScoreUI.text = _winScoreThreshold.ToString();

            // 打破纪录 -> Win，不然 -> TimesUp
            State = beatRecord ? Utilities.GameState.Win : Utilities.GameState.TimesUp;
        }
        else
        {
            // 普通关卡保持原来的逻辑
            if (Score >= _winScoreThreshold)
                State = Utilities.GameState.Win;
            else
                State = Utilities.GameState.TimesUp;
        }
    }

    public static void ResetSpeedRunHighScore()
    {
        // 删除 SpeedRun 的最高分记录
        PlayerPrefs.DeleteKey(SPEED_RUN_HIGHSCORE_KEY);
        PlayerPrefs.Save();

        Debug.Log("SpeedRun high score reset.");

        // 如果此时刚好在 SpeedRun 场景里，就顺便把 UI 也刷新一下
        if (Instance != null && SceneManager.GetActiveScene().name == SPEED_RUN_SCENE)
        {
            Instance._winScoreThreshold = 0;

            if (Instance._targetScoreUI != null)
                Instance._targetScoreUI.text = "0";

            // 音乐 RTPC 也清零
            AudioManager.Instance?.SetScoreRatio01(0f);
        }
    }

    // === Bullet Time ===
    public void ActivateBulletTime(float durationSeconds)
    {
        if (State != Utilities.GameState.Play) return;
        if (_bulletTimeActive) return;
        if (_bulletUsed) return; // 每关仅一次

        _bulletUsed = true;
        StartCoroutine(Co_BulletTime(durationSeconds));
    }

    private IEnumerator Co_BulletTime(float seconds)
    {
        _bulletTimeActive = true;
        PauseTimerForBulletTime = true;

        //全局时间慢放
        Time.timeScale = _bulletSlowFactor;
        Time.fixedDeltaTime = _defaultFixedDelta * _bulletSlowFactor;

        //整个Bullet Time期间变成高亮材质（黄色或发光）
        if (_playerRenderer != null && _matPowerUp != null)
            _playerRenderer.material = _matPowerUp;

        //启动视觉闪烁提示（只在最后阶段闪）
        if (_playerRenderer != null)
            StartCoroutine(Co_FlickerWarning(seconds));

        yield return new WaitForSecondsRealtime(seconds);

        ForceEndBulletTime();
    }

    private IEnumerator Co_FlickerWarning(float totalDuration)
    {
        if (_playerRenderer == null) yield break;

        yield return new WaitForSecondsRealtime(totalDuration * 0.7f);

        float remaining = totalDuration * 0.3f;
        int flickerCount = 10;
        float interval = remaining / (flickerCount * 2f);

        for (int i = 0; i < flickerCount * 2; i++)
        {
            if (_playerRenderer != null)
            {
                //在 PowerUp和Standard材质之间交替闪烁
                _playerRenderer.material = (i % 2 == 0) ? _matPowerUp : _matStandard;
            }
            yield return new WaitForSecondsRealtime(interval);
        }

        //防止提前闪完
        if (_playerRenderer != null)
            _playerRenderer.material = _matPowerUp;
    }

    private void ForceEndBulletTime()
    {
        StopAllCoroutines(); //停掉闪烁协程，防止它继续改颜色
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _defaultFixedDelta;
        PauseTimerForBulletTime = false;
        _bulletTimeActive = false;

        //结束后恢复标准材质，一定要加，不加的话会一直是黄色的！！！
        if (_playerRenderer != null && _matStandard != null)
            _playerRenderer.material = _matStandard;
    }

    // === Wwise Intialization ===
    private void UpdateMusicByScore()
    {
        if (_winScoreThreshold <= 0) return;

        //计算分数比例（0~1），而不是Wwise中的0-100
        _scoreRatio01 = Mathf.Clamp01((float)_score / _winScoreThreshold);

        if (_score >= _winScoreThreshold)
            _scoreRatio01 = 1f;

        AudioManager.Instance?.SetScoreRatio01(_scoreRatio01);
    }

    // === Scene Utilities ===
    private void ReloadScene(string currentScene)
    {
        switch (currentScene)
        {
            case "Level 1": SceneManager.LoadScene("Level 1"); break;
            case "Level 2": SceneManager.LoadScene("Level 2"); break;
            case SPEED_RUN_SCENE: SceneManager.LoadScene(SPEED_RUN_SCENE); break;
            default:        SceneManager.LoadScene("Level 1"); break;
        }

        // 下面这几行你原来就有，保留就行
        SetWinThreshold();
        State = Utilities.GameState.Play;
        _bulletUsed = false;
        AudioManager.Instance?.SetScoreRatio01(0f);
    }

    private void LoadNextScene(string currentScene)
    {
        switch (currentScene)
        {
            case "Level 1": SceneManager.LoadScene("Level 2"); break;
            // 以后如果想要加level 3： case "Level 2": SceneManager.LoadScene("Level 3"); break;
            case "Level 2": SceneManager.LoadScene("Thanks"); break;
            case SPEED_RUN_SCENE: SceneManager.LoadScene(SPEED_RUN_SCENE); break; // Win 后再来一把
            default:        SceneManager.LoadScene("Level 1"); break;
        }

        SetWinThreshold();
        State = Utilities.GameState.Play;
        _bulletUsed = false;
        AudioManager.Instance?.SetScoreRatio01(0f);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _pauseUI       = GameObject.FindWithTag("PauseText")?.GetComponent<TMP_Text>();
        _scoreTextUI   = GameObject.FindWithTag("ScoreText")?.GetComponent<TMP_Text>();
        _targetScoreUI = GameObject.FindWithTag("TargetText")?.GetComponent<TMP_Text>();
        _timesUpUI     = GameObject.FindWithTag("TimeUpText")?.GetComponent<TMP_Text>();
        _winUI         = GameObject.FindWithTag("WinText")?.GetComponent<TMP_Text>();

        Score = 0;
        SetWinThreshold();
        _bulletUsed = false;

        //每次加载新关卡时用Tag来重新绑定Player Renderer
        if (_playerRenderer == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                _playerRenderer = player.GetComponent<Renderer>();
        }

        //强制还原为普通材质，要不然仍然可能会是黄黄的...
        if (_playerRenderer != null && _matStandard != null)
            _playerRenderer.material = _matStandard;

        if (_bulletTimeActive)
            ForceEndBulletTime();

        AudioManager.Instance?.SetScoreRatio01(0f);
    }

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_bulletTimeActive) ForceEndBulletTime();
    }
    private void OnDestroy()
    {
        if (_bulletTimeActive) ForceEndBulletTime();
    }
}
