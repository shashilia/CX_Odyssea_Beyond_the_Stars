using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeManager : MonoBehaviour
{
    void Start()
    {

    }

    void Update()
    {
        if(Input.GetKeyUp(KeyCode.Return))
        {
            SceneManager.LoadScene("Tutorial");
        }
    }
}
