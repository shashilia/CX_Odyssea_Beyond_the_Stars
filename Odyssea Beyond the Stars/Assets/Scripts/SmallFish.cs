using UnityEngine;

public class SmallFish : CatchableItem
{
    void Awake()
    {
        _itemName = "Small Fish";
        _scoreValue = 25;
        _weight = 0.8f;
    }

    public override void OnCaught()
    {
        base.OnCaught();
        Debug.Log("Splash! You caught a small fish!");
    }
}
