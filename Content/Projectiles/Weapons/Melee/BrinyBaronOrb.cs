using CalamityMod.Projectiles.Melee;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class BrinyBaronOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.ignoreWater = true;
            Projectile.alpha = 255;
            Projectile.timeLeft = 600;
            Projectile.extraUpdates = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Gore bubble = Gore.NewGorePerfect(Projectile.GetSource_FromAI(), Projectile.position, Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1f, 1f), 411);
            bubble.timeLeft = 9 + Main.rand.Next(7);
            bubble.scale = Main.rand.NextFloat(0.6f, 1f);
            bubble.type = Main.rand.NextBool(3) ? 412 : 411;
            for (int i = 0; i < 6; i++) {
                Dust water = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Wet, 0f, 0f, 100);
                water.noGravity = true;
                water.velocity = Projectile.velocity * 0.5f;
            }
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 10; i++) {
                Gore bubble = Gore.NewGorePerfect(Projectile.GetSource_FromAI(), Projectile.position, Projectile.velocity.RotatedByRandom(MathHelper.ToRadians(60f)) * 0.3f, 411);
                bubble.timeLeft = 9 + Main.rand.Next(7);
                bubble.scale = Main.rand.NextFloat(0.6f, 1f);
                bubble.type = Main.rand.NextBool(3) ? 412 : 411;
            }

            for (int i = 0; i < 3; i++) {//这他妈在写些什么？！
                int constant = 36 + i * 10;
                for (int j = 0; j < constant; j++) {
                    Vector2 rotate = Vector2.Normalize(Projectile.velocity) * new Vector2(Projectile.width / 2f, Projectile.height) * 0.75f;
                    rotate = rotate.RotatedBy((double)((j - (constant / 2 - 1)) * 6.28318548f / constant), default) + Projectile.Center;
                    Vector2 faceDirection = rotate - Projectile.Center;
                    int waterDust = Dust.NewDust(rotate + faceDirection, 0, 0, DustID.DungeonWater, faceDirection.X * 2f, faceDirection.Y * 2f, 100, default, 1.4f);
                    Main.dust[waterDust].noGravity = true;
                    Main.dust[waterDust].noLight = true;
                    Main.dust[waterDust].velocity = faceDirection * i;
                }
            }

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center + new Vector2(0, 16)
                , Vector2.Zero, ModContent.ProjectileType<BrinyTyphoonBubble>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
        }
    }
}
