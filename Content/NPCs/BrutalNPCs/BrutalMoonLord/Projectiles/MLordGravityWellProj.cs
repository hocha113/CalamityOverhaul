using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles
{
    /// <summary>
    /// 引力井：牵引玩家 + 引力透镜扭曲 + 环绕星尘吸积。
    /// 崩解时放出环形幻影眼（服务端）。本体不接触伤害
    /// </summary>
    internal class MLordGravityWellProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        internal const int ExpandTime = 30;
        internal const int HoldTime = 220;
        internal const int CollapseTime = 26;
        internal const int TotalLife = ExpandTime + HoldTime + CollapseTime;

        private const float PullRadius = 920f;
        private const float HardPullRadius = 320f;
        /// <summary>公平阀：向井分速度达此值后不再施力，全程保留主动挣脱手段（契约3）</summary>
        internal const float EscapeTowardSpeedCap = 8.5f;

        private ref float Timer => ref Projectile.localAI[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
        }

        /// <summary>体量包络 0~1</summary>
        private float Envelope {
            get {
                float rise = MathHelper.Clamp(Timer / ExpandTime, 0f, 1f);
                float fall = MathHelper.Clamp((TotalLife - Timer) / CollapseTime, 0f, 1f);
                //崩解前塌缩到 0.42：变小再变响
                return Math.Min(VaultUtils.EaseOutCubic(rise), MathHelper.Lerp(0.42f, 1f, fall));
            }
        }

        public override void AI() {
            Timer++;
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, MLordDirector.DeepViolet.ToVector3() * 0.9f * Envelope);

            if (Timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.BlackHole with { Volume = 0.85f, Pitch = -0.2f }, Projectile.Center);
            }

            //牵引本地玩家（各端只推自己，动作权威在玩家本地）
            Player local = Main.LocalPlayer;
            if (!VaultUtils.isServer && local.active && !local.dead && Envelope > 0.3f) {
                Vector2 toWell = Projectile.Center - local.Center;
                float dist = toWell.Length();
                if (dist < PullRadius && dist > 40f) {
                    float strength = MathHelper.Lerp(0.1f, 0.42f,
                        MathHelper.Clamp(1f - (dist - HardPullRadius) / (PullRadius - HardPullRadius), 0f, 1f));
                    Vector2 pull = toWell.SafeNormalize(Vector2.Zero) * strength;
                    //只在被拉向井的分速度低于逃逸阀时施力，保留挣脱空间
                    float towardSpeed = Vector2.Dot(local.velocity, toWell.SafeNormalize(Vector2.Zero));
                    if (towardSpeed < EscapeTowardSpeedCap) {
                        local.velocity += pull;
                    }
                }
            }

            //崩解拍
            if ((int)Timer == TotalLife - CollapseTime) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.9f, Pitch = -0.5f }, Projectile.Center);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }

            //吸积星尘：外圈拉入 + 切向涡旋
            MLordScreenFX.ConvergeStreak(Projectile.Center, 360f * Envelope, Timer / (float)TotalLife * 0.6f);
            if (Main.rand.NextBool(3)) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * Main.rand.NextFloat(70f, 150f) * Envelope;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos,
                    (pos - Projectile.Center).SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(2f, 5f),
                    MLordDirector.Phantasmal, Main.rand.NextFloat(0.4f, 0.75f))?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        public override void OnKill(int timeLeft) {
            //崩解爆发：屏效 + 环形幻影眼
            if (!VaultUtils.isServer) {
                MLordScreenEffects.PushStarRing(Projectile.Center, 0.9f, 700f, 30);
                MLordScreenFX.StarBurst(Projectile.Center, 1.4f, 22);
                MLordScreenFX.Punch(Projectile.Center, 7f, 14);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1f, Pitch = -0.4f }, Projectile.Center);
            }
            if (!VaultUtils.isClient) {
                int damage = (int)Projectile.ai[0];
                if (damage <= 0) {
                    damage = MLordDirector.EyeDamage;
                }
                int count = 10;
                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < count; i++) {
                    float angle = baseAngle + MathHelper.TwoPi / count * i;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                        angle.ToRotationVector2() * 7.5f, ProjectileID.PhantasmalEye, damage, 0f, Main.myPlayer);
                }
            }
        }

        #region 扭曲与绘制

        public bool DontUseBlueshiftEffect() => true;

        public bool CanDrawCustom() => false;

        public void DrawCustom(SpriteBatch spriteBatch) { }

        public void Warp() {
            float env = Envelope;
            if (env <= 0.05f) {
                return;
            }
            float size = 760f * env;
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size, 0.34f, env, 0f, "GravitationalLens", 0.42f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow == null) {
                return false;
            }
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            float env = Envelope;
            float spin = Main.GlobalTimeWrappedHourly * 1.7f;

            //暗核：吸光体（AlphaBlend 深色，不走加色）
            Main.EntitySpriteDraw(glow, screenPos, null, new Color(8, 4, 22) * (0.92f * env),
                spin, glow.Size() / 2f, 0.5f * env, SpriteEffects.None, 0);
            //侧倾吸积盘（各向异性：横长竖扁）
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.DeepViolet with { A = 0 } * (0.75f * env),
                spin * 0.6f, glow.Size() / 2f, new Vector2(0.95f, 0.3f) * env, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, screenPos, null, MLordDirector.Phantasmal with { A = 0 } * (0.55f * env),
                -spin * 0.8f, glow.Size() / 2f, new Vector2(0.62f, 0.2f) * env, SpriteEffects.None, 0);
            return false;
        }

        #endregion
    }
}
