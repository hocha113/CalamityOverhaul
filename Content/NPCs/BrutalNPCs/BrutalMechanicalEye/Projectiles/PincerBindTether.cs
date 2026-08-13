using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Projectiles
{
    /// <summary>
    /// 钳形投技束缚光绳，纯演出无伤害；ai[0]激光眼whoAmI；ai[1]被缚玩家whoAmI；ai[2]最长持续帧。
    /// 弹射节拍或任一端失效即消散，全端各自模拟绳形，端点数据来自同步实体
    /// </summary>
    internal class PincerBindTether : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        [VaultLoaden(CWRConstant.Masking + "ThunderTrail")]
        private static Asset<Texture2D> ThunderTex = null;

        private const int WarmupTime = 10;
        private const int FadeTime = 10;
        private const int RopePointCount = 14;

        private ref float Timer => ref Projectile.localAI[0];
        private NPC Eye => ((int)Projectile.ai[0]).TryGetNPC(out NPC n) ? n : null;
        private int BoundPlayerIndex => (int)Projectile.ai[1];
        private int Duration => (int)Projectile.ai[2];

        private ThunderTrail strandA;
        private ThunderTrail strandB;
        private float power;
        private bool fading;

        internal static Color RopeColor => new(140, 215, 255);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }

        private Player BoundPlayer {
            get {
                int idx = BoundPlayerIndex;
                if (idx < 0 || idx >= Main.maxPlayers) {
                    return null;
                }
                Player player = Main.player[idx];
                return player != null && player.active && !player.dead ? player : null;
            }
        }

        public override void AI() {
            NPC eye = Eye;
            Player bound = BoundPlayer;

            //任一端失效或弹射开始→收绳
            bool eyeHolding = eye.Alives()
                && TwinsPincerGrabState.TryGetGrabData(eye, out _, out int beat, out _)
                && beat >= TwinsPincerGrabState.BeatClamp
                && beat < TwinsPincerGrabState.BeatEject;
            if (!fading && (!eyeHolding || bound == null)) {
                fading = true;
                if (Timer < Duration - FadeTime) {
                    Timer = Duration - FadeTime;
                }
            }

            if (Timer == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.85f, Pitch = -0.1f }, Projectile.Center);
            }

            //锚定在绳中段
            if (eye.Alives() && bound != null) {
                Projectile.Center = (eye.Center + bound.Center) * 0.5f;
            }

            //功率包络：展开→满功率→消散
            if (Timer < WarmupTime) {
                power = Timer / (float)WarmupTime * 0.4f;
            }
            else if (Timer >= Duration - FadeTime) {
                power = MathHelper.Lerp(1f, 0f, (Timer - (Duration - FadeTime)) / FadeTime);
            }
            else {
                float t = MathHelper.Clamp((Timer - WarmupTime) / 8f, 0f, 1f);
                power = MathHelper.Lerp(0.4f, 1f, VaultUtils.EaseOutCubic(t));
            }

            if ((int)Timer == WarmupTime && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.9f, Pitch = 0.15f }, Projectile.Center);
            }

            Timer++;
            if (Timer >= Duration) {
                Projectile.Kill();
                return;
            }

            if (VaultUtils.isServer || !eye.Alives() || bound == null) {
                return;
            }

            BuildRope(eye.Center, bound.Center);

            //沿绳光照与偶发火花
            for (int i = 0; i < 4; i++) {
                Lighting.AddLight(Vector2.Lerp(eye.Center, bound.Center, i / 3f), RopeColor.ToVector3() * 0.5f * power);
            }
            if (power > 0.6f && Main.rand.NextBool(4)) {
                Vector2 sparkPos = Vector2.Lerp(eye.Center, bound.Center, Main.rand.NextFloat());
                PRTLoader.NewParticle<PRT_TwinsSpark>(sparkPos,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), Color.White, Main.rand.NextFloat(0.8f, 1.3f))?.Configure(13, 0);
            }
        }

        /// <summary>双股绳：两条相位相反的正弦缠绕线</summary>
        private void BuildRope(Vector2 start, Vector2 end) {
            Vector2 dir = end - start;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            float waveSeed = Main.GlobalTimeWrappedHourly * 6f;

            Vector2[] pointsA = new Vector2[RopePointCount];
            Vector2[] pointsB = new Vector2[RopePointCount];
            for (int i = 0; i < RopePointCount; i++) {
                float t = i / (float)(RopePointCount - 1);
                float envelope = (float)Math.Sin(t * MathHelper.Pi);
                float wave = (float)Math.Sin(waveSeed + t * 9f) * 12f * envelope * power;
                pointsA[i] = start + dir * t + perp * wave;
                pointsB[i] = start + dir * t - perp * wave;
            }

            if (strandA == null) {
                strandA = new ThunderTrail(ThunderTex, GetStrandWidth, GetStrandColor, GetRopeAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 3,
                };
                strandA.SetRange((0, 7));
                strandA.SetExpandWidth(4);

                strandB = new ThunderTrail(ThunderTex, GetCoreWidth, GetCoreColor, GetRopeAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                strandB.SetRange((0, 4));
                strandB.SetExpandWidth(3);
            }

            strandA.BasePositions = pointsA;
            strandB.BasePositions = pointsB;
            if ((int)Timer % 4 == 0) {
                strandA.RandomThunder();
                strandB.RandomThunder();
            }
        }

        private float GetStrandWidth(float factor) => (12f + 6f * (float)Math.Sin(factor * MathHelper.Pi)) * power;
        private float GetCoreWidth(float factor) => (6f + 3f * (float)Math.Sin(factor * MathHelper.Pi)) * power;
        private Color GetStrandColor(float factor) => RopeColor;
        private Color GetCoreColor(float factor) => Color.White;
        private float GetRopeAlpha(float factor) => power;

        public override bool PreDraw(ref Color lightColor) {
            if (power <= 0.02f) {
                return false;
            }

            strandA?.DrawThunder(Main.instance.GraphicsDevice);
            strandB?.DrawThunder(Main.instance.GraphicsDevice);

            NPC eye = Eye;
            Player bound = BoundPlayer;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            //黑底辉光贴图必须 A=0 走加色语义
            Color glowColor = RopeColor with { A = 0 };
            float pulse = 1f + 0.18f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 22f);

            if (eye.Alives()) {
                Main.EntitySpriteDraw(glow, eye.Center - Main.screenPosition, null, glowColor * power,
                    0f, glow.Size() / 2f, 0.8f * power * pulse, SpriteEffects.None, 0);
            }
            if (bound != null) {
                //被缚点束环：双圈反向旋转勒紧感
                Texture2D ring = CWRAsset.DiffusionCircle.Value;
                Vector2 drawPos = bound.Center - Main.screenPosition;
                float ringScale = 0.34f + 0.03f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12f);
                Main.EntitySpriteDraw(ring, drawPos, null, glowColor * (0.7f * power),
                    Main.GlobalTimeWrappedHourly * 5f, ring.Size() / 2f, ringScale, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(ring, drawPos, null, (Color.White with { A = 0 }) * (0.4f * power),
                    -Main.GlobalTimeWrappedHourly * 3.6f, ring.Size() / 2f, ringScale * 0.72f, SpriteEffects.None, 0);
                Main.EntitySpriteDraw(glow, drawPos, null, glowColor * (0.55f * power),
                    0f, glow.Size() / 2f, 0.6f * power * pulse, SpriteEffects.None, 0);
            }

            return false;
        }
    }
}
