﻿using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic
{
    internal class FateCluster : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "GostFire";
        private const int maxFrame = 3;
        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.alpha = 255;
            Projectile.penetrate = 1;
            Projectile.MaxUpdates = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.frameCounter = Main.rand.Next(4);
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frameCounter, 5, maxFrame - 1);
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.ai[0] == 0) {
                NPC target = Projectile.Center.FindClosestNPC(300);

                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1.01f, 0.25f);
                }
            }
            if (Projectile.ai[0] == 1) {
                NPC target = Projectile.Center.FindClosestNPC(600);

                if (target != null) {
                    Projectile.SmoothHomingBehavior(target.Center, 1, 0.05f);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            Projectile.Explode(64);

            for (int i = 0; i <= 360; i += 3) {
                Vector2 vr = new Vector2(3f, 3f).RotatedBy(MathHelper.ToRadians(i));
                int num = Dust.NewDust(Projectile.Center, Projectile.width, Projectile.height
                    , DustID.RedTorch, vr.X, vr.Y, 200, new Color(232, 251, 250, 200), 1.4f);
                Main.dust[num].noGravity = true;
                Main.dust[num].position = Projectile.Center;
                Main.dust[num].velocity = vr;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D mainValue = TextureAssets.Projectile[Type].Value;
            Rectangle rectangle = mainValue.GetRectangle(Projectile.frameCounter, maxFrame);
            Main.EntitySpriteDraw(mainValue, Projectile.Center - Main.screenPosition, rectangle, Color.White * (Projectile.alpha / 255f)
                , Projectile.rotation - MathHelper.PiOver2, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None);
            return false;
        }
    }
}
