using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    //RTPC和其他各种名称与Wwise 保持一致
    private const string RTPC_ScoreRatio = "ScoreRatio";

    [Header("Music Events")]
    [SerializeField] private string _playMusicEvent = "Play_MX";
    [SerializeField] private string _stopMusicEvent = "Stop_MX";

    [Header("SFX Events")]
    [SerializeField] private string _castEvent = "Play_SFX_Cast";
    [SerializeField] private string _catchEvent = "Play_SFX_Catch";
    [SerializeField] private string _countdownEvent = "Play_Countdown";

    [Header("Pause Events")]
    [SerializeField] private string _pauseEvent = "PauseFilter_MX";
    [SerializeField] private string _resetPauseEvent = "ResetPauseFilter_MX";

    private uint _musicPlayingId = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        //确保 SoundBank 已加载（AkBank 组件会做）
        //启动音乐并初始RTPC到0
        _musicPlayingId = AkUnitySoundEngine.PostEvent(_playMusicEvent, gameObject);
        SetScoreRatio01(0f);
    }

    public void StopMusic()
    {
        AkUnitySoundEngine.PostEvent(_stopMusicEvent, gameObject);
        _musicPlayingId = AkUnitySoundEngine.AK_INVALID_PLAYING_ID;
    }

    public void SetScoreRatio01(float ratio01)
    {
        float clamped = Mathf.Clamp01(ratio01);
        AkUnitySoundEngine.SetRTPCValue(RTPC_ScoreRatio, clamped * 100f, gameObject);
    }

    public void PlayCastSFX()    => AkUnitySoundEngine.PostEvent(_castEvent, gameObject);
    public void PlayCatchSFX()   => AkUnitySoundEngine.PostEvent(_catchEvent, gameObject);
    public void PlayCountdown() => AkUnitySoundEngine.PostEvent(_countdownEvent, gameObject);
    public void ApplyPauseFilter()  => AkSoundEngine.PostEvent("PauseFilter_MX", gameObject);
    public void ResetPauseFilter()  => AkSoundEngine.PostEvent("ResetPauseFilter_MX", gameObject);
}
