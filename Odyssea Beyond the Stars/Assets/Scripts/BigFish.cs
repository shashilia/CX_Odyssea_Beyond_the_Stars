using UnityEngine;

public class BigFish : CatchableItem
{
    void Awake()
    {
        _itemName = "Big Fish";
        _scoreValue = 20;
        _weight = 2.5f;
    }

    public override void OnCaught()
    {
        base.OnCaught();
        Debug.Log("Wow! A big fish!");
    }
}
