using UnityEngine;

public class HookTriggerForwarder : MonoBehaviour
{
    private FishingRodBehavior _rod;

    void Awake()
    {
        _rod = GetComponentInParent<FishingRodBehavior>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_rod != null)
            _rod.OnHookTriggerEnter2D(other);
    }

}
