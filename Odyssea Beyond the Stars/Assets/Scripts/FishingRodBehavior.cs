using UnityEngine;
using System.Collections;

public class FishingRodBehavior : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _rodTip;
    [SerializeField] private Transform _hook;
    [SerializeField] private LineRenderer _line;
    [SerializeField] private LayerMask _catchableMask;

    [Header("Swing")]
    [SerializeField] private float _maxSwingAngle = 60f;
    [SerializeField] private float _swingSpeedHz = 1.8f;

    [Header("Casting")]
    [SerializeField] private float _idleLineLength = 0.4f;
    [SerializeField] private float _extendSpeed = 8f;
    [SerializeField] private float _retractSpeed = 12f;
    [SerializeField] private float _maxLineLength = 5f;

    private float _currentLength = 0f;
    private bool _busy = false;
    private Transform _caught = null;
    private Vector3 _caughtOffset = Vector3.zero;

    private float _swingTime = 0f;
    private Vector3 _savedTipPos;
    private Quaternion _savedTipRot;
    private Vector3 _castDir;

    // ===== Bullet Time helpers =====
    private bool InPlay =>
        GameBehavior.Instance != null && GameBehavior.Instance.State == Utilities.GameState.Play;

    private bool InBulletTime =>
        GameBehavior.Instance != null && GameBehavior.Instance.IsBulletTimeActive;

    //Swing在子弹时间里用未缩放时间 * 系数（让它只慢一点点）
    private float DtSwing =>
        InBulletTime ? Time.unscaledDeltaTime * GameBehavior.Instance.SwingFactorInBullet
                     : Time.deltaTime;

    //Retract / Extend 在子弹时间里用未缩放时间（保持原速）
    private float DtRetract =>
        InBulletTime ? Time.unscaledDeltaTime : Time.deltaTime;

    void Awake()
    {
        if (_line == null) _line = GetComponent<LineRenderer>();
        _line.positionCount = 2;
        _line.useWorldSpace = true;

        if (_rodTip != null && _hook != null)
            _hook.position = _rodTip.position;

        UpdateLine();
    }

    void Update()
    {
        // 1) 如果不忙，展示摆动与待机短线
        if (!_busy && InPlay)
        {
            //用 DtSwing 控制相位推进：Bullet 时只慢一点
            _swingTime += DtSwing;

            float phase = _swingTime * _swingSpeedHz * Mathf.PI * 2f;
            float angle = _maxSwingAngle * Mathf.Sin(phase);

            _rodTip.rotation = Quaternion.Euler(0f, 0f, angle);

            //待机钩子跟随竿尖方向上一小段，肉眼可见“在摆动”
            Vector3 idleDir = -_rodTip.up;
            _hook.position = _rodTip.position + idleDir * _idleLineLength;

            if (Input.GetMouseButtonDown(0))
            {
                AudioManager.Instance?.PlayCastSFX();
                StartCoroutine(CastAndRetract());
            }
        }

        // 2) 每帧更新鱼线两端
        UpdateLine();
    }

    private IEnumerator CastAndRetract()
    {
        _busy = true;
        _caught = null;

        //缓存抛线瞬间的竿尖位置/朝向，并据此确定方向
        _savedTipPos = _rodTip.position;
        _savedTipRot = _rodTip.rotation;
        _castDir     = -_rodTip.up;  // 与待机idleDir一致

        //抛线改为 DtRetract，让子弹时间时速度不变
        while (_currentLength < _maxLineLength && _caught == null)
        {
            _currentLength += _extendSpeed * DtRetract;
            _hook.position = _savedTipPos + _castDir * _currentLength;
            yield return null;
        }

        //收线使用 DtRetract，Bullet 时不变慢
        while (_currentLength > 0f)
        {
            _currentLength -= _retractSpeed * DtRetract;
            float len = Mathf.Max(0f, _currentLength);
            _hook.position = _savedTipPos + _castDir * len;

            if (_caught != null)
                _caught.position = _hook.position + _caughtOffset;

            yield return null;
        }

        //恢复抛线时的竿尖旋转，并把钩子放回“闲置短线”的位置
        _currentLength   = 0f;
        _rodTip.rotation = _savedTipRot;
        _hook.position   = _savedTipPos + _castDir * _idleLineLength;

        _busy = false;
    }

    private void UpdateLine()
    {
        if (_line == null || _rodTip == null || _hook == null) return;
        _line.SetPosition(0, _rodTip.position);
        _line.SetPosition(1, _hook.position);
    }

    public void OnHookTriggerEnter2D(Collider2D other)
    {
        if (!_busy || _caught != null) return;

        //只钩可钓物Tag"CI"
        if (!other.CompareTag("CI")) return;

        var item = other.GetComponent<CatchableItem>();
        if (item == null) return;

        AudioManager.Instance?.PlayCatchSFX();
        _caught = other.transform;
        _caughtOffset = Vector3.zero;

        item.OnCaught();
    }
}
