using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Game Tiles/Effects/Orb Tile")]
public class OrbTile : EffectTile
{
    // Returns the tile type
    public override ObjectTypes GetTileType() { return ObjectTypes.Orb; }

    // Checks colisions between collideables and objects
    public override Vector3Int CollisionHandler(Vector3Int checkPosition, Vector3Int direction, Tilemap tilemapObjects, Tilemap tilemapCollideable, bool beingPushed = false)
    {
        return Vector3Int.back;
    }

    // The tile's effect
    public override void Effect(GameTile tile)
    {
        if (tile.directions.GetActiveDirectionCount() <= 0 || LevelManager.Instance.currentLevelID == LevelManager.Instance.levelEditorName) return;

        // if (!GameManager.save.game.collectedOrbs.Contains(LevelManager.Instance.currentLevelID)) GameManager.save.game.collectedOrbs.Add(LevelManager.Instance.currentLevelID);
        LevelManager.Instance.RemoveTile(LevelManager.Instance.tilemapEffects.GetTile<OrbTile>(position));
    }
    
    // Prepares editor variables.
    public override void PrepareTile()
    {
        directions.editorDirections = false;
        directions.editorPushable = false;
        directions.editorMinimumDirections = 0;
    }
}