using CalamityOverhaul.Common;
using CalamityOverhaul.Common.Interfaces;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class MightyStar : CustomProjectiles
    {
        public override string Texture => CWRConstant.Masking + "CosmicFlame";

        public override void SetStaticDefaults() {

        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 220;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override int Status { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public override int Behavior { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }
        public override int ThisTimeValue { get => (int)Projectile.ai[2]; set => Projectile.ai[2] = value; }

        public override void OnKill(int timeLeft) {

        }

        public override void OnSpawn(IEntitySource source) {

        }

        public override bool ShouldUpdatePosition() {
            return true;
        }

        public override void AI() {

        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return null;
        }

        public override bool PreDraw(ref Color lightColor) {
            return true;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            overPlayers.Add(index);
        }
    }
}
