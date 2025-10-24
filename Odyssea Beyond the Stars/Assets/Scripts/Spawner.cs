using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Spawner : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public GameObject prefab;
        [Min(0f)] public float weight = 1f;
    }

    [Header("Prefabs & Parent")]
    [SerializeField] private Transform _parent;

    [Header("Spawn List (per-level)")]
    [SerializeField] private List<Entry> _entries = new();

    [Header("Population")]
    [SerializeField] private int _maxFish = 7;
    [SerializeField] private float _checkInterval = 0.05f;

    [Header("Water Area (RECOMMENDED)")]
    [SerializeField] private Collider2D _waterArea;
    [SerializeField] private float _edgePadding = 0.1f;

    [Header("Fallback: Polar Area (if no waterArea)")]
    [SerializeField] private float _spawnRadius = 8f;
    [SerializeField] private int _maxAttempts = 40;

    [Header("No-Overlap & Obstacles")]
    [SerializeField] private float _fishRadius = 0.35f;
    [SerializeField] private float _minGap = 0.10f;
    [SerializeField] private LayerMask _obstacleMask;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private Coroutine _loop;
    private Coroutine _bootstrap;

    void OnEnable()
    {
        if (_parent == null) Debug.LogError("[Spawner] _parent 未指定。");
        if (_entries == null || _entries.Count == 0) Debug.LogWarning("[Spawner] _entries 为空，此关卡不会生成任何物体。");

        //先等 GameBehavior 单例就绪，再启动维持循环
        _bootstrap = StartCoroutine(Bootstrap());
    }

    void OnDisable()
    {
        if (_loop != null) StopCoroutine(_loop);
        if (_bootstrap != null) StopCoroutine(_bootstrap);
    }

    //等到 GameBehavior.Instance 可用，再启动 EnsurePopulationLoop
    private IEnumerator Bootstrap()
    {
        //等待单例就绪（防止执行顺序导致的NullReference）
        while (GameBehavior.Instance == null)
            yield return null;

        _loop = StartCoroutine(EnsurePopulationLoop());
    }

    //循环维持数量, 不足上限则每次补 1 个
    private IEnumerator EnsurePopulationLoop()
    {
        var wait = new WaitForSeconds(_checkInterval);

        while (true)
        {
            //清理外部被销毁的null
            for (int i = _spawned.Count - 1; i >= 0; i--)
                if (_spawned[i] == null) _spawned.RemoveAt(i);

            if (_spawned.Count < _maxFish)
            {
                TrySpawnOne();
            }

            yield return wait;
        }
    }

    private void TrySpawnOne()
    {
        if (_parent == null) return;
        if (_spawned.Count >= _maxFish) return;

        var prefab = PickWeighted();
        if (prefab == null) return;

        //确保 GameBehavior 就绪且在 Play
        var gb = GameBehavior.Instance;
        if (gb == null) return;
        if (gb.State != Utilities.GameState.Play) return;

        if (TryGetSpawnPosition(out Vector3 pos))
        {
            GameObject go = Instantiate(prefab, pos, Quaternion.identity, _parent);

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.flipX = Random.value > 0.5f;

            var ci = go.GetComponent<CatchableItem>();
            if (ci != null) ci.SetSpawnerForRespawn(this);

            _spawned.Add(go);
        }
        else
        {
            Debug.LogWarning("[Spawner] 未找到合适的非重叠位置（尝试次数达上限）。");
        }
    }

    //按权重挑一个Prefab
    private GameObject PickWeighted()
    {
        if (_entries == null || _entries.Count == 0) return null;

        float total = 0f;
        foreach (var e in _entries)
            if (e != null && e.prefab != null && e.weight > 0f) total += e.weight;

        if (total <= 0f) return null;

        float r = Random.value * total;
        float acc = 0f;
        foreach (var e in _entries)
        {
            if (e == null || e.prefab == null || e.weight <= 0f) continue;
            acc += e.weight;
            if (r <= acc) return e.prefab;
        }

        return _entries.First(e => e.prefab != null).prefab;
    }

    //优先在 _waterArea 内取点，否则退回到以 _parent 为圆心的圆形范围，这里使用RayCaster
    private bool TryGetSpawnPosition(out Vector3 worldPos)
    {
        if (_waterArea != null)
        {
            var b = _waterArea.bounds;

            for (int attempt = 0; attempt < _maxAttempts; attempt++)
            {
                float x = Random.Range(b.min.x + _edgePadding, b.max.x - _edgePadding);
                float y = Random.Range(b.min.y + _edgePadding, b.max.y - _edgePadding);
                Vector3 candidate = new Vector3(x, y, 0f);

                if (!_waterArea.OverlapPoint(candidate))
                    continue;

                if (_obstacleMask.value != 0)
                {
                    Vector3 center = _parent.position;
                    Vector2 dir = (candidate - center);
                    float dist = dir.magnitude;
                    if (dist > 0.0001f)
                    {
                        RaycastHit2D hit = Physics2D.Raycast(center, dir.normalized, dist, _obstacleMask);
                        if (hit.collider != null) continue;
                    }
                }

                if (TooCloseToExisting(candidate))
                    continue;

                worldPos = candidate;
                return true;
            }

            worldPos = Vector3.zero;
            return false;
        }
        else
        {
            Vector3 center = _parent.position;

            for (int attempt = 0; attempt < _maxAttempts; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float r = Mathf.Sqrt(Random.value) * _spawnRadius;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                Vector3 candidate = center + (Vector3)offset;

                if (_obstacleMask.value != 0)
                {
                    RaycastHit2D hit = Physics2D.Raycast(center, offset.normalized, r, _obstacleMask);
                    if (hit.collider != null) continue;

                    if (Physics2D.OverlapCircle(candidate, _fishRadius, _obstacleMask))
                        continue;
                }

                if (TooCloseToExisting(candidate))
                    continue;

                worldPos = candidate;
                return true;
            }

            worldPos = Vector3.zero;
            return false;
        }
    }

    private bool TooCloseToExisting(Vector3 candidate)
    {
        float minDist = _fishRadius * 2f + _minGap;
        for (int i = 0; i < _spawned.Count; i++)
        {
            var g = _spawned[i];
            if (g == null) continue;

            if (Vector2.Distance(candidate, g.transform.position) < minDist)
                return true;
        }
        return false;
    }

    //被钓走/销毁后延迟补一个（可从 CatchableItem 调用）
    public void RequestRespawn(float delay = 3f)
    {
        StartCoroutine(RespawnAfterDelay(delay));
    }

    private IEnumerator RespawnAfterDelay(float delay)
    {
        float timer = 0f;
        while (timer < delay)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        //调用前确认单例就绪且状态为Play
        var gb = GameBehavior.Instance;
        if (gb != null && gb.State == Utilities.GameState.Play)
        {
            TrySpawnOne();
        }
    }
}
