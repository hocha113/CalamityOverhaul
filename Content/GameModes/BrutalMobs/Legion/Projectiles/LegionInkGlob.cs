using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Legion.Projectiles
{
    /// <summary>
    /// 弧线血墨：血乌贼三连的实弹（原版血弹贴图）。出手带上抬、随后吃重力走抛物弧，
    /// 淡入完成才有杀伤（伤害窗=可见窗），触地即溅散
    /// </summary>
    internal class LegionInkGlob : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodShot;

        /// <summary>出手淡入帧数，期间无判定</summary>
        private const int FadeInFrames = 8;
        /// <summary>弧线重力（每帧）</summary>
        private const float InkGravity = 0.12f;
        /// <summary>下落终速上限</summary>
        private const float MaxFallSpeed = 12f;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(220f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            //抛物弧：重力持续、转角随速度走
            Projectile.velocity.Y += InkGravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //坠行血滴（低频）
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Dust drip = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    -Projectile.velocity * 0.1f, 120, default, 0.9f);
                drip.noGravity = true;
            }
        }

        /// <summary>淡入完成才有杀伤（公平阀门）</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit3 with { Volume = 0.35f, Pitch = 0.2f, MaxInstances = 5 },
                Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust splat = Dust.NewDustPerfect(Projectile.Center, DustID.Blood,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.9f)
                        * Main.rand.NextFloat(1f, 3.5f), 60, default, Main.rand.NextFloat(0.9f, 1.4f));
                splat.noGravity = Main.rand.NextBool();
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
                    new Color(255, 70, 80, 95) * (0.4f * t * opacity),
                    Projectile.rotation, orig, 0.9f * t + 0.1f, SpriteEffects.None, 0);
            }

            //本体：真 alpha 原版血弹贴图 + 猩红微辉
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, Projectile.GetAlpha(lightColor),
                Projectile.rotation, orig, 1f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, new Color(255, 60, 70, 0) * (0.3f * opacity),
                Projectile.rotation, orig, 1.08f, SpriteEffects.None, 0);
            return false;
        }
    }
}
