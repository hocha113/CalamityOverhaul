using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Cindercrag.Projectiles
{
    /// <summary>
    /// 「恸嚎波」：低频女妖恸嚎的声压波前，纯声压演出，伤害为零。
    /// 从远处水平推进（远嚎渐近+空气涟漪即预告，抵达用时 ≥140 帧远超公平契约），
    /// 波前扫过本地玩家时给极轻屏震与短暂原版 Weak，然后远去消散。
    /// 减益在被扫者本机结算（原版 buff 原生同步），Boss 在场/城镇安宁时只过风不上减益
    /// </summary>
    internal class CindercragWailWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>起波距离（速度 8.5px/t → 约 147 帧后抵达）</summary>
        internal const float ApproachDist = 1250f;
        internal const float WaveSpeed = 8.5f;
        private const int LifeFrames = 340;
        /// <summary>波前判定半高（同层洞窟内才算被扫过）</summary>
        private const float PassHalfHeight = 520f;
        /// <summary>Weak 时长（短暂）</summary>
        private const int WeakTicks = 240;
        /// <summary>波前可视半高</summary>
        private const float RippleHalfHeight = 240f;

        /// <summary>涟漪淡红灰（哀音的颜色）</summary>
        private static readonly Color RippleTint = new(210, 150, 140);

        /// <summary>本端本地玩家是否已被扫过（各端表现私产，只关心自己的玩家）</summary>
        private bool passedLocal;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = false;//恒零伤害
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifeFrames;
            Projectile.netImportant = true;
        }

        /// <summary>整体响度包络：起波淡入，尾段远去淡出</summary>
        private float Envelope {
            get {
                int elapsed = LifeFrames - Projectile.timeLeft;
                float fadeIn = MathHelper.Clamp(elapsed / 20f, 0f, 1f);
                float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 50f, 0f, 1f);
                return fadeIn * fadeOut;
            }
        }

        public override void AI() {
            int elapsed = LifeFrames - Projectile.timeLeft;

            if (Main.dedServ) {
                return;//波前推进由速度自走，服务端无需任何逻辑
            }

            //远嚎渐近：波前上周期性发声，定位衰减天然给出"由远及近"
            if (elapsed % 40 == 0) {
                SoundEngine.PlaySound(SoundID.Zombie103 with {
                    Volume = 0.5f * Envelope,
                    Pitch = -0.5f + Main.rand.NextFloat(-0.08f, 0.08f),
                    MaxInstances = 3,
                }, Projectile.Center);
            }

            //空气涟漪的粒子通道：波前扬灰
            if (Main.rand.NextBool(3)) {
                Dust ash = Dust.NewDustPerfect(
                    Projectile.Center + new Vector2(Main.rand.NextFloat(-10f, 10f),
                        Main.rand.NextFloat(-RippleHalfHeight, RippleHalfHeight)),
                    DustID.Ash, new Vector2(Projectile.velocity.X * 0.25f, -Main.rand.NextFloat(0.3f, 1f)),
                    140, default, Main.rand.NextFloat(0.8f, 1.3f));
                ash.noGravity = true;
            }

            //扫身判定：只管本机玩家（减益本机结算走原生 buff 同步，屏震只震自己）
            Player local = Main.LocalPlayer;
            if (passedLocal || local == null || !local.active || local.dead) {
                return;
            }
            float ahead = (local.Center.X - Projectile.Center.X) * MathF.Sign(Projectile.velocity.X);
            if (ahead > 0f || MathF.Abs(local.Center.Y - Projectile.Center.Y) > PassHalfHeight) {
                return;
            }
            passedLocal = true;
            if (CindercragAmbience.ZoneOf(local) && !CWRWorld.HasBoss && !CindercragAmbience.TownSafe(local)) {
                local.AddBuff(BuffID.Weak, WeakTicks);
                local.CWR().GetScreenShake(3.2f);
                SoundEngine.PlaySound(SoundID.Zombie103 with {
                    Volume = 0.62f, Pitch = -0.6f, MaxInstances = 2,
                }, local.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float env = Envelope;
            if (env <= 0.01f) {
                return false;
            }
            Texture2D flow = CWRAsset.Airflow?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (flow == null || glow == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float sign = MathF.Sign(Projectile.velocity.X);
            float t = Main.GlobalTimeWrappedHourly;
            Color tint = RippleTint with { A = 0 };

            //空气涟漪：竖立的气流帘三层，前缘最亮，身后拖两道渐弱残纹；轻微摆动读作空气褶皱
            Vector2 flowOrigin = flow.Size() * 0.5f;
            ReadOnlySpan<float> layerBack = [0f, 16f, 34f];
            ReadOnlySpan<float> layerAlpha = [0.12f, 0.075f, 0.045f];
            ReadOnlySpan<float> layerThick = [0.2f, 0.15f, 0.11f];
            for (int i = 0; i < 3; i++) {
                float wobble = MathF.Sin(t * 3.1f + i * 1.9f + Projectile.identity) * 0.05f;
                Vector2 layerPos = pos - new Vector2(sign * layerBack[i], 0f);
                DrawRippleLayer(layerPos, flow, flowOrigin, tint * (layerAlpha[i] * env),
                    -MathHelper.PiOver2 + wobble,
                    new Vector2(RippleHalfHeight * 2f / flow.Width, layerThick[i]));
            }

            //波前柔光：竖压的微光,标出声压的"身位"
            Main.EntitySpriteDraw(glow, pos, null, tint * (0.10f * env), 0f,
                glow.Size() * 0.5f, new Vector2(0.55f, RippleHalfHeight * 2f / glow.Height * 0.9f),
                SpriteEffects.None, 0);
            return false;
        }

        /// <summary>涟漪层统一画法（气流贴图转竖，长轴即涟漪高度）</summary>
        private static void DrawRippleLayer(Vector2 pos, Texture2D tex, Vector2 origin,
            Color color, float rotation, Vector2 scale)
            => Main.EntitySpriteDraw(tex, pos, null, color, rotation, origin, scale, SpriteEffects.None, 0);
    }
}
