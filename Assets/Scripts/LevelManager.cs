using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
using System;
using UnityEngine.Tilemaps;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;
using static Serializables;
using static GameTile;

public class LevelManager : MonoBehaviour
{
    // Basic //
    internal readonly ObjectTypes[] typesSolidsList = { ObjectTypes.Wall };
    internal readonly ObjectTypes[] typesObjectList = { ObjectTypes.Box, ObjectTypes.Circle, ObjectTypes.Hexagon, ObjectTypes.Mimic };
    internal readonly ObjectTypes[] typesAreas = { ObjectTypes.Area, ObjectTypes.InverseArea };
    internal readonly ObjectTypes[] typesHazardsList = { ObjectTypes.Hazard };
    internal readonly ObjectTypes[] typesEffectsList = { ObjectTypes.Invert, ObjectTypes.Arrow, ObjectTypes.NegativeArrow };
    internal readonly ObjectTypes[] customMovers = { ObjectTypes.Hexagon, ObjectTypes.Mimic };
    [HideInInspector] public static LevelManager Instance;
    [HideInInspector] public GameTile wallTile;
    [HideInInspector] public GameTile boxTile;
    [HideInInspector] public GameTile circleTile;
    [HideInInspector] public GameTile hexagonTile;
    [HideInInspector] public GameTile mimicTile;
    [HideInInspector] public GameTile areaTile;
    [HideInInspector] public GameTile inverseAreaTile;
    [HideInInspector] public GameTile hazardTile;
    [HideInInspector] public GameTile invertTile;
    [HideInInspector] public GameTile arrowTile;
    [HideInInspector] public GameTile negativeArrowTile;
    public int boundsX = 13; // Recommended not to change!
    public int boundsY = -7; // Recommended not to change!

    // Grids and tilemaps //
    private Grid levelGrid;
    [HideInInspector] public Tilemap tilemapCollideable;
    [HideInInspector] public Tilemap tilemapObjects;
    [HideInInspector] public Tilemap tilemapWinAreas;
    [HideInInspector] public Tilemap tilemapHazards;
    [HideInInspector] public Tilemap tilemapEffects;
    [HideInInspector] public Tilemap tilemapLetterbox;

    // Level data //
    private readonly List<GameTile> levelSolids = new();
    private readonly List<GameTile> levelObjects = new();
    private readonly List<GameTile> levelWinAreas = new();
    private readonly List<GameTile> levelHazards = new();
    private readonly List<GameTile> levelEffects = new();
    private readonly List<GameTile> movementBlacklist = new();
    private readonly List<HexagonTile> lateMove = new();
    private readonly List<GameTile> toDestroy = new();
    [HideInInspector] public SerializableLevel currentLevel = null;
    [HideInInspector] public string currentLevelID = null;
    [HideInInspector] public string levelEditorName = null;

    // Player //
    private Coroutine timerCoroutine = null;
    private bool isPaused = false;
    private float levelTimer = 0f;
    private int levelMoves = 0;
    public bool hasWon = false;

    void Awake()
    {
        // Singleton (LevelManager has persistence)
        if (!Instance) { Instance = this; }
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);

        // Getting grids and tilemap references
        SceneManager.sceneLoaded += RefreshGameOnSceneLoad;
        TryGetSceneReferences();

        // Getting tile references
        wallTile = Resources.Load<WallTile>("Tiles/Solids/Wall");
        boxTile = Resources.Load<BoxTile>("Tiles/Objects/Box");
        circleTile = Resources.Load<CircleTile>("Tiles/Objects/Circle");
        hexagonTile = Resources.Load<HexagonTile>("Tiles/Objects/Hexagon");
        mimicTile = Resources.Load<MimicTile>("Tiles/Objects/Mimic");
        areaTile = Resources.Load<WinAreaTile>("Tiles/Areas/Area");
        inverseAreaTile = Resources.Load<InverseWinAreaTile>("Tiles/Areas/Inverse Area");
        hazardTile = Resources.Load<HazardTile>("Tiles/Hazards/Hazard");
        invertTile = Resources.Load<InvertTile>("Tiles/Effects/Invert");
        arrowTile = Resources.Load<ArrowTile>("Tiles/Effects/Arrow");
        negativeArrowTile = Resources.Load<NegativeArrowTile>("Tiles/Effects/Negative Arrow");

        // Defaults
        hasWon = false;

        // Editor (with file persistence per session)
        levelEditorName = "EditorSession";
        currentLevelID = null;
        currentLevel = null;
    }

    // Gets the scene references for later use (should be called every time on scene change (actually no i lied))
    private void TryGetSceneReferences()
    {
        Transform gridObject = transform.Find("Level Grid");
        levelGrid = gridObject != null ? gridObject.GetComponent<Grid>() : null;
        tilemapCollideable = gridObject != null ? gridObject.Find("Collideable").GetComponent<Tilemap>() : null;
        tilemapObjects = gridObject != null ? gridObject.Find("Objects").GetComponent<Tilemap>() : null;
        tilemapWinAreas = gridObject != null ? gridObject.Find("Overlaps").GetComponent<Tilemap>() : null;
        tilemapHazards = gridObject != null ? gridObject.Find("Hazards").GetComponent<Tilemap>() : null;
        tilemapEffects = gridObject != null ? gridObject.Find("Effects").GetComponent<Tilemap>() : null;
        tilemapLetterbox = gridObject != null ? gridObject.Find("Letterbox").GetComponent<Tilemap>() : null;
    }

    // Adds a tile to the private objects list
    public void AddToObjectList(GameTile tile)
    {
        if (!typesObjectList.Contains(tile.GetTileType())) return;
        else if (!levelObjects.Contains(tile)) levelObjects.Add(tile);
    }

    // Adds a tile to the private win areas list
    public void AddToWinAreasList(GameTile tile)
    {
        if (!typesAreas.Contains(tile.GetTileType())) return;
        else if (!levelWinAreas.Contains(tile)) levelWinAreas.Add(tile);
    }

    // Adds a tile to the private collideable list
    public void AddToCollideableList(GameTile tile)
    {
        if (!typesSolidsList.Contains(tile.GetTileType())) return;
        else if (!levelSolids.Contains(tile)) levelSolids.Add(tile);
    }

    // Adds a tile to the private hazards list
    public void AddToHazardsList(GameTile tile)
    {
        if (!typesHazardsList.Contains(tile.GetTileType())) return;
        else if (!levelHazards.Contains(tile)) levelHazards.Add(tile);
    }

    // Adds a tile to the private effects list
    public void AddToEffectsList(GameTile tile)
    {
        if (!typesEffectsList.Contains(tile.GetTileType())) return;
        else if (!levelEffects.Contains(tile)) levelEffects.Add(tile);
    }

    // Adds a tile to the private to destroy queue (hazards use this)
    public void AddToDestroyQueue(GameTile tile)
    {
        if (!toDestroy.Contains(tile)) toDestroy.Add(tile);
    }

    // Late moving a tile
    public void AddToLateMove(HexagonTile tile)
    {
        if (!lateMove.Contains(tile)) lateMove.Add(tile);
    }

    // Saves a level to the game's persistent path
    public void SaveLevel(string levelName, string levelID = default, bool silent = true)
    {
        if (IsStringEmptyOrNull(levelName)) return;
        levelName = levelName.Trim();

        // Level id stuff
        if (levelID == default) levelID = $"{Random.Range(1000000, 1000000000)}";

        // Create the level object
        SerializableLevel level = new() { levelName = levelName };

        // Populate the level
        levelSolids.ForEach(tile => level.tiles.solidTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelObjects.ForEach(tile => level.tiles.objectTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelWinAreas.ForEach(tile => level.tiles.overlapTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelHazards.ForEach(tile => level.tiles.hazardTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelEffects.ForEach(tile => level.tiles.effectTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));

        // Save the level locally
        string levelPath = $"{Application.persistentDataPath}/Custom Levels/{levelID}.level";
        File.WriteAllText(levelPath, JsonUtility.ToJson(level, false));
        if (!silent) UI.Instance.global.SendMessage($"Saved level \"{levelName}\" with ID \"{levelID}\" to \"{levelPath}\".", 4.0f);
    }

    // Load and build a level
    public void LoadLevel(string levelID, bool external = false, bool silent = true)
    {
        if (IsStringEmptyOrNull(levelID)) return;
        levelID = levelID.Trim();

        // Clears the current level
        ClearLevel();

        // Gets the new level
        currentLevel = GetLevel(levelID, external);
        if (currentLevel == null) return;

        // Loads the level
        currentLevelID = levelID;
        BuildLevel();

        // Start the level timer (coro)
        timerCoroutine = StartCoroutine(LevelTimer());

        // Yay! UI!
        if (!silent) UI.Instance.global.SendMessage($"Loaded level \"{currentLevel.levelName}\"");

        // UI Stuff
        GameData.Level levelAsSave = GameManager.save.game.levels.Find(l => l.levelID == levelID);
        UI.Instance.ingame.SetAreaCount(0, levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.Area; }));
        UI.Instance.ingame.SetLevelName(currentLevel.levelName);
        if (levelAsSave != null) {
            UI.Instance.pause.SetBestTime(levelAsSave.stats.bestTime);
            UI.Instance.pause.SetBestMoves(levelAsSave.stats.totalMoves);
        } else {
            UI.Instance.pause.SetBestTime(0f);
            UI.Instance.pause.SetBestMoves(0);
        }
    }

    // Load and build a level
    public void ReloadLevel(bool silent = true)
    {
        if (currentLevel == null) return;

        // Clears the current level
        ClearLevel();

        // Restart timer and level stats
        levelTimer = 0f;
        levelMoves = 0;
        UI.Instance.ingame.SetLevelMoves(levelMoves);
        timerCoroutine = StartCoroutine(LevelTimer());

        // Soft "loads" the new level (doesnt use LoadLevel)
        if (!silent) UI.Instance.global.SendMessage("Reloaded level.");
        currentLevel = GetLevel(currentLevelID, true);
        BuildLevel();

        // UI!
        UI.Instance.ingame.SetAreaCount(0, levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.Area; }));
    }

    // Builds the level
    private void BuildLevel()
    {
        if (currentLevel == null) return;
        currentLevel.tiles.solidTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapCollideable, levelSolids));
        currentLevel.tiles.objectTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapObjects, levelObjects));
        currentLevel.tiles.overlapTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapWinAreas, levelWinAreas));
        currentLevel.tiles.hazardTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapHazards, levelHazards));
        currentLevel.tiles.effectTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapEffects, levelEffects));
    }

    // Clears the current level
    public void ClearLevel()
    {
        levelGrid.GetComponentsInChildren<Tilemap>().ToList().ForEach(layer => { if(layer.name != "Letterbox") layer.ClearAllTiles(); });
        if (timerCoroutine != null) { StopCoroutine(timerCoroutine); }
        InputManager.Instance.latestMovement = Vector3Int.back;
        movementBlacklist.Clear();
        levelSolids.Clear();
        levelObjects.Clear();
        levelWinAreas.Clear();
        levelHazards.Clear();
        levelEffects.Clear();
    }

    // Moves a tile (needs optimizing)
    public bool TryMove(Vector3Int startingPosition, Vector3Int newPosition, Vector3Int direction, bool removeFromQueue = false, bool beingPushed = false)
    {
        // Check if the tile exists
        GameTile tile = tilemapObjects.GetTile<GameTile>(startingPosition);
        if (!tile) return false;

        // Is the tile pushable?
        if (!tile.directions.pushable && beingPushed) return false;

        // Disallows MOVING a tile that has already moved
        if (movementBlacklist.Contains(tile) && !beingPushed) return false;

        // Scene bounds (x,y always at 0)
        if (!CheckSceneInbounds(newPosition) && !customMovers.Contains(tile.GetTileType())) return false;

        // Checks if directions are null
        if ((direction.y > 0 && !tile.directions.up ||
            direction.y < 0 && !tile.directions.down ||
            direction.x < 0 && !tile.directions.left ||
            direction.x > 0 && !tile.directions.right) && !beingPushed) return false;

        // Moves the tile if all collision checks pass
        newPosition = tile.CollisionHandler(newPosition, direction, tilemapObjects, tilemapCollideable, beingPushed);
        if (newPosition == Vector3.back || newPosition == startingPosition) return false;
        MoveTile(startingPosition, newPosition, tile);

        // Updates new current position of the tile
        tile.position = newPosition;

        // Removes from movement queue
        if (removeFromQueue) { movementBlacklist.Add(tile); }

        // Marks all the objects that should be deleted
        HazardTile hazard = tilemapHazards.GetTile<HazardTile>(tile.position);
        if (hazard) AddToDestroyQueue(tile);

        // Tile effect?
        EffectTile effect = tilemapEffects.GetTile<EffectTile>(tile.position);
        if (effect) effect.Effect(tile);

        return true;
    }

    // Moves a tile, no other cases
    public void MoveTile(Vector3Int startingPos, Vector3Int newPos, GameTile tile)
    {
        // Sets the new tile and removes the old one
        tilemapObjects.SetTile(newPos, tile);
        tilemapObjects.SetTile(startingPos, null);
    }

    // Places a tile using its own position
    public void PlaceTile(GameTile tile, Tilemap tilemap, List<GameTile> tileList = null)
    {
        tilemap.SetTile(tile.position, tile);
        tileList.Add(tile);
    }

    // Removes a tile from a tilemap
    public void RemoveTile(GameTile tile)
    {
        switch (tile.GetTileType())
        {
            case ObjectTypes t when typesSolidsList.Contains(t):
                tilemapCollideable.SetTile(tile.position, null);
                levelSolids.Remove(tile);
                break;

            case ObjectTypes t when typesAreas.Contains(t):
                tilemapWinAreas.SetTile(tile.position, null);
                levelWinAreas.Remove(tile);
                break;

            case ObjectTypes t when typesHazardsList.Contains(t):
                tilemapHazards.SetTile(tile.position, null);
                levelHazards.Remove(tile);
                break;

            case ObjectTypes t when typesEffectsList.Contains(t):
                tilemapEffects.SetTile(tile.position, null);
                levelEffects.Remove(tile);
                break;

            default:
                tilemapObjects.SetTile(tile.position, null);
                levelObjects.Remove(tile);
                break;
        }
    }

    // Creates a gametile
    public GameTile CreateTile(string type, Directions defaultDirections, Vector3Int defaultPosition)
    {
        // Instantiate correct tile
        GameTile tile = type switch
        {
            "Circle" => Instantiate(circleTile),
            "Hexagon" => Instantiate(hexagonTile),
            "Mimic" => Instantiate(mimicTile),
            "Wall" => Instantiate(wallTile),
            "Area" => Instantiate(areaTile),
            "InverseArea" => Instantiate(inverseAreaTile),
            "Hazard" => Instantiate(hazardTile),
            "Invert" => Instantiate(invertTile),
            "Arrow" => Instantiate(arrowTile),
            "NegativeArrow" => Instantiate(negativeArrowTile),
            _ => Instantiate(boxTile) // Default, covers box types
        };

        // Apply tile defaults
        tile.directions = defaultDirections;
        tile.position = defaultPosition;
        return tile;
    }

    // Returns if a position is inside or outside the level bounds
    public bool CheckSceneInbounds(Vector3Int position)
    {
        return !(position.x < 0 || position.x > boundsX || position.y > 0 || position.y < boundsY);
    }

    // Applies gravity using a direction
    internal void ApplyGravity(Vector3Int movement)
    {
        // Clears blacklist
        movementBlacklist.Clear();
        toDestroy.Clear();
        lateMove.Clear();

        // Sort by move "priority"
        List<GameTile> moveList = levelObjects.OrderBy(tile => tile.GetTileType() != ObjectTypes.Hexagon).ToList();
        List<bool> validation = new();

        // Moves every object
        foreach (var tile in moveList)
        {
            if (!movementBlacklist.Contains(tile))
            {
                // Tries to move a tile
                validation.Add(TryMove(tile.position, tile.position + movement, movement, true));
            }
        }

        // Late moves (stupid...)
        foreach (var tile in lateMove)
        {
            if (!movementBlacklist.Contains(tile))
            {
                // Tries to move a tile
                validation.Add(TryMove(tile.position, tile.position + movement, movement, true));
            }
        }

        // Destroys all marked object tiles.
        foreach (GameTile tile in toDestroy) { RemoveTile(tile); }

        // Win check, add one move to the player
        if (validation.Contains(true)) levelMoves++;
        if (UI.Instance) UI.Instance.ingame.SetLevelMoves(levelMoves);
        CheckCompletion();
    }

    // Checks if you've won
    private void CheckCompletion()
    {
        // Condition:
        // All area tiles have some object overlapping them and at least 1 exists,
        // no inverse areas are being overlapped.
        bool winCondition = 
            levelWinAreas.All(overlap =>
                {
                    if (!typesAreas.Contains(overlap.GetTileType())) return true;

                    GameTile objectOverlap = tilemapObjects.GetTile<GameTile>(overlap.position);
                    ObjectTypes type = overlap.GetTileType();

                    return (objectOverlap != null && type == ObjectTypes.Area) ||
                    (objectOverlap == null && type == ObjectTypes.InverseArea);;
                }
            ) && levelWinAreas.Any(area => area.GetTileType() == ObjectTypes.Area); // At least one exists

        // UI area count
        UI.Instance.ingame.SetAreaCount(
            levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.Area && tilemapObjects.GetTile<GameTile>(area.position) != null; }),
            levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.Area; })
            );

        // If won, do the thing
        if (winCondition)
        {
            // Level savedata
            GameData.LevelChanges changes = new(true, (float)Math.Round(levelTimer, 2), levelMoves);
            GameManager.Instance.UpdateSavedLevel(currentLevelID, changes, true);

            // UI
            EventSystem.current.SetSelectedGameObject(UI.Instance.win.menuButton);
            UI.Instance.win.ToggleEditButton(GameManager.Instance.isEditing || Application.isEditor);
            UI.Instance.win.ToggleNextLevel(!IsStringEmptyOrNull(currentLevel.nextLevel));
            UI.Instance.win.SetTotalTime(changes.time);
            UI.Instance.win.SetTotalMoves(changes.moves);
            UI.Instance.win.Toggle(true);
            hasWon = true;
        }
    }

    // Returns if currently in editor
    public bool IsAllowedToPlay() { return !(GameManager.Instance.IsBadScene() || isPaused || hasWon); }

    // Is string empty or null
    public bool IsStringEmptyOrNull(string str) { return str == null || str == string.Empty; }

    // Gets a level and returns it as a serialized object
    public SerializableLevel GetLevel(string levelID, bool external)
    {
        string externalPath = $"{Application.persistentDataPath}/Custom Levels/{levelID}.level";
        string level = null;

        // Internal/external level import.
        if (external && File.Exists(externalPath)) level = File.ReadAllText(externalPath);
        else {
            TextAsset internalCheck = Resources.Load<TextAsset>($"Levels/{levelID}");
            if (internalCheck) level = internalCheck.text;
        }

        // Invalid level!
        if (level == null) { UI.Instance.global.SendMessage($"Invalid level! ({levelID})", 2.5f); return null; }

        return JsonUtility.FromJson<SerializableLevel>(level);
    }

    // Pauses or resumes the game.
    public void PauseResumeGame(bool status)
    {
        if (status) {
            EventSystem.current.SetSelectedGameObject(UI.Instance.pause.backToMenu);
            UI.Instance.pause.ToggleEditButton(GameManager.Instance.isEditing || Application.isEditor);
        }

        UI.Instance.pause.Toggle(status);
        isPaused = status;
    }

    // Refreshes an object tile
    public void RefreshObjectTile(GameTile tile)
    {
        tilemapObjects.SetTile(tile.position, null);
        tilemapObjects.SetTile(tile.position, tile);
    }

    // Refreshes an effect tile
    public void RefreshEffectTile(GameTile tile)
    {
        tilemapEffects.SetTile(tile.position, null);
        tilemapEffects.SetTile(tile.position, tile);
    }

    // Refreshes an area tile
    public void RefreshAreaTile(GameTile tile)
    {
        tilemapWinAreas.SetTile(tile.position, null);
        tilemapWinAreas.SetTile(tile.position, tile);
    }

    // Refreshes the game and closes all UI's
    public void RefreshGame()
    {
        isPaused = false;
        hasWon = false;
        levelTimer = 0f;
        levelMoves = 0;

        tilemapLetterbox.gameObject.SetActive(true);
        UI.Instance.ingame.SetLevelTimer(levelTimer);
        UI.Instance.ingame.SetLevelMoves(levelMoves);
        UI.Instance.ingame.Toggle(true);
        UI.Instance.pause.Toggle(false);
        UI.Instance.win.Toggle(false);
        UI.Instance.editor.Toggle(false);
    }

    // Gets called whenever you change scenes
    private void RefreshGameOnSceneLoad(Scene scene, LoadSceneMode sceneMode)
    {
        if (scene.name != "Game" && scene.name != "Level Editor")
        {
            tilemapLetterbox.gameObject.SetActive(false);
            UI.Instance.ingame.Toggle(false);
            return;
        }

        RefreshGame();
    }

    // Level timer speedrun any%
    private IEnumerator LevelTimer()
    {
        while (!hasWon)
        {
            levelTimer += Time.deltaTime;
            if (UI.Instance) UI.Instance.ingame.SetLevelTimer(levelTimer);
            yield return null;
        }
    }

    // Pings all areas (FYI, this is horrible.)
    internal void PingAllAreas(bool status)
    {
        foreach (GameTile area in levelWinAreas)
        {
            if (status) tilemapWinAreas.GetTile<AreaTile>(area.position).Ping();
            else {
                GameManager.Instance.drawOver.sprite = null;
                RefreshAreaTile(area);
            }
        }
    }
}
