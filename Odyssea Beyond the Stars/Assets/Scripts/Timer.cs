using UnityEngine;
using TMPro;

public class Timer : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float _startTime = 90f; //ceiling默认3分钟

    private float _timeRemaining;
    private TMP_Text _timerText;
    private bool _ended = false;

    private bool _countdownTriggered = false;

    private float TimeRemaining
    {
        get => _timeRemaining;
        set
        {
            _timeRemaining = Mathf.Clamp(value, 0, _startTime); //必须正数 || 0

            int minutes = Mathf.FloorToInt(_timeRemaining / 60);
            int seconds = Mathf.CeilToInt(_timeRemaining % 60);

            if (seconds == 60)
            {
                seconds = 0;
                minutes += 1;
            }

            _timerText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    void Start()
    {
        _timerText = GetComponent<TMP_Text>();
        TimeRemaining = _startTime;
    }

    void Update()
    {
        //先防御空引用并且只在Play状态下运行
        if (GameBehavior.Instance == null) return;

        if (GameBehavior.Instance.PauseTimerForBulletTime) return;

        if (GameBehavior.Instance.State != Utilities.GameState.Play) return;

        if (_timeRemaining > 0f)
        {
            TimeRemaining -= Time.deltaTime;

            if (!_countdownTriggered && _timeRemaining <= 3f)
            {
                _countdownTriggered = true;
                AudioManager.Instance?.PlayCountdown();
            }
        }
        else if (!_ended)
        {
            _ended = true;
            GameBehavior.Instance.CheckEndCondition();
        }
    }
}
