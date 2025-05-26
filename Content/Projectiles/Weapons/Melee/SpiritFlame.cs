﻿using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class SpiritFlame : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile + "GostFire";
        private const int maxFrame = 3;
        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void OnSpawn(IEntitySource source) {
            Projectile.frameCounter = Main.rand.Next(maxFrame);
            Projectile.scale = Main.rand.NextFloat(0.2f, 0.8f);
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frameCounter, 5, maxFrame - 1);
            Vector2 posChange = Main.player[Projectile.owner].CWR().PlayerPositionChange;
            if (Projectile.ai[0] == 0) {
                Player owner = CWRUtils.GetPlayerInstance(Projectile.owner);
                Projectile.velocity = owner != null ? owner.velocity * 0.9f + new Vector2(0, -2) : new Vector2(0, -2);
            }
            if (Projectile.ai[0] == 1) {
                if (Projectile.ai[1] == 0) {
                    Projectile.timeLeft = 120;
                    Projectile.scale = 0.6f;
                    Projectile.ai[1] = 1;
                }
                Projectile.scale *= 1.01f;
                Projectile.velocity = Projectile.velocity.RotatedBy(0.03f);
                Projectile.velocity *= 0.99f;
                Projectile.position += posChange;//需要靠这行代码实现与玩家的相对静止
            }
            if (Projectile.ai[0] == 2) {
                if (Projectile.ai[1] == 0) {
                    Projectile.timeLeft = 150;
                    Projectile.scale = 0.9f;
                    Projectile.ai[1] = 1;
                }
                Projectile.scale *= 1.015f;
                Projectile.velocity = Projectile.velocity.RotatedBy(0.04f);
                Projectile.velocity *= 0.995f;
                Projectile.position += posChange;
            }
            if (Projectile.ai[0] == 3) {
                if (Projectile.ai[1] == 0) {
                    Projectile.timeLeft = Main.rand.Next(32, 64);
                    Projectile.scale = Main.rand.NextFloat(0.5f, 0.7f);
                    Projectile.ai[1] = 1;
                }
                Projectile.scale *= 1.003f;
                Projectile.velocity.Y = -3;
                Projectile.velocity.X += MathF.Sin(Main.GameUpdateCount / 60 * MathHelper.Pi) * 3;
                Projectile.position += posChange;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float alp = Projectile.timeLeft / 30f;
            Rectangle rectangle = texture.GetRectangle(Projectile.frameCounter, maxFrame);
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, rectangle, Color.White * alp
                , Projectile.rotation, rectangle.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
