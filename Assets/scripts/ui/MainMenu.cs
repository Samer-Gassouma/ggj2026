using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Call this from the Start button's OnClick event in the UI
    public void StartGame()
    {
        SceneManager.LoadScene(1); // Loads the scene at build index 1
    }

    // Optional: add Quit button support
    public void QuitGame()
    {
        Application.Quit();
    }
}
