using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;
using static Serializables;
using static GameTile;
using static TransitionManager.Transitions;

public class LevelManager : MonoBehaviour
{
    // Tile References & Others //
    internal readonly ObjectTypes[] typesSolidsList = { ObjectTypes.Wall, ObjectTypes.AntiWall };
    internal readonly ObjectTypes[] typesObjectsList = { ObjectTypes.Box, ObjectTypes.Circle, ObjectTypes.Hexagon, ObjectTypes.Mimic };
    internal readonly ObjectTypes[] typesAreas = { ObjectTypes.Area, ObjectTypes.InverseArea, ObjectTypes.OutboundArea };
    internal readonly ObjectTypes[] typesHazardsList = { ObjectTypes.Hazard, ObjectTypes.Void };
    internal readonly ObjectTypes[] typesEffectsList = { ObjectTypes.Invert, ObjectTypes.Arrow, ObjectTypes.NegativeArrow, ObjectTypes.Orb, ObjectTypes.Fragment };
    internal readonly ObjectTypes[] typesCustomsList = { ObjectTypes.Level, ObjectTypes.Hologram, ObjectTypes.NPC, ObjectTypes.Fake };
    internal readonly ObjectTypes[] customSpriters = { ObjectTypes.NPC, ObjectTypes.Hologram, ObjectTypes.Fake };
    internal readonly ObjectTypes[] customMovers = { ObjectTypes.Hexagon, ObjectTypes.Mimic };
    [HideInInspector] public static LevelManager Instance;
    [HideInInspector] public GameTile wallTile;
    [HideInInspector] public GameTile antiwallTile;
    [HideInInspector] public GameTile boxTile;
    [HideInInspector] public GameTile circleTile;
    [HideInInspector] public GameTile hexagonTile;
    [HideInInspector] public GameTile mimicTile;
    [HideInInspector] public GameTile areaTile;
    [HideInInspector] public GameTile inverseAreaTile;
    [HideInInspector] public GameTile outboundAreaTile;
    [HideInInspector] public GameTile levelTile;
    [HideInInspector] public GameTile hologramTile;
    [HideInInspector] public GameTile npcTile;
    [HideInInspector] public GameTile hazardTile;
    [HideInInspector] public GameTile voidTile;
    [HideInInspector] public GameTile invertTile;
    [HideInInspector] public GameTile arrowTile;
    [HideInInspector] public GameTile negativeArrowTile;
    [HideInInspector] public GameTile orbTile;
    [HideInInspector] public GameTile fragmentTile;

    // Grids and tilemaps //
    private Grid levelGrid;
    private Grid extraGrid;
    [HideInInspector] public Tilemap tilemapCollideable;
    [HideInInspector] public Tilemap tilemapObjects;
    [HideInInspector] public Tilemap tilemapWinAreas;
    [HideInInspector] public Tilemap tilemapHazards;
    [HideInInspector] public Tilemap tilemapEffects;
    [HideInInspector] public Tilemap tilemapCustoms;
    [HideInInspector] public Tilemap tilemapLetterbox;
    [HideInInspector] public Tilemap extrasOutlines;
    private TilemapRenderer areaRenderer;
    private TilemapRenderer objectRenderer;
    private TilemapRenderer effectRenderer;
    internal Vector3 originalPosition;
    internal int worldOffsetX = 0;
    internal int worldOffsetY = 0;
    // internal GameObject directionPrefab;
    internal Sprite dottedOverlapBox;
    internal Sprite dottedOverlapCircle;
    internal Sprite dottedOverlapHex;
    internal Sprite fullOverlapBox;
    internal Sprite fullOverlapCircle;
    internal Sprite fullOverlapHex;
    private Color slightlyTransparent;

    // Level data //
    [HideInInspector] public SerializableLevel currentLevel = null;
    [HideInInspector] public string currentLevelID = null;
    [HideInInspector] public string levelEditorName = null;
    internal List<ObjectTypes> formQueue = new(capacity: 101); // 100 + 1 form capacity (for now)
    internal List<SerializableCustomInfo> customTileInfo = new();
    private readonly List<GameTile> levelSolids = new();
    private readonly List<GameTile> levelObjects = new();
    private readonly List<GameTile> levelWinAreas = new();
    private readonly List<GameTile> levelHazards = new();
    private readonly List<GameTile> levelEffects = new();
    private readonly List<GameTile> levelCustoms = new();
    private readonly List<GameTile> movementBlacklist = new();
    private readonly List<HexagonTile> lateMove = new();
    private readonly List<GameTile> toDestroy = new();
    private readonly List<Tiles> undoSequence = new(capacity: 100); // 100 undo capacity (for now)
    private readonly List<(int, int)> roomSequence = new(capacity: 100); // 100 undo capacity (for now)
    private readonly int boundsX = 13;
    private readonly int boundsY = -7;
    private int defaultAreaLayer;
    private int defaultObjectsLayer;
    private int defaultEffectsLayer;
    private Checker background;

    // Player //
    private Coroutine timerCoroutine = null;
    private bool doPushSFX = false;
    private float levelTimer = 0f;
    private int levelMoves = 0;
    private bool noMove = false;
    public bool isPaused = false;
    public bool hasWon;

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
        antiwallTile = Resources.Load<AntiWallTile>("Tiles/Solids/Anti Wall");
        boxTile = Resources.Load<BoxTile>("Tiles/Objects/Box");
        circleTile = Resources.Load<CircleTile>("Tiles/Objects/Circle");
        hexagonTile = null;
        mimicTile = Resources.Load<MimicTile>("Tiles/Objects/Mimic");
        areaTile = Resources.Load<WinAreaTile>("Tiles/Areas/Area");
        inverseAreaTile = Resources.Load<InverseWinAreaTile>("Tiles/Areas/Inverse Area");
        outboundAreaTile = Resources.Load<OutboundAreaTile>("Tiles/Areas/Outbound Area");
        hazardTile = Resources.Load<HazardTile>("Tiles/Hazards/Hazard");
        voidTile = Resources.Load<VoidTile>("Tiles/Hazards/Void");
        invertTile = Resources.Load<InvertTile>("Tiles/Effects/Invert");
        arrowTile = Resources.Load<ArrowTile>("Tiles/Effects/Arrow");
        negativeArrowTile = Resources.Load<NegativeArrowTile>("Tiles/Effects/Negative Arrow");
        orbTile = null;
        fragmentTile = null;
        levelTile = null;
        hologramTile = null;
        npcTile = Resources.Load<NPCTile>("Tiles/Customs/NPC");

        // Defaults
        // directionPrefab = Resources.Load<GameObject>("Prefabs/Tile Properties");
        dottedOverlapBox = Resources.Load<Sprite>("Sprites/Overlaps/DottedBoxOverlap");
        dottedOverlapCircle = Resources.Load<Sprite>("Sprites/Overlaps/DottedCircleOverlap");
        dottedOverlapHex = Resources.Load<Sprite>("Sprites/Overlaps/DottedHexOverlap");
        fullOverlapBox = Resources.Load<Sprite>("Sprites/Overlaps/FullBoxOverlap");
        fullOverlapCircle = Resources.Load<Sprite>("Sprites/Overlaps/FullCircleOverlap");
        fullOverlapHex = Resources.Load<Sprite>("Sprites/Overlaps/FullHexOverlap");
        slightlyTransparent = new(1, 1, 1, 0.85f);
        defaultAreaLayer = areaRenderer.sortingOrder;
        defaultObjectsLayer = objectRenderer.sortingOrder;
        defaultEffectsLayer = effectRenderer.sortingOrder;
        hasWon = false;

        // Editor (with file persistence per session)
        levelEditorName = "EditorSession";
        currentLevelID = null;
        currentLevel = null;
    }

    // Gets the scene references for later use (should be called every time on scene change (actually no i lied))
    private void TryGetSceneReferences()
    {
        // Level grids and tilemaps
        Transform gridObject = transform.Find("Level Grid");
        levelGrid = gridObject != null ? gridObject.GetComponent<Grid>() : null;
        tilemapCollideable = gridObject != null ? gridObject.Find("Collideable").GetComponent<Tilemap>() : null;
        tilemapObjects = gridObject != null ? gridObject.Find("Objects").GetComponent<Tilemap>() : null;
        tilemapWinAreas = gridObject != null ? gridObject.Find("Overlaps").GetComponent<Tilemap>() : null;
        tilemapHazards = gridObject != null ? gridObject.Find("Hazards").GetComponent<Tilemap>() : null;
        tilemapEffects = gridObject != null ? gridObject.Find("Effects").GetComponent<Tilemap>() : null;
        tilemapCustoms = gridObject != null ? gridObject.Find("Customs").GetComponent<Tilemap>() : null;
        tilemapLetterbox = gridObject != null ? gridObject.Find("Letterbox").GetComponent<Tilemap>() : null;

        // Extra grids and tilemaps
        Transform extraObject = transform.Find("Extras");
        extraGrid = extraObject != null ? extraObject.GetComponent<Grid>() : null;
        extrasOutlines = extraObject != null ? extraObject.Find("Outlines").GetComponent<Tilemap>() : null;

        areaRenderer = tilemapWinAreas.GetComponent<TilemapRenderer>();
        objectRenderer = tilemapObjects.GetComponent<TilemapRenderer>();
        effectRenderer = tilemapEffects.GetComponent<TilemapRenderer>();
        originalPosition = new Vector3(tilemapObjects.transform.position.x, tilemapObjects.transform.position.y, tilemapObjects.transform.position.z);
    }

    // Adds a tile to the private objects list
    public void AddToObjectList(GameTile tile)
    {
        if (!typesObjectsList.Contains(tile.GetTileType())) return;
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

    // Adds a tile to the private customs list
    public void AddToCustomsList(GameTile tile)
    {
        if (!typesCustomsList.Contains(tile.GetTileType())) return;
        else if (!levelCustoms.Contains(tile)) levelCustoms.Add(tile);
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
    public string SaveLevel(string levelName, string levelID = default, bool silent = true, byte[] previewImage = null)
    {
        if (IsStringEmptyOrNull(levelName)) return null;
        levelName = levelName.Trim();

        // Level id stuff
        if (levelID == default) levelID = $"{levelName}-{Random.Range(1, 1000)}";

        // Create the level object
        SerializableLevel level = new() { levelName = levelName };

        // Populate the level
        levelSolids.ForEach(tile => level.tiles.solidTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelObjects.ForEach(tile => level.tiles.objectTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelWinAreas.ForEach(tile => level.tiles.overlapTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelHazards.ForEach(tile => level.tiles.hazardTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelEffects.ForEach(tile => level.tiles.effectTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));
        levelCustoms.ForEach(tile => level.tiles.customTiles.Add(new(tile.GetTileType(), tile.directions, tile.position)));

        // Set some flags
        if (currentLevel != null) {
            if (previewImage != null) level.previewImage = Convert.ToBase64String(previewImage);
            else level.previewImage = null;
            level.nextLevel = currentLevel.nextLevel;
            level.remixLevel = currentLevel.remixLevel;
            level.difficulty = currentLevel.difficulty;
            level.freeroam = currentLevel.freeroam;
            level.hideUI = currentLevel.hideUI;
        }

        // Add custom tile information
        foreach (var tile in customTileInfo)
        {
            if (!tilemapCustoms.GetTile<CustomTile>(tile.position) || level.tiles.customTileInfo.Any(pos => { return pos.position == tile.position; })) continue;
            level.tiles.customTileInfo.Add(tile);
        }

        // Save the level locally
        string levelPath = $"{Application.persistentDataPath}/Custom Levels/{levelID}.level";
        File.WriteAllText(levelPath, JsonUtility.ToJson(level, false));
        // byte[] levelAsBytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(level, false));
        // File.WriteAllText(levelPath, Convert.ToBase64String(levelAsBytes));

        if (!silent) UI.Instance.global.SendMessage($"Saved level \"{levelName}\" with ID \"{levelID}\".", 4.0f);
        return levelID;
    }

    // Load and build a level
    public bool LoadLevel(string levelID, bool external = false, bool silent = true)
    {
        if (IsStringEmptyOrNull(levelID)) return false;
        levelID = levelID.Trim();

        // Gets the new level
        SerializableLevel checkLevel = GetLevel(levelID, external, silent);
        if (checkLevel == null) return false;
        else currentLevel = checkLevel;

        // Clears the current level
        MoveTilemaps(originalPosition, true);
        ClearLevel();

        // Loads the level
        currentLevelID = levelID;
        BuildLevel(currentLevel.tiles);

        // Start the level timer (coro) and reset moves
        levelTimer = 0;
        levelMoves = 0;
        timerCoroutine = StartCoroutine(LevelTimer());
        UI.Instance.ingame.SetLevelMoves(levelMoves);

        // Swapping mechanic startup
        var playables = InputManager.Instance.GetPlayableObjects();
        if (playables.Count == 1) { formQueue.Add(playables[0].GetTileType()); }
        else formQueue.Add(ObjectTypes.Mimic); // should never happen anyways.

        // Reset hint popup (if applicable)
        if (!GameManager.save.game.seenHintPopup) InputManager.Instance.restartCount = 0;

        // Rich presence
        GameManager.Instance.SetPresence("playinglevel", currentLevel.levelName);
        GameManager.Instance.SetPresence("steam_display", "#Playing");

        // Hide UI?
        if (!silent) UI.Instance.global.SendMessage($"Loaded level \"{currentLevel.levelName}\"");
        if (currentLevel.hideUI) {
            tilemapLetterbox.gameObject.SetActive(true);
            UI.Instance.ingame.Toggle(false);
            return true;
        }

        // UI Stuff
        UI.Instance.pause.title.text = currentLevel.levelName;
        GameData.Level levelAsSave = GameManager.save.game.levels.Find(l => l.levelID == levelID);
        UI.Instance.ingame.SetAreaCount(0, levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.Area; }), 1);
        if (levelAsSave != null) {
            UI.Instance.pause.SetBestTime(levelAsSave.stats.bestTime);
            UI.Instance.pause.SetBestMoves(levelAsSave.stats.totalMoves);
        } else {
            UI.Instance.pause.SetBestTime(0f);
            UI.Instance.pause.SetBestMoves(0);
        }

        return true;
    }

    // Load and build a level
    public void ReloadLevel(bool silent = true, bool isLevelEditor = false)
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
        BuildLevel(currentLevel.tiles, isLevelEditor);

        // UI!
        if (currentLevel.hideUI) return;
        UI.Instance.ingame.SetAreaCount(0, levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.Area; }), 1);
    }

    // Builds the level
    private void BuildLevel(Tiles level, bool editor = false)
    {
        if (level == null) return;

        // Disallow fragment and orb spawning
        // if (GameManager.save.game.collectedOrbs.Contains(currentLevelID)) level.effectTiles = level.effectTiles.FindAll(tile => { return tile.type != "Orb"; });
        // if (GameManager.save.game.collectedFragments.Contains(currentLevelID)) level.effectTiles = level.effectTiles.FindAll(tile => { return tile.type != "Fragment"; });

        // Build the level
        level.solidTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position)));
        level.objectTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position)));
        level.overlapTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position)));
        level.hazardTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position)));
        level.effectTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position)));
        level.customTiles.ForEach(tile => PlaceTile(CreateTile(tile.type, tile.directions, tile.position)));
        
        // Apply all custom tile text
        level.customTileInfo.ForEach(tile => customTileInfo.Add(new(tile.position, tile.text)));
        foreach (var tile in level.customTileInfo)
        {
            CustomTile realTile = tilemapCustoms.GetTile<CustomTile>(tile.position);
            if (realTile) { realTile.customText = tile.text; SetCustomSprite(realTile, false, editor); RefreshCustomTile(realTile); }
            else customTileInfo.Remove(tile);
        }
    }

    // Clears the current level
    public void ClearLevel(bool soft = false)
    {
        levelGrid.GetComponentsInChildren<Tilemap>().ToList().ForEach(layer => { if(layer.name != "Letterbox" && layer.name != "Scanlines") layer.ClearAllTiles(); });
        if (!soft) {
            if (timerCoroutine != null) { StopCoroutine(timerCoroutine); }
            InputManager.Instance.latestMovement = Vector3Int.back;
            ClearUndoFrames();
        }

        // directionPrefab.transform.Find("AnimationSprite").gameObject.SetActive(false);
        InputManager.Instance.latestTile = ObjectTypes.Hexagon;
        movementBlacklist.Clear();
        customTileInfo.Clear();
        levelSolids.Clear();
        levelObjects.Clear();
        levelWinAreas.Clear();
        levelHazards.Clear();
        levelEffects.Clear();
        levelCustoms.Clear();
    }

    // Moves a tile (needs optimizing)
    public bool TryMove(Vector3Int startingPosition, Vector3Int newPosition, Vector3Int direction, bool removeFromQueue = false, bool beingPushed = false)
    {
        if (noMove) return false;

        // Check if the tile exists
        GameTile tile = tilemapObjects.GetTile<GameTile>(startingPosition);
        if (!tile) return false;

        // Disable the tile's sprite before it moves
        tile.directions.animationSprite.gameObject.SetActive(false);

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
            direction.x > 0 && !tile.directions.right)
            && !beingPushed) {
            if (removeFromQueue) movementBlacklist.Add(tile);
            return false;
        }

        // Moves the tile if all collision checks pass
        newPosition = tile.CollisionHandler(newPosition, direction, tilemapObjects, tilemapCollideable, beingPushed);
        if (newPosition == Vector3.back || newPosition == startingPosition || (movementBlacklist.Contains(tile) && !beingPushed) || noMove) return false; // also re-checking for blacklist
        MoveTile(startingPosition, newPosition, tile, false);

        // Updates new current position of the tile
        if (beingPushed) doPushSFX = true;
        tile.position = newPosition;

        // Change "scene" if on world map?
        if (currentLevel.freeroam)
        {

            // X POSITION: -14 / +14.
            // Y POSITION: -8 / +8.
            // TODO: noMove = true; (Freeze object tiles from the new room, except ones coming from old room)
            if (tile.position.x < 0 + worldOffsetX) { MoveTilemaps(new Vector3(14, 0)); worldOffsetX -= 14; }
            else if (tile.position.x > boundsX + worldOffsetX) { MoveTilemaps(new Vector3(-14, 0)); worldOffsetX += 14; }
            else if (tile.position.y > 0 + worldOffsetY) { MoveTilemaps(new Vector3(0, -8)); worldOffsetY += 8; }
            else if (tile.position.y < boundsY + worldOffsetY) { MoveTilemaps(new Vector3(0, 8)); worldOffsetY -= 8; }
        }

        // Removes from movement queue
        if (removeFromQueue) { if (!movementBlacklist.Contains(tile)) movementBlacklist.Add(tile); }

        // Marks all the objects that should be deleted
        GameTile hazard = tilemapHazards.GetTile<GameTile>(tile.position);
        if (hazard)
        {
            AddToDestroyQueue(tile);
            if (hazard.GetTileType() == ObjectTypes.Void) RemoveTile(hazard);
        }

        // Tile effect?
        EffectTile effect = tilemapEffects.GetTile<EffectTile>(tile.position);
        CustomTile custom = tilemapCustoms.GetTile<CustomTile>(tile.position);
        if (effect) effect.Effect(tile);
        if (custom) custom.Effect(tile);

        return true;
    }

    // Moves a tile, no other cases
    public void MoveTile(Vector3Int startingPos, Vector3Int newPos, GameTile tile, bool doAnimation = true)
    {
        if (doAnimation) // Will always be false!! caller is set to false rn.
        {
            // Vector3 lastPosAsWorld = tilemapObjects.CellToWorld(newPos);

            // Moves tile to new position
            tilemapObjects.SetTile(newPos, tile);
            tilemapObjects.SetTile(startingPos, null);

            // Plays tile's animation, towards target position after moving a tile (ONLY OBJECT TILES)
            switch (tile.GetTileType())
            {
                case ObjectTypes.Mimic:
                case ObjectTypes.Box:
                    break;

                case ObjectTypes.Circle:
                    break;

                case ObjectTypes.Hexagon:
                    break;
            }
            return;
        }

        // Only sets the new tile and removes the old one
        tilemapObjects.SetTile(newPos, tile);
        tilemapObjects.SetTile(startingPos, null);
    }

    // Places a tile using its own position
    public void PlaceTile(GameTile tile)
    {
        switch (tile.GetTileType())
        {
            case ObjectTypes t when typesSolidsList.Contains(t):
                tilemapCollideable.SetTile(tile.position, tile);
                levelSolids.Add(tile);
                break;

            case ObjectTypes t when typesAreas.Contains(t):
                tilemapWinAreas.SetTile(tile.position, tile);
                levelWinAreas.Add(tile);
                break;

            case ObjectTypes t when typesHazardsList.Contains(t):
                tilemapHazards.SetTile(tile.position, tile);
                levelHazards.Add(tile);
                break;

            case ObjectTypes t when typesEffectsList.Contains(t):
                tilemapEffects.SetTile(tile.position, tile);
                levelEffects.Add(tile);
                break;

            case ObjectTypes t when typesCustomsList.Contains(t):
                tilemapCustoms.SetTile(tile.position, tile);
                levelCustoms.Add(tile);
                break;

            default:
                tilemapObjects.SetTile(tile.position, tile);
                levelObjects.Add(tile);
                break;
        }
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

            case ObjectTypes t when typesCustomsList.Contains(t):
                tilemapCustoms.SetTile(tile.position, null);
                levelCustoms.Remove(tile);
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
            "AntiWall" => Instantiate(antiwallTile),
            "Area" => Instantiate(areaTile),
            "InverseArea" => Instantiate(inverseAreaTile),
            "OutboundArea" => Instantiate(outboundAreaTile),
            "Hazard" => Instantiate(hazardTile),
            "Void" => Instantiate(voidTile),
            "Invert" => Instantiate(invertTile),
            "Arrow" => Instantiate(arrowTile),
            "NegativeArrow" => Instantiate(negativeArrowTile),
            "Orb" => Instantiate(orbTile),
            "Fragment" => Instantiate(fragmentTile),
            "Level" => Instantiate(levelTile),
            "Hologram" => Instantiate(hologramTile),
            "Fake" => Instantiate(hologramTile), // Hologram tile demo support
            "NPC" => Instantiate(npcTile),
            _ => Instantiate(boxTile) // Default, covers box types
        };

        // Apply tile defaults
        tile.directions = defaultDirections;
        tile.position = defaultPosition;
        tile.PrepareTile();
        return tile;
    }

    // Returns if a position is inside or outside the level bounds
    public bool CheckSceneInbounds(Vector3Int position, bool hexSpecial = false, bool hexPushed = false)
    {
        if (GameManager.Instance.IsEditor()) return !(position.x < 0 + worldOffsetX || position.x > boundsX + worldOffsetX || position.y > 0 + worldOffsetY || position.y < boundsY + worldOffsetY);
        if (currentLevel.freeroam && hexSpecial && !hexPushed) return !(position.x < 0 + worldOffsetX - 2 || position.x > boundsX + worldOffsetX + 2 || position.y > 0 + worldOffsetY + 2 || position.y < boundsY + worldOffsetY - 2);
        if (currentLevel.freeroam && currentLevel.hideUI) return true;
        return !(position.x < 0 + worldOffsetX || position.x > boundsX + worldOffsetX || position.y > 0 + worldOffsetY || position.y < boundsY + worldOffsetY);
    }

    // Applies gravity using a direction
    internal void ApplyGravity(Vector3Int movement)
    {
        // Clears blacklist
        movementBlacklist.Clear();
        toDestroy.Clear();
        lateMove.Clear();
        doPushSFX = false;
        noMove = false;

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

        // Tile pushed SFX
        if (doPushSFX) AudioManager.Instance.PlaySFX(AudioManager.tilePush, 0.45f);

        // Destroys all marked object tiles.
        foreach (GameTile tile in toDestroy) { RemoveTile(tile); }
        if (toDestroy.Count > 0) AudioManager.Instance.PlaySFX(AudioManager.tileDeath, 0.45f);

        // Win check, add one move to the player
        if (validation.Contains(true)) levelMoves++;
        else RemoveUndoFrame();
        if (UI.Instance) UI.Instance.ingame.SetLevelMoves(levelMoves);
        CheckCompletion();
    }

    // Checks if you've won
    private void CheckCompletion()
    {
        // Level win condition:
        // All area tiles have some object overlapping them and at least 1 exists,
        // no other areas are being overlapped.
        bool winCondition = 
            levelWinAreas.All(overlap =>
                {
                    if (!typesAreas.Contains(overlap.GetTileType())) return true;

                    GameTile objectOverlap = tilemapObjects.GetTile<GameTile>(overlap.position);
                    ObjectTypes type = overlap.GetTileType();

                    return (objectOverlap != null && type == ObjectTypes.Area) ||
                    (objectOverlap == null && type == ObjectTypes.InverseArea) ||
                    (objectOverlap == null && type == ObjectTypes.OutboundArea);
                }
            ) && levelWinAreas.Any(area => area.GetTileType() == ObjectTypes.Area); // At least one exists

        // Inverted win condition:
        // All area tiles have some object overlapping them and at least 1 exists,
        // all object tiles must be overlapping said inverted areas,
        // level MUST have a remix level defined (not null)
        bool remixCondition =
            levelWinAreas.All(overlap => // All inverse areas are overlapped
                {
                    if (!typesAreas.Contains(overlap.GetTileType())) return true;

                    GameTile objectOverlap = tilemapObjects.GetTile<GameTile>(overlap.position);
                    ObjectTypes type = overlap.GetTileType();

                    return (objectOverlap != null && type == ObjectTypes.InverseArea) ||
                    (objectOverlap == null && type == ObjectTypes.Area) ||
                    (objectOverlap == null && type == ObjectTypes.OutboundArea);
                }
            ) && levelWinAreas.Any(area => area.GetTileType() == ObjectTypes.InverseArea) // At least one exists
            && levelObjects.All(tile => // All level objects are overlapping inverse areas
                {
                    return tilemapWinAreas.GetTile<InverseWinAreaTile>(tile.position) != null;
                }
            ) && !IsStringEmptyOrNull(currentLevel.remixLevel);

        // Outbound win condition:
        // All outbound area tiles have some object overlapping them and at least 1 exists,
        // All area tiles have some object overlapping them.
        bool outboundCondition = 
            levelWinAreas.All(overlap =>
                {
                    if (!typesAreas.Contains(overlap.GetTileType())) return true;

                    GameTile objectOverlap = tilemapObjects.GetTile<GameTile>(overlap.position);
                    ObjectTypes type = overlap.GetTileType();

                    return (objectOverlap != null && type == ObjectTypes.OutboundArea) ||
                    (objectOverlap != null && type == ObjectTypes.Area) ||
                    type == ObjectTypes.InverseArea;
                }
            ) && levelWinAreas.Any(area => area.GetTileType() == ObjectTypes.OutboundArea); // At least one exists

        // UI area count
        SetUIAreaCount();

        // Save current game status when beating a level (doesnt store new stats)
        if (winCondition || remixCondition || outboundCondition) { GameManager.Instance.SaveDataJSON(GameManager.save); }

        // Outbound win
        if (outboundCondition && !DialogManager.Instance.inDialog)
        {
            // Level + savedata
            GameData.LevelChanges changes = new(false, true, -1, -1);
            GameManager.Instance.UpdateSavedLevel(currentLevelID, changes, true);

            // UI
            UI.Instance.GoNextLevel();
            hasWon = true;
            return;
        }

        // Load remix level!
        if (remixCondition && !DialogManager.Instance.inDialog)
        {
            // Check if the player is playtesting a level
            if (GameManager.Instance.isEditing)
            {
                UI.Instance.GoLevelEditor();
                return;   
            }

            // First remix level?
            if (!GameManager.save.game.mechanics.hasSeenRemix)
            {
                GameManager.save.game.mechanics.hasSeenRemix = true;
            }

            // Level + savedata
            GameData.LevelChanges changes = new(false, false, -1, -1);
            GameManager.Instance.UpdateSavedLevel(currentLevelID, changes, true);
            GameManager.Instance.UpdateSavedLevel(currentLevel.remixLevel, changes, false); // create a remix entry (therefore its discovered)

            TransitionManager.Instance.TransitionIn(Unknown, ActionRemixCondition, currentLevel.remixLevel);
            GameManager.Instance.isEditing = false;
            return;
        }

        // If won, do the thing
        if (winCondition && !DialogManager.Instance.inDialog)
        {
            // Level savedata
            GameData.LevelChanges changes = new(true, false, (float)Math.Round(levelTimer, 2), levelMoves);
            GameManager.Instance.UpdateSavedLevel(currentLevelID, changes, true);

            // UI
            UI.Instance.GoNextLevel();
            hasWon = true;
        }
    }

    // Returns if currently in editor
    public bool IsAllowedToPlay() { return !(GameManager.Instance.IsBadScene() || isPaused || hasWon || DialogManager.Instance.inDialog || TransitionManager.Instance.inTransition || UI.Instance.restart.self.activeSelf || UI.Instance.popup.self.activeSelf); }

    // Is string empty or null
    public bool IsStringEmptyOrNull(string str) { return str == null || str == string.Empty || str == ""; }

    // Gets a level and returns it as a serialized object
    public SerializableLevel GetLevel(string levelID, bool external, bool silent = false)
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
        if (level == null) { if (!silent) UI.Instance.global.SendMessage($"Invalid level! ({levelID})", 2.5f); return null; }

        // Gets the level as readable data
        // byte[] levelAsBytes = Convert.FromBase64String(level);
        // level = System.Text.Encoding.UTF8.GetString(levelAsBytes);
        return JsonUtility.FromJson<SerializableLevel>(level);
    }

    // Pauses or resumes the game.
    public void PauseResumeGame(bool status)
    {
        if (status) {
            UI.Instance.selectors.ChangeSelected(UI.Instance.pause.resumeButton, true);
            UI.Instance.pause.ToggleEditButton(GameManager.Instance.isEditing || GameManager.Instance.IsDebug());
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

    // Refreshes a custom tile
    public void RefreshCustomTile(CustomTile tile)
    {
        tilemapCustoms.SetTile(tile.position, null);
        tilemapCustoms.SetTile(tile.position, tile);
    }

    // Refreshes the game and closes all UI's
    public void RefreshGameVars()
    {
        isPaused = false;
        hasWon = false;
        levelTimer = 0f;
        levelMoves = 0;
        worldOffsetX = 0;
        worldOffsetY = 0;

        DialogManager.Instance.loadedDial = null;
        tilemapLetterbox.gameObject.SetActive(true);
        extrasOutlines.gameObject.SetActive(true);
        UI.Instance.ingame.SetLevelTimer(levelTimer);
        UI.Instance.ingame.SetLevelMoves(levelMoves);
        MoveTilemaps(originalPosition, true);
    }

    // Sets all UI's to its defaults
    public void RefreshGameUI()
    {
        if (!currentLevel.hideUI) UI.Instance.ingame.Toggle(true);
        UI.Instance.pause.Toggle(false);
        // UI.Instance.win.Toggle(false);
        UI.Instance.editor.Toggle(false);
    }

    // Gets called whenever you change scenes
    private void RefreshGameOnSceneLoad(Scene scene, LoadSceneMode sceneMode)
    {
        if (UI.Instance.selectors)
        {
            UI.Instance.selectors.right.SetParent(UI.Instance.selectors.gameObject.transform);
            UI.Instance.selectors.left.SetParent(UI.Instance.selectors.gameObject.transform);
            // UI.Instance.selectors.instant = true;
        }

        if (GameManager.Instance.noGameplayScenes.Contains(scene.name))
        {
            tilemapLetterbox.gameObject.SetActive(false);
            extrasOutlines.gameObject.SetActive(false);
            UI.Instance.ingame.Toggle(false);
            return;
        }

        // Get game background reference
        if (SceneManager.GetActiveScene().name == "Game" && !background) background = GameObject.Find("Static Background").GetComponent<Checker>();
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

    // (World Scene) Moves all* tilemaps towards a direction
    internal void MoveTilemaps(Vector3 direction, bool force = false)
    {
        foreach (Transform tilemap in levelGrid.transform)
        {
            if (tilemap.name == "Letterbox") continue; // * except letterbox

            // Updates position based on direction
            if (force) tilemap.position = direction;
            else tilemap.position += direction;
        }
    }

    // Pings all areas
    internal void ShowOverlaps(bool status)
    {
        if (status)
        {
            areaRenderer.sortingOrder = 5;
            effectRenderer.sortingOrder = 10;
            objectRenderer.sortingOrder = 3;
            tilemapWinAreas.color = slightlyTransparent;
            tilemapEffects.color = slightlyTransparent;
        } else
        {
            areaRenderer.sortingOrder = defaultAreaLayer;
            effectRenderer.sortingOrder = defaultEffectsLayer;
            objectRenderer.sortingOrder = defaultObjectsLayer;
            tilemapWinAreas.color = Color.white;
            tilemapEffects.color = Color.white;
        }
    }

    // Adds an undo frame to the sequence (lord help me)
    internal void AddUndoFrame()
    {
        if (undoSequence.Count >= undoSequence.Capacity) RemoveUndoFrame(true);
        formQueue.Add(InputManager.Instance.latestTile);
        roomSequence.Add((worldOffsetX, worldOffsetY));
        undoSequence.Add(new Tiles(levelSolids, levelObjects, levelWinAreas, levelHazards, levelEffects, levelCustoms, customTileInfo));
    }

    // Removes the latest undo frame from the sequence
    internal void RemoveUndoFrame(bool earliest = false)
    {
        if (undoSequence.Count <= 0) return;
        if (earliest) { undoSequence.RemoveAt(0); roomSequence.RemoveAt(0); formQueue.RemoveAt(0); }
        else { undoSequence.RemoveAt(undoSequence.Count - 1); roomSequence.RemoveAt(roomSequence.Count - 1); formQueue.RemoveAt(formQueue.Count - 1); }
    }

    // Clears all frames
    internal void ClearUndoFrames() { undoSequence.Clear(); roomSequence.Clear(); formQueue.Clear(); }

    // Undo check
    internal bool IsUndoQueueValid() { return undoSequence.Count > 0; }

    // Undoes a move
    internal void Undo()
    {
        // Reload level snapshot (not very efficient)
        ClearLevel(true);
        BuildLevel(undoSequence[^1]);
        InputManager.Instance.latestTile = formQueue[^1];
        worldOffsetX = roomSequence[^1].Item1;
        worldOffsetY = roomSequence[^1].Item2;
        MoveTilemaps(originalPosition - new Vector3(worldOffsetX, worldOffsetY), true);
        SetUIAreaCount();

        // Undo SFX
        AudioManager.Instance.PlaySFX(AudioManager.undo, 1f, true);
        
        // Remove a move
        levelMoves--;
        if (UI.Instance) UI.Instance.ingame.SetLevelMoves(levelMoves);
    }

    // No more moving!
    internal void StopMovements()
    {
        // Gotta do it this way instead of clearing the movement list
        // to avoid changing the stack size!
        noMove = true;
    }

    // Updates a custom tile's sprite
    internal void SetCustomSprite(CustomTile tile, bool refresh = true, bool isLevelEditor = false)
    {
        if (!customSpriters.Contains(tile.GetTileType())) return;

        if (tile.customText != string.Empty)
        {
            string stringCheck;

            // Checks if there is a sprite
            switch (tile.GetTileType())
            {
                case ObjectTypes.NPC:
                    if (tile.customText.Split(";").Length < 2) return;
                    stringCheck = (string)tile.customText.Split(";").GetValue(1);
                    break;
                
                case ObjectTypes.Hologram:
                case ObjectTypes.Fake:
                    stringCheck = tile.customText;
                    break;
                default:
                    return;
            }

            // Checks if the sprite exists
            Sprite spriteCheck = Resources.Load<Sprite>($"Sprites/Tiles/{stringCheck}");
            if (spriteCheck == null && stringCheck != "Invisible") return;
            else if (isLevelEditor && stringCheck == "Invisible") return;

            // Sets the sprite and (optionally) refreshes the tile
            tile.tileSprite = spriteCheck;
            if (refresh) RefreshCustomTile(tile);
        }
    }

    // Returns the list of object tiles
    internal List<GameTile> GetObjectTiles()
    {
        return levelObjects;
    }

    // Returns the list of custom tiles
    internal List<GameTile> GetCustomTiles()
    {
        return levelCustoms;
    }

    // Finds all overlapped areas and sets the amount on the UI
    private void SetUIAreaCount()
    {
        GameTile currTile;

        // Applies tile overlap colors and counts the total amount
        int normalOverlaps = levelWinAreas.Count(area => {
            currTile = tilemapObjects.GetTile<GameTile>(area.position);
            if (area.GetTileType() == ObjectTypes.Area && currTile != null)
            {
                currTile.directions.SetAnimationSprite(currTile.GetOverlapSprite(), GameManager.Instance.completedColor);
                RefreshObjectTile(currTile);
                currTile.directions.animationSprite.gameObject.SetActive(false);
                return true;
            }
            return false;
        });

        int remixOverlaps = levelWinAreas.Count(area => {
            currTile = tilemapObjects.GetTile<GameTile>(area.position);
            if (area.GetTileType() == ObjectTypes.InverseArea && currTile != null)
            {
                currTile.directions.SetAnimationSprite(currTile.GetOverlapSprite(), GameManager.Instance.remixColor);
                RefreshObjectTile(currTile);
                currTile.directions.animationSprite.gameObject.SetActive(false);
                return true;
            }
            return false;
        });

        int outboundOverlaps = levelWinAreas.Count(area => {
            currTile = tilemapObjects.GetTile<GameTile>(area.position);
            if (area.GetTileType() == ObjectTypes.OutboundArea && currTile != null)
            {
                currTile.directions.SetAnimationSprite(currTile.GetOverlapSprite(), GameManager.Instance.outboundColor);
                RefreshObjectTile(currTile);
                currTile.directions.animationSprite.gameObject.SetActive(false);
                return true;
            }
            return false;
        });

        // Outbound overlaps
        if (outboundOverlaps > 0)
        {
            UI.Instance.ingame.SetAreaCount(
                outboundOverlaps + normalOverlaps,
                levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.OutboundArea; }) + levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.Area; }),
                3 // thats a funny 3
            );
            return;
        }

        // Normal overlaps
        if (normalOverlaps > 0 && !GameManager.save.game.mechanics.hasSeenRemix || (normalOverlaps > remixOverlaps && GameManager.save.game.mechanics.hasSeenRemix))
        {
            UI.Instance.ingame.SetAreaCount(
                normalOverlaps,
                levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.Area; }), 1
            );
            return;
        }

        // Remix overlaps
        if (remixOverlaps > 0 && GameManager.save.game.mechanics.hasSeenRemix)
        {
            UI.Instance.ingame.SetAreaCount(
                remixOverlaps,
                levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.InverseArea; }), 2
            );
            return;
        }

        // if nothing applies, use default (normal overlaps, copied code)
        UI.Instance.ingame.SetAreaCount(
            normalOverlaps,
            levelWinAreas.Count(area => { return area.GetTileType() == ObjectTypes.Area; }), 1
        );
    }

    // Actions //
    public void ActionLoadLevel(string name)
    {
        var save = GameManager.save.game.levels.Find(level => level.levelID == name);

        // Loads the level
        try {
            LoadLevel(name, SceneManager.GetActiveScene().name == "Custom Levels");
        } catch(Exception e) {
            Debug.LogException(e, this);
            UI.Instance.global.SendMessage("An error ocurred while loading!", 10f);
            UI.Instance.ChangeScene("Main Menu", false);
            ClearLevel();
            return;
        }

        RefreshGameVars();
        RefreshGameUI();

        // Preload screen
        TransitionManager.Instance.ChangeTransition(Triangle);
        if (!currentLevel.hideUI) UI.Instance.preload.PreparePreloadScreen(save);
        else {
            UI.Instance.ChangeScene("Game", false);
            TransitionManager.Instance.TransitionOut<string>();
        }
    }

    public void ActionRemixCondition(string remixID)
    {
        var save = GameManager.save.game.levels.Find(level => level.levelID == remixID);
        
        // Loads the level (Load internal level first, if it fails, load external)
        if (!LoadLevel(remixID)) LoadLevel(remixID, true);

        // Preload screen
        TransitionManager.Instance.ChangeTransition(Unknown);
        if (!currentLevel.hideUI) UI.Instance.preload.PreparePreloadScreen(save);
        else TransitionManager.Instance.TransitionOut<string>();
    }
}
