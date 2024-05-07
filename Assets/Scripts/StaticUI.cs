using UnityEngine;
using UnityEngine.SceneManagement;

public class StaticUI : MonoBehaviour
{
    // Change scenes
    public void StaticChangeScene(string sceneName)
    {
        if (UI.Instance) UI.Instance.ChangeScene(sceneName);
        else SceneManager.LoadScene(sceneName);
    }

    // Exit application
    public void StaticExitApplication() { Application.Quit(); }

    // Master Slider
    public void StaticUpdateMasterSlider(float value)
    {
        if (GameManager.Instance.musicBox) GameManager.Instance.musicBox.volume = value;
    }

    // SFX Slider
    public void StaticUpdateSFXSlider(float value)
    {
        Debug.Log($"SFX: {value}");
    }
}
