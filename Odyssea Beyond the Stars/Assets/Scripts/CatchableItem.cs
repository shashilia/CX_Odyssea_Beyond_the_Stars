using UnityEngine;

public class CatchableItem : MonoBehaviour
{
    [Header("Base Catchable Settings")]
    [SerializeField] protected int _scoreValue = 10;
    [SerializeField] protected string _itemName = "Fish";
    [SerializeField] protected float _weight = 1.0f; //这个没有用到，有后可能有用。。。
    [SerializeField] protected float _respawnDelay = 3.0f;

    private Spawner _spawner;

    public void SetSpawnerForRespawn(Spawner spawner)
    {
        _spawner = spawner;
    }

    public virtual void OnCaught()
    {
        GameBehavior.Instance.AddScore(_scoreValue);
        Debug.Log($"{_itemName} was caught! +{_scoreValue} points.");

        //通知Spawner延迟再生
        if (_spawner != null)
            _spawner.RequestRespawn(_respawnDelay);

        //销毁自身
        Destroy(gameObject);
    }

    public int GetScoreValue() => _scoreValue;
}
