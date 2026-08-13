using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles
{
    /// <summary>
    /// 瞬移预兆裂隙（无伤纯预告）：ai[0]=1 真 / 0 假
    /// 可学习规则：真裂隙与全局心跳同拍搏动，假裂隙错半拍
    /// </summary>
    internal class BrainTeleportRift : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private bool IsReal => Projectile.ai[0] == 1f;
        private ref float Age => ref Projectile.localAI[0];

        /// <summary>裂隙画布边长（像素）</summary>
        private const float CanvasSize = 300f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 90;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.netImportant = true;
        }

        public override void AI() {
            if (Age == 0f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with {
                    Volume = 0.55f, Pitch = -0.8f, MaxInstances = 6,
                    SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
                }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Zombie103 with {
                    Volume = 0.32f, Pitch = -0.4f, MaxInstances = 3,
                    SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
                }, Projectile.Center);
            }
            Age++;
            Projectile.velocity = Vector2.Zero;

            //撕开期渗血
            if (!VaultUtils.isServer && Age < 20f && Main.rand.NextBool(2) && BrainMotion.OnScreen(Projectile.Center)) {
                BrainMotion.BloodMistBurst(Projectile.Center + Main.rand.NextVector2Circular(24f, 40f), 0.4f, 1, 3f);
            }

            Lighting.AddLight(Projectile.Center, BrainMotion.BloodDark.ToVector3() * (0.5f + PulseEnvelope() * 0.5f));
        }

        /// <summary>心跳搏动包络：真=同拍，假=错半拍（唯一可靠破绽）</summary>
        private float PulseEnvelope() {
            NPC brain = BrainMotion.FindBrain();
            if (brain == null) {
                return 0f;
            }
            //与全局心跳同一周期公式（ai[0]<0=二阶段40帧拍，否则54），假体偏移半拍
            float period = brain.ai[0] < 0f ? 40f : 54f;
            float clock = brain.ai[3];
            if (!IsReal) {
                clock += period * 0.5f;
            }
            float phase = clock % period / period;
            //收缩期锐脉冲
            return (float)Math.Exp(-phase * 6.5f);
        }

        public override void OnKill(int timeLeft) {
            //真裂隙由脑穿出（脑侧瞬移演出补重拍），假裂隙干瘪塌缩
            if (VaultUtils.isServer || !BrainMotion.OnScreen(Projectile.Center)) {
                return;
            }
            if (IsReal) {
                BrainMotion.BloodMistBurst(Projectile.Center, 1.3f, 8, 8f);
            }
            else {
                BrainMotion.BloodMistBurst(Projectile.Center, 0.5f, 2, 3f);
                BrainMotion.FleshSquish(Projectile.Center, 0.5f, -0.75f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float openT = MathHelper.Clamp(Age / 20f, 0f, 1f);
            float closeT = Projectile.timeLeft < 14 ? Projectile.timeLeft / 14f : 1f;
            float open = BrainMotion.SharpOut(openT, 4) * closeT;
            float pulse = PulseEnvelope();
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Effect shader = EffectLoader.BrainRift?.Value;
            if (shader != null) {
                //placeholder2 = 1x1 白像素画布，quad UV 天然铺满 0~1
                Texture2D canvas = CWRUtils.GetT2DAsset(CWRConstant.VaultPlaceholder2).Value;
                float scale = CanvasSize / canvas.Width;

                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uOpen"]?.SetValue(open);
                shader.Parameters["uPulse"]?.SetValue(pulse);
                shader.Parameters["uSeed"]?.SetValue(Projectile.identity % 97 * 0.173f);
                shader.Parameters["uNoise"]?.SetValue(CWRAsset.PerlinNoise.Value);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                shader.CurrentTechnique.Passes[0].Apply();

                Main.spriteBatch.Draw(canvas, drawPos, null, Color.White, 0f,
                    canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                return false;
            }

            //着色器缺失回退：血泪撕裂贴图+脉动光
            Texture2D tear = CWRUtils.GetT2DAsset(CWRConstant.Masking + "RedTearBig01").Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float fbScale = open * 0.8f;
            Main.spriteBatch.Draw(glow, drawPos, null, new Color(140, 16, 26, 0) * (0.7f * open + pulse * 0.3f),
                0f, glow.Size() * 0.5f, fbScale * 3.4f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tear, drawPos, null, new Color(190, 30, 40, 200) * open,
                0f, tear.Size() * 0.5f, fbScale * (1f + pulse * 0.12f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
