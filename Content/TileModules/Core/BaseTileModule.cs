using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.TileModules.Core
{
    public abstract class BaseTileModule
    {
        public virtual int TargetTileID => -1;

        public Tile Tile;

        public Item TrackItem;

        public bool Active;

        public int WhoAmI;

        public int ModuleID => TileModuleLoader.TileModuleID[GetType()];

        public Point16 Position;

        public Vector2 PosInWorld => new Vector2(Position.X, Position.Y) * 16;

        public virtual BaseTileModule Clone() => (BaseTileModule)Activator.CreateInstance(GetType());

        public virtual void LoadInWorld() { }

        public virtual void UnLoadInWorld() { }

        public void Kill() {
            OnKill();
            Active = false;
        }

        public virtual void OnKill() { }

        public virtual void KillMultiTileSet(int frameX, int frameY) { }

        public virtual void Load() { }

        public virtual void UnLoad() { }

        public void SetPropertyLoader() { }

        public virtual void SetStaticProperty() { }

        public virtual void SetProperty() { }

        public virtual void Update() { }

        public virtual bool IsDaed() {
            if (Tile == null) {
                return true;
            }

            if (!Tile.HasTile) {
                return true;
            }

            if (Tile.TileType != TargetTileID) {
                return true;
            }
            return false;
        }

        public virtual void Draw(SpriteBatch spriteBatch) { }
    }
}
