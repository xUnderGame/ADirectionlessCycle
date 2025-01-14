using System;
using System.Collections.Generic;
using UnityEngine;
using static GameTile;

public class Serializables
{
    // Level serializing //

    // Level as a serializable class
    [Serializable]
    public class SerializableLevel
    {
        public string levelName;
        public string nextLevel = null;
        public string remixLevel = null;
        public string previewImage = null;
        public bool hideUI = false;
        public bool freeroam = false;
        public int difficulty = 1;
        public Tiles tiles = new();
    }

    // Collection of tiles in a level
    [Serializable]
    public class Tiles
    {
        public List<SerializableCustomInfo> customTileInfo = new();
        public List<SerializableTile> solidTiles = new();
        public List<SerializableTile> objectTiles = new();
        public List<SerializableTile> overlapTiles = new();
        public List<SerializableTile> hazardTiles = new();
        public List<SerializableTile> effectTiles = new();
        public List<SerializableTile> customTiles = new();

        // List constructors (im sorryyyyy)
        public Tiles() { }
        public Tiles(List<GameTile> solids, List<GameTile> objects, List<GameTile> overlaps, List<GameTile> hazards, List<GameTile> effects, List<GameTile> customs, List<SerializableCustomInfo> info)
        {
            solids.ForEach(tile => solidTiles.Add(new SerializableTile(tile.GetTileType(), tile.directions, tile.position)));
            objects.ForEach(tile => objectTiles.Add(new SerializableTile(tile.GetTileType(), tile.directions, tile.position)));
            overlaps.ForEach(tile => overlapTiles.Add(new SerializableTile(tile.GetTileType(), tile.directions, tile.position)));
            hazards.ForEach(tile => hazardTiles.Add(new SerializableTile(tile.GetTileType(), tile.directions, tile.position)));
            effects.ForEach(tile => effectTiles.Add(new SerializableTile(tile.GetTileType(), tile.directions, tile.position)));
            customs.ForEach(tile => customTiles.Add(new SerializableTile(tile.GetTileType(), tile.directions, tile.position)));
            info.ForEach(custom => customTileInfo.Add(new SerializableCustomInfo(custom.position, custom.text)));
        }
    }

    // Similar to a GameTile, but able to serialize to json
    [Serializable]
    public class SerializableTile
    {
        public string type;
        public Directions directions;
        public Vector3Int position;

        // Tile constructor
        public SerializableTile(ObjectTypes tileType, Directions tileDirections, Vector3Int tilePosition)
        {
            type = tileType.ToString();
            directions = new(tileDirections.up, tileDirections.down, tileDirections.left, tileDirections.right, tileDirections.pushable);
            position = new(tilePosition.x, tilePosition.y, tilePosition.z);
        }
    }

    // ugh.
    [Serializable]
    public class SerializableCustomInfo
    {
        public Vector3Int position;
        public string text;

        // Constructor
        public SerializableCustomInfo(Vector3Int tilePosition, string text)
        {
            position = new(tilePosition.x, tilePosition.y, tilePosition.z);
            this.text = text;
        }
    }

    // Savedata stuff //

    // Main game's save
    [Serializable]
    public class Savedata
    {
        public GameData game;
        public Preferences preferences;
    }

    // Game data
    [Serializable]
    public class GameData
    {
        public List<Level> levels = new();
        public List<string> exhaustedDialog;
        public Mechanics mechanics = new();
        public bool unlockedWorldTwo = false;
        public bool unlockedWorldThree = false;
        public bool unlockedWorldSuper = false;
        public bool doPrologue = true;
        public bool seenHintPopup = false;

        // A level
        [Serializable]
        public class Level
        {
            public string levelID = null;
            public bool completed = false;
            public bool outboundCompletion = false;
            public LevelStats stats;

            // Constructor
            public Level(string id)
            {
                levelID = id;
                stats = new();
            }
        }

        // A level's stats
        [Serializable]
        public class LevelStats
        {
            public float bestTime = 0f;
            public int totalMoves = 0;
        }

        // Not serializable!!
        public class LevelChanges
        {
            public bool completed;
            public bool outbound;
            public float time = -1f;
            public int moves = -1;

            // Constructor
            public LevelChanges(bool completed, bool outbound, float time, int moves)
            {
                this.completed = completed;
                this.outbound = outbound;
                this.time = time;
                this.moves = moves;
            }
        }

        // Seen mechanics
        [Serializable]
        public class Mechanics
        {
            public bool hasSeenRemix = false;
        }
    }


    // User settings
    [Serializable]
    public class Preferences
    {
        public List<ObjectTypes> editorTiles = new() { ObjectTypes.Wall, ObjectTypes.Box, ObjectTypes.Hazard, ObjectTypes.Area };
        public float masterVolume = 1f;
        public float SFXVolume = 0.8f;
        public bool repeatInput = true;
        public bool forceConfirmRestart = true;
        public string outlineType = "NONE"; // Dotted, Full, NONE
    }
}