using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Wastes.Projectiles
{
    /// <summary>
    /// 锥幕沙弹。ai[0]=每帧重力 ai[1]=变体着色（0沙/1腐化/2猩红/3神圣，食尸鬼风味）。
    /// 出膛短暂淡入且淡入期无判定（公平阀）；原版沙块贴图实体层 + 同材质拖尾
    /// </summary>
    internal class WastesSandPelletProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallFalling;

        /// <summary>淡入帧数，判定开启与可见同门</summary>
        private const int FadeInFrames = 8;
        /// <summary>下坠终端速度</summary>
        private const float MaxFallSpeed = 12f;

        private ref float Gravity => ref Projectile.ai[0];
        private int Tint => Math.Clamp((int)Projectile.ai[1], 0, 3);
        private ref float Age => ref Projectile.localAI[0];

        /// <summary>变体主色（沙/腐化/猩红/神圣）</summary>
        private static readonly Color[] TintColors = [
            new Color(230, 204, 128),
            new Color(178, 148, 200),
            new Color(224, 122, 100),
            new Color(255, 216, 235),
        ];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / FadeInFrames, 0f, 1f));

            Projectile.velocity.Y += Gravity;
            if (Projectile.velocity.Y > MaxFallSpeed) {
                Projectile.velocity.Y = MaxFallSpeed;
            }
            //沙块翻滚
            Projectile.rotation += 0.17f * (Projectile.velocity.X >= 0f ? 1f : -1f);

            //沿途细沙（低频）
            if (!Main.dedServ && Main.rand.NextBool(6)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    -Projectile.velocity * 0.1f, 140, default, 0.8f);
                dust.noGravity = true;
            }
        }

        /// <summary>淡入完成才有杀伤（公平阀）</summary>
        public override bool? CanDamage() => Age > 6 ? null : false;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 orig = tex.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;
            Color body = Color.Lerp(lightColor, TintColors[Tint], 0.55f) * opacity;

            //同材质拖尾（横轴粗细 ≥ 弹体一半）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, drawPos, null, body * (0.4f * t), Projectile.rotation,
                    orig, Projectile.scale * (0.55f + 0.3f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, body,
                Projectile.rotation, orig, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Sand,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.6f) * Main.rand.NextFloat(0.5f, 2f)
                    + Main.rand.NextVector2Circular(1.2f, 1.2f), 100, default, Main.rand.NextFloat(0.8f, 1.2f));
                dust.noGravity = Main.rand.NextBool();
            }
        }
    }
}
