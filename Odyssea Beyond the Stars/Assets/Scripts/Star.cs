using UnityEngine;

public class Star : CatchableItem
{
    void Awake()
    {
        _itemName = "Star";
        _scoreValue = 30;
        _weight = 5.0f;
    }

    public override void OnCaught()
    {
        base.OnCaught();
        Debug.Log("WHooooa! You just got a STAR!!!");
    }
}
