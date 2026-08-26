using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles
{
    /// <summary>
    /// 军团战矢：箭令倒数结束后放出的重箭。方向由预告体锁定，出膛后不追踪；
    /// 淡入完成才有杀伤（伤害窗口=可见窗口）
    /// </summary>
    internal class LegionVolleyArrow : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.WoodenArrowHostile;

        /// <summary>出膛淡入帧数，期间无判定</summary>
        private const int FadeInFrames = 8;
        /// <summary>此龄期后吃轻微坠力，给重箭坠感</summary>
        private const int DropStartAge = 20;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Age++;
            if (Age == 1f && !VaultUtils.isServer) {
                //出弦声锚定实体首帧：凡收到本战矢的端都在自己的正确时刻听到
                SoundEngine.PlaySound(SoundID.Item5 with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
            }
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            if (Age > DropStartAge) {
                Projectile.velocity.Y += 0.06f;
                if (Projectile.velocity.Y > 16f) {
                    Projectile.velocity.Y = 16f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行火星（低频）
            if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                Dust ember = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.1f, 140, default, 0.8f);
                ember.noGravity = true;
            }
        }

        /// <summary>淡入完成才有杀伤（公平阀门）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
            for (int i = 0; i < 5; i++) {
                Dust chip = Dust.NewDustPerfect(Projectile.Center,
                    DustID.WoodFurniture, -Projectile.velocity.SafeNormalize(Vector2.UnitY)
                        .RotatedByRandom(0.7f) * Main.rand.NextFloat(1f, 3.5f),
                    60, default, Main.rand.NextFloat(0.8f, 1.2f));
                chip.noGravity = Main.rand.NextBool();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同材质幽灵拖尾（横轴粗细=本体，契约量级）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, ghostPos, null,
                    new Color(255, 160, 80, 90) * (0.4f * t * opacity),
                    Projectile.rotation, orig, 0.9f * t + 0.1f, SpriteEffects.None, 0);
            }

            //本体：真 alpha 原版箭贴图 + 琥珀微辉
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, Projectile.GetAlpha(lightColor),
                Projectile.rotation, orig, 1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(255, 180, 90, 0) * (0.3f * opacity),
                Projectile.rotation, orig, 1.05f, SpriteEffects.None, 0);
            return false;
        }
    }
}
