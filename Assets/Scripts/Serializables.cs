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
        public Tiles tiles = new();
    }

    // Collection of tiles in a level
    [Serializable]
    public class Tiles
    {
        public List<SerializableTile> solidTiles = new();
        public List<SerializableTile> objectTiles = new();
        public List<SerializableTile> overlapTiles = new();
        public List<SerializableTile> hazardTiles = new();
        public List<SerializableTile> effectTiles = new();
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
            directions = tileDirections;
            position = tilePosition;
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

        // A level
        [Serializable]
        public class Level
        {
            public string levelID = null;
            public bool completed = false;
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
            public float time = -1f;
            public int moves = -1;

            // Constructor
            public LevelChanges(bool completed, float time, int moves)
            {
                this.completed = completed;
                this.time = time;
                this.moves = moves;
            }
        }
    }


    // User settings
    [Serializable]
    public class Preferences
    {
        public Resolution resolution;
        public FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;
        public float masterVolume = 1f;
        public float SFXVolume = 1f;
        public bool repeatInput = true;
    }
}