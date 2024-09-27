using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Game Tiles/Areas/Outbound Area Tile")]
public class OutboundAreaTile : AreaTile
{
    // Returns the tile type
    public override ObjectTypes GetTileType() { return ObjectTypes.OutboundArea; }

    // Checks colisions between collideables and objects
    public override Vector3Int CollisionHandler(Vector3Int checkPosition, Vector3Int direction, Tilemap tilemapObjects, Tilemap tilemapCollideable, bool beingPushed = false)
    {
        return Vector3Int.back;
    }
    
    // Prepares editor variables.
    public override void PrepareTile()
    {
        directions.editorDirections = false;
        directions.editorPushable = false;
        directions.editorMinimumDirections = 0;
    }
}