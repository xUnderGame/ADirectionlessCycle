using UnityEngine.SceneManagement;
using UnityEngine;
using System.Linq;
using System.IO;
using System;
using Steamworks;
using static Serializables;

public class GameManager : MonoBehaviour
{
    [HideInInspector] public static GameManager Instance;
    
    // Game data // 
    [HideInInspector] public bool isEditing;
    [HideInInspector] public bool buildDebugMode;
    [HideInInspector] public bool chessbattleadvanced;
    [HideInInspector] public string currentEditorLevelID;
    [HideInInspector] public string currentEditorLevelName;
    public static Savedata save;
    public static string customLevelPath;

    internal Color remixColor;
    internal Color outboundColor;
    internal Color completedColor;
    internal readonly string[] noGameplayScenes = { "Main Menu", "Custom Levels", "Settings", "Credits", "Hub" };
    private readonly string[] badScenes = { "Main Menu", "Level Editor", "Custom Levels", "Settings", "Credits", "Hub" };
    private string dataPath;

    void Awake()
    {
        // Singleton (GameManager has persistence)
        if (!Instance) { Instance = this; }
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);

        // Game data
        dataPath = $"{Application.persistentDataPath}/userdata.save";

        // Create a savefile if none exist
        customLevelPath = $"{Application.persistentDataPath}/Custom Levels";
        CreateSave();

        // Create custom levels directory
        if (!Directory.Exists(customLevelPath)) Directory.CreateDirectory(customLevelPath);

        // Load the savefile
        LoadDataJSON();

        // Default variables
        currentEditorLevelID = null;
        currentEditorLevelName = null;
        chessbattleadvanced = false;
        buildDebugMode = false;
        isEditing = false;

        // Colors!!
        ColorUtility.TryParseHtmlString("#E5615F", out remixColor);
        ColorUtility.TryParseHtmlString("#A22BE3", out outboundColor);
        ColorUtility.TryParseHtmlString("#4CF832", out completedColor);

        // Set master and SFX values
        if (AudioManager.Instance) {
            AudioManager.Instance.SetMasterVolume(save.preferences.masterVolume);
            // AudioManager.Instance.SetSFXVolume(save.preferences.SFXVolume); // not needed, we already use the variable!
        }

        // Steam integration initial stuff
        if (!SteamManager.Initialized) return;
        SetPresence("steam_display", "#Menuing");
    }

    // Save game on leaving
    void OnDisable() { SaveDataJSON(save); }

    // Returns if the current scene shouldn't be taken into account
    public bool IsBadScene()
    {
        return badScenes.Contains(SceneManager.GetActiveScene().name);
    }

    // Editor check
    public bool IsEditor()
    {
        return SceneManager.GetActiveScene().name == "Level Editor";
    }

    // Debug check
    public bool IsDebug()
    {
        return buildDebugMode || Application.isEditor;
    }

    // Stuff with savedata //
    
    // Creates a savefile
    public void CreateSave(bool load = false)
    {
        if (!File.Exists(dataPath)) SaveDataJSON(new Savedata());
        if (load) LoadDataJSON();
    }

    // Deletes a savefile
    public void DeleteSave()
    {
        if (File.Exists(dataPath)) File.Delete(dataPath);
    }

    // Save user data
    public void SaveDataJSON(Savedata savedata = null)
    {
        savedata ??= save;
        File.WriteAllText(dataPath, JsonUtility.ToJson(savedata));
    }

    // Load user data
    public void LoadDataJSON() { save = JsonUtility.FromJson<Savedata>(File.ReadAllText(dataPath)); }

    // Mark level
    public void UpdateSavedLevel(string levelID, GameData.LevelChanges changes, bool compareBest = false)
    {
        // Get the level
        GameData.Level level = save.game.levels.Find(l => l.levelID == levelID);
        if (level == null)
        {
            level = new(levelID);
            save.game.levels.Add(level);
        }

        // Update the level
        if (!level.completed) level.completed = changes.completed;
        if (!level.outboundCompletion) level.outboundCompletion = changes.outbound;
        if (changes.time != -1) level.stats.bestTime = (compareBest && (changes.time < level.stats.bestTime || level.stats.bestTime == 0f)) ? changes.time : level.stats.bestTime;
        if (changes.moves != -1) level.stats.totalMoves = (compareBest && (changes.moves < level.stats.totalMoves || level.stats.totalMoves == 0)) ? changes.moves : level.stats.totalMoves;
    }

    // Saves a level preview when exporting from the editor
    internal byte[] SaveLevelPreview()
    {
        if (SceneManager.GetActiveScene().name != "Level Editor") return null;
        RenderTexture texture = Resources.Load<RenderTexture>("Misc/Screenshot");

        // Convert to Texture2D
        Texture2D tex = new(1920, 1080, TextureFormat.RGB24, false, true);
        RenderTexture.active = texture;
        tex.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        tex.Apply();

        // Encode and return
        return tex.EncodeToPNG(); 
    }
    
    // Converts a base 64 string to a texture, usually used with level preview textures
    public Texture2D Base64ToTexture(string image)
    {
        Texture2D texture = new(1920, 1080);
        byte[] bytes = Convert.FromBase64String(image);
        ImageConversion.LoadImage(texture, bytes);
        return texture;
    }

    // Steam Integration //

    // Grants an achievement, then stores it to server
    // Example: EditAchivement("ACH_TEST");
    internal void EditAchivement(string id, bool grant = true)
    {
        if (!SteamManager.Initialized || id == null) return;

        if (grant) SteamUserStats.SetAchievement(id);
        else SteamUserStats.ClearAchievement(id);

        SteamUserStats.StoreStats();
    }

    // Grants an achievement, then stores it to server
    // Example key: SetPresence("steam_display", "#Menuing");
    // Example variable: SetPresence("currentlevel", "Level Name!");
    // https://steamcommunity.com/dev/testrichpresence
    internal void SetPresence(string key, string display)
    {
        if (!SteamManager.Initialized) return;

        SteamFriends.SetRichPresence(key, display);
    }
}
