using CalamityMod;
using CalamityOverhaul.Content.Items.Melee.Extras;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.RebelBladeProj
{
    internal class RebelBladeBack : BaseHeldProj
    {
        public override string Texture => CWRConstant.Item_Melee + "RebelBlade";
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 45;
            Projectile.timeLeft = 200;
            Projectile.knockBack = 2;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (Owner.ActiveItem().type != ModContent.ItemType<RebelBlade>()
                || Owner.ownedProjectileCounts[ModContent.ProjectileType<RebelBladeFlyAttcke>()] > 0
                || DownLeft || DownRight
                ) {
                Projectile.Kill();
            }
            Projectile.timeLeft = 2;
            Projectile.Center = Owner.GetPlayerStabilityCenter();
            float rot = 120;
            Projectile.rotation = Owner.direction > 0 ? MathHelper.ToRadians(rot) : MathHelper.ToRadians(180 - rot);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = Projectile.T2DValue();
            Vector2 drawPos = Projectile.Center - Main.screenPosition + Owner.CWR().SpecialDrawPositionOffset;
            Main.EntitySpriteDraw(value, drawPos, null, lightColor, Projectile.rotation + MathHelper.PiOver4, value.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs
            , List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            behindNPCsAndTiles.Add(index);
        }
    }
}
