using CalamityOverhaul.Content.Items.Stones;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Stoneborn.Projectiles
{
    /// <summary>
    /// 豪杰骷髅·仪式标枪：原版 JavelinHostile 贴图即遮挡体（M5 原版贴图优先），
    /// 飞行参数镜像原版 508（45 帧平飞后吃重力坠成抛物线）。
    /// 淡入完成才有杀伤（伤害窗=可见窗），拖尾为同贴图残影 ≥0.5× 横轴
    /// </summary>
    internal class StonebornJavelinProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.JavelinHostile;

        /// <summary>淡入帧（无判定窗）</summary>
        private const int FadeInFrames = 6;
        /// <summary>平飞帧数：镜像原版 508 的 45 帧后坠</summary>
        private const int GravityDelayFrames = 45;
        /// <summary>坠落每帧增量与横向阻尼（镜像原版 508）</summary>
        private const float GravityPerFrame = 0.3f;
        private const float DragX = 0.98f;
        private const float MaxFallSpeed = 12f;

        private ref float Age => ref Projectile.localAI[0];

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
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            Projectile.alpha = 200;
        }

        /// <summary>淡入完成才有杀伤</summary>
        public override bool? CanDamage() => Age > FadeInFrames ? null : false;

        public override void AI() {
            Age++;
            Projectile.alpha = (int)MathHelper.Lerp(200f, 0f, MathHelper.Clamp(Age / (float)FadeInFrames, 0f, 1f));

            if (Age > GravityDelayFrames) {
                Projectile.velocity.X *= DragX;
                Projectile.velocity.Y += GravityPerFrame;
                if (Projectile.velocity.Y > MaxFallSpeed) {
                    Projectile.velocity.Y = MaxFallSpeed;
                }
            }
            //+PiOver2：原版标枪贴图朝上（aiStyle 1 默认旋转修正）
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame,
                    -Projectile.velocity * 0.1f, 140, default, 0.7f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 5 }, Projectile.Center);
            for (int i = 0; i < 4; i++) {
                Dust.NewDustPerfect(Projectile.Center, DustID.Stone,
                    -Projectile.velocity.SafeNormalize(Vector2.UnitY).RotatedByRandom(0.6f) * Main.rand.NextFloat(1f, 2.6f),
                    100, default, 0.9f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() / 2f;
            float opacity = 1f - Projectile.alpha / 255f;

            //同贴图残影拖尾（M5：同材质、横轴同宽 ≥0.5×）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 old = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, old, null,
                    GraniteMarbleVFX.MarbleGold * (0.3f * t * opacity), Projectile.rotation,
                    origin, 0.85f, SpriteEffects.None, 0);
            }
            //本体：原版贴图，微暖提亮辨识
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                Color.Lerp(lightColor, GraniteMarbleVFX.MarbleCore, 0.25f) * opacity, Projectile.rotation,
                origin, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}
