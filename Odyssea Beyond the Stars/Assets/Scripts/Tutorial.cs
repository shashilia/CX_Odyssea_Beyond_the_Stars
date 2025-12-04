using UnityEngine;
using UnityEngine.SceneManagement;

public class Tutorial : MonoBehaviour
{
    void Start()
    {

    }

    void Update()
    {
        if(Input.GetKeyUp(KeyCode.Return))
        {
            SceneManager.LoadScene("Level 1");
        }

        if(Input.GetKeyUp(KeyCode.S))
        {
            SceneManager.LoadScene("SpeedRun");
        }

        if (Input.GetKeyUp(KeyCode.R))
        {
            GameBehavior.ResetSpeedRunHighScore();
        }
    }
}
