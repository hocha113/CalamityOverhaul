using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.FrostLegion.Projectiles
{
    /// <summary>
    /// 曲射大雪球（迫击弹视觉载体）：ai[0]=引信帧。全程无判定——威胁只在被警示环标记的落点，
    /// 定时长抛物线由落点环解算端与 <see cref="Gravity"/> 严格对齐，引信归零即自毁交棒给落点迸裂。
    /// 穿墙飞行（曲射语义：翻越工事，落点由警示环诚实宣告）；原版雪球贴图实体层 + 同材质拖尾
    /// </summary>
    internal class FlgMortarShellProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SnowBallHostile;

        /// <summary>每帧重力（落点环的弹道解算与此对齐，改一处必改两处）</summary>
        internal const float Gravity = 0.22f;
        /// <summary>大雪球绘制比例</summary>
        private const float ShellScale = 1.55f;

        private int FuseFrames => Math.Max((int)Projectile.ai[0], 10);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = false;//纯视觉载体，永不判定
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = FuseFrames;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = -0.45f, MaxInstances = 5 }, Projectile.Center);
                }
            }

            //抛物线：不设落速钳制，保证与解算弹道严格一致（落点承诺）
            Projectile.velocity.Y += Gravity;
            Projectile.rotation += Projectile.velocity.X * 0.03f + 0.07f;

            if (!Main.dedServ && Main.rand.NextBool(5)) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    -Projectile.velocity * 0.1f, 140, default, 0.9f);
                dust.noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //抵达即消隐（迸裂表现与雪片归落点环所有）
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.Snow,
                    Main.rand.NextVector2Circular(1.8f, 1.8f), 110, default, 1.1f);
                dust.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SnowBallHostile);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.SnowBallHostile].Value;
            int frames = Main.projFrames[ProjectileID.SnowBallHostile] > 0 ? Main.projFrames[ProjectileID.SnowBallHostile] : 1;
            Rectangle rect = tex.Frame(1, frames, 0, 0);
            Vector2 orig = rect.Size() / 2f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color body = Color.Lerp(lightColor, new Color(206, 226, 248), 0.5f);

            //同材质拖尾（旧位重画，横轴 ≥0.5×弹体）
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 oldPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, oldPos, rect, body * (0.32f * t), Projectile.rotation - i * 0.08f,
                    orig, ShellScale * (0.55f + 0.35f * t), SpriteEffects.None, 0);
            }

            //弱辉光敷料 + 原版雪球实体层（真 alpha 遮挡像素）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float twinkle = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + Projectile.identity);
            Main.EntitySpriteDraw(glow, pos, null, new Color(190, 225, 255, 0) * (0.28f * twinkle), 0f,
                glow.Size() / 2f, 0.4f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, pos, rect, body, Projectile.rotation, orig, ShellScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
