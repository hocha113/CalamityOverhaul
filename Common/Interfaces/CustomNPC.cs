using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common.Interfaces
{
    public abstract class CustomNPC : ModNPC
    {
        public abstract override string Texture { get; }
        public abstract override void SetStaticDefaults();
        public abstract override void SetDefaults();
        public abstract int Status { get; set; }
        public abstract int Behavior { get; set; }
        public abstract int ThisTimeValue { get; set; }
        public abstract override void OnKill();
        public abstract override void OnSpawn(IEntitySource source);
        public abstract override void AI();
        public abstract override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor);
        public abstract override void DrawBehind(int index);
    }
}
