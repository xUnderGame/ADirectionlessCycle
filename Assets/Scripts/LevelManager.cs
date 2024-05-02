using System.Collections.Generic;
using UnityEngine.Tilemaps;
using System.Linq;
using UnityEngine;
using System.IO;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static Serializables;
using static GameTile;
using Unity.VisualScripting;

public class LevelManager : MonoBehaviour
{
    // Basic //
    internal readonly ObjectTypes[] typesSolidsList = { ObjectTypes.Wall };
    internal readonly ObjectTypes[] typesObjectList = { ObjectTypes.Box, ObjectTypes.Circle, ObjectTypes.Hexagon };
    internal readonly ObjectTypes[] typesAreas = { ObjectTypes.Area, ObjectTypes.InverseArea };
    internal readonly ObjectTypes[] typesHazardsList = { ObjectTypes.Hazard };
    internal readonly ObjectTypes[] typesEffectsList = { ObjectTypes.Invert };
    [HideInInspector] public static LevelManager Instance;
    [HideInInspector] public GameTile wallTile;
    [HideInInspector] public GameTile boxTile;
    [HideInInspector] public GameTile hexagonTile;
    [HideInInspector] public GameTile circleTile;
    [HideInInspector] public GameTile areaTile;
    [HideInInspector] public GameTile inverseAreaTile;
    [HideInInspector] public GameTile hazardTile;
    [HideInInspector] public GameTile invertTile;
    public int boundsX = 19;
    public int boundsY = -11;

    // Grids and tilemaps //
    private Grid levelGrid;
    [HideInInspector] public Tilemap tilemapCollideable;
    [HideInInspector] public Tilemap tilemapObjects;
    [HideInInspector] public Tilemap tilemapWinAreas;
    [HideInInspector] public Tilemap tilemapHazards;
    [HideInInspector] public Tilemap tilemapEffects;

    // Level data //
    private readonly List<GameTile> levelSolids = new();
    private readonly List<GameTile> levelObjects = new();
    private readonly List<GameTile> levelWinAreas = new();
    private readonly List<GameTile> levelHazards = new();
    private readonly List<GameTile> levelEffects = new();
    private readonly List<GameTile> movementBlacklist = new();
    private readonly List<GameTile> toDestroy = new();

    // Player //
    private Vector3Int latestMovement = Vector3Int.zero;
    private bool canMove = true;
    private bool isPaused = false;
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
        hexagonTile = Resources.Load<HexagonTile>("Tiles/Objects/Hexagon");
        circleTile = Resources.Load<CircleTile>("Tiles/Objects/Circle");
        areaTile = Resources.Load<AreaTile>("Tiles/Areas/Area");
        inverseAreaTile = Resources.Load<InverseAreaTile>("Tiles/Areas/Inverse Area");
        hazardTile = Resources.Load<HazardTile>("Tiles/Hazards/Hazard");
        invertTile = Resources.Load<InvertTile>("Tiles/Effects/Invert");
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

    // Saves a level to the game's persistent path
    public void SaveLevel(string levelName)
    {
        if (IsStringEmptyOfNull(levelName)) return;
        levelName = levelName.Trim();

        // Create the level object
        SerializableLevel level = new() { levelName = levelName };

        // Populate the level
        levelSolids.ForEach(tile => level.tiles.solidTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelObjects.ForEach(tile => level.tiles.objectTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelWinAreas.ForEach(tile => level.tiles.overlapTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelHazards.ForEach(tile => level.tiles.hazardTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelEffects.ForEach(tile => level.tiles.effectTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));

        // Save the level locally
        string levelPath = $"{Application.persistentDataPath}/{levelName}.bytes";
        File.WriteAllText(levelPath, JsonUtility.ToJson(level, true));
        UI.Instance.global.SendMessage($"Saved level \"{levelName}\" to \"{levelPath}\".");
    }

    // Load and build a level
    public void LoadLevel(string levelName)
    {
        if (IsStringEmptyOfNull(levelName)) return;
        levelName = levelName.Trim();

        // Clears the current level
        ClearLevel();

        // Loads the new level
        SerializableLevel level = GetLevel(levelName);
        if (level == null) return;

        // Loads the level
        level.tiles.solidTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapCollideable, levelSolids));
        level.tiles.objectTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapObjects, levelObjects));
        level.tiles.overlapTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapWinAreas, levelWinAreas));
        level.tiles.hazardTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapHazards, levelHazards));
        level.tiles.effectTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position), tilemapEffects, levelEffects));
        UI.Instance.global.SendMessage($"Loaded level \"{levelName}\"!");
    }

    // Clears the current level
    public void ClearLevel()
    {
        levelGrid.GetComponentsInChildren<Tilemap>().ToList().ForEach(layer => layer.ClearAllTiles());
        latestMovement = Vector3Int.zero;
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

        // Disallows MOVING a tile that has already moved
        if (movementBlacklist.Contains(tile) && !beingPushed) return false;

        // Scene bounds (x,y always at 0)
        if (!CheckSceneInbounds(newPosition)) return false;

        // Checks if directions are null
        if ((direction.y > 0 && !tile.directions.up ||
            direction.y < 0 && !tile.directions.down ||
            direction.x < 0 && !tile.directions.left ||
            direction.x > 0 && !tile.directions.right) && !beingPushed) return false;

        // Moves the tile if all collision checks pass
        newPosition = tile.CollisionHandler(newPosition, direction, tilemapObjects, tilemapCollideable);
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
            "Wall" => Instantiate(wallTile),
            "Area" => Instantiate(areaTile),
            "InverseArea" => Instantiate(inverseAreaTile),
            "Hazard" => Instantiate(hazardTile),
            "Invert" => Instantiate(invertTile),
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
    private void ApplyGravity(Vector3Int movement)
    {
        // Clears blacklist
        movementBlacklist.Clear();
        toDestroy.Clear();

        // Sort by move "priority"
        List<GameTile> moveList = levelObjects.OrderBy(tile => tile.GetTileType() != ObjectTypes.Hexagon).ToList();

        // Moves every object
        foreach (var tile in moveList)
        {
            if (!movementBlacklist.Contains(tile))
            {
                // Tries to move a tile
                TryMove(tile.position, tile.position + movement, movement, true);
            }
        }

        // Destroys all marked object tiles.
        foreach (GameTile tile in toDestroy) { RemoveTile(tile); }
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
                    (objectOverlap == null && type == ObjectTypes.InverseArea);
                }
            ) && levelWinAreas.Any(area => area.GetTileType() == ObjectTypes.Area); // At least one exists

        if (winCondition) {
            UI.Instance.win.Toggle(true);
            hasWon = true;
        }
    }

    // Returns if currently in editor
    public bool IsAllowedToPlay() { return !(GameManager.Instance.IsBadScene() || isPaused || hasWon); }

    // Is string empty or null
    public bool IsStringEmptyOfNull(string str) { return str == null || str == string.Empty; }

    // Gets a level and returns it as a serialized object
    private SerializableLevel GetLevel(string levelName)
    {
        TextAsset level = Resources.Load<TextAsset>($"Levels/{levelName}");
        if (!level) { UI.Instance.global.SendMessage($"Invalid level! ({levelName})"); return null; }

        string levelJson = level.text;
        return JsonUtility.FromJson<SerializableLevel>(levelJson);
    }

    // Pauses or resumes the game.
    public void PauseResumeGame(bool status)
    {
        UI.Instance.pause.Toggle(status);
        isPaused = status;
    }

    // Refreshes an object tile
    public void RefreshObjectTile(GameTile tile)
    {
        tilemapObjects.SetTile(tile.position, null);
        tilemapObjects.SetTile(tile.position, tile);
    }

    // Gets called whenever you change scenes
    private void RefreshGameOnSceneLoad(Scene scene, LoadSceneMode sceneMode)
    {
        if (scene.name != "Game") return;
        isPaused = false;
        hasWon = false;
    }

    // Player Input //

    // Movement
    private void OnMove(InputValue ctx)
    {
        if (!IsAllowedToPlay()) return;

        // Bad input prevention logic
        Vector3Int movement = Vector3Int.RoundToInt(ctx.Get<Vector2>());
        if (movement == Vector3Int.zero) { canMove = true; return; };
        if (!canMove) return;

        // Checks if you can actually move in that direction
        latestMovement = movement;
        canMove = false;

        // Moves tiles and checks for a win
        ApplyGravity(movement);
        CheckCompletion();
    }

    // Wait
    private void OnWait()
    {
        if (latestMovement == Vector3Int.zero || !IsAllowedToPlay()) return;

        // Moves tiles using the user's latest movement
        ApplyGravity(latestMovement);
        CheckCompletion();
    }
}
