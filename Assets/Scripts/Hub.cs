using UnityEngine;
using UnityEngine.EventSystems;

public class Hub : MonoBehaviour
{
    public Checker checker;
    public GameObject worldHolder;
    public GameObject backButton;

    private RectTransform holderRT;
    private readonly int[] positions = { 0, -1920, -3840, -5760 };
    private int worldIndex = 0;

    private void Start()
    {
        EventSystem.current.SetSelectedGameObject(backButton);
        holderRT = worldHolder.GetComponent<RectTransform>();
    }

    // Load level
    public void StaticLoadLevel(string levelName)
    {
        if (!LevelManager.Instance) return;

        LevelManager.Instance.LoadLevel(levelName);
        LevelManager.Instance.RefreshGameVars();
        LevelManager.Instance.RefreshGameUI();
        UI.Instance.ChangeScene("Game");
    }

    // Change world
    public void ChangeWorld(int direction)
    {
        EventSystem.current.SetSelectedGameObject(backButton);
        if (worldIndex + direction >= positions.Length || worldIndex + direction < 0) return;
        if (worldIndex + direction == 3 && GameManager.save.game.collectedOrbs.Count < 1) return;

        worldIndex += direction;
        holderRT.anchoredPosition = new(positions[worldIndex], holderRT.anchoredPosition.y);

        // Update checker direction
        checker.dirX = direction;
    }
}
