using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Common.Interfaces
{
    public abstract class CustomProjectiles : ModProjectile
    {
        public abstract override void SetStaticDefaults();
        public abstract override void SetDefaults();
        public abstract override string Texture { get; }
        public abstract int Status { get; set; }
        public abstract int Behavior { get; set; }
        public abstract int ThisTimeValue { get; set; }
        public abstract override void OnSpawn(IEntitySource source);
        public abstract override void OnKill(int timeLeft);
        public abstract override bool ShouldUpdatePosition();
        public abstract override void AI();
        public abstract override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox);
        public abstract override bool PreDraw(ref Color lightColor);
        public abstract override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI);
    }
}
