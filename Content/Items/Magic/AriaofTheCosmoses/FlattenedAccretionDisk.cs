using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// 右键·事件视界领域：蓝紫高能态黑洞架成炮台
    /// 盘面垂直于瞄准方向,喷流轴指向鼠标;蓄满后沿轴持续点射伽马射线
    /// rotation/ChargeProgress 由手持弹幕每帧喂入
    internal class FlattenedAccretionDisk : ModProjectile, IPrimitiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>0~1 蓄力进度(手持喂入)</summary>
        public ref float ChargeProgress => ref Projectile.ai[2];

        private const float FireThreshold = 0.8f;
        private const int FireInterval = 7;

        private float visTime;
        private float spinPhase;
        private float fade;
        private float jetPower;
        private int gammaRayTimer;

        /// <summary>绘制quad边长：领域直径的2.4倍留辉光余量</summary>
        private float QuadSide => Projectile.width * Projectile.scale * 2.4f;
        private float Seed => Projectile.whoAmI * 0.137f % 1f;

        public override void SetDefaults() {
            Projectile.width = 500;
            Projectile.height = 500;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.scale = 0.35f;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            visTime += 1f / 60f;
            fade = Math.Min(fade + 0.09f, 1f);
            Projectile.scale = MathHelper.Lerp(0.35f, 1f, ChargeProgress);

            //自旋相位积分
            spinPhase += MathHelper.Lerp(1.2f, 3f, ChargeProgress) / 60f;

            //喷流功率：达阈值后爬升,未达缓降
            float jetTarget = ChargeProgress >= FireThreshold ? 1f : 0f;
            jetPower = MathHelper.Lerp(jetPower, jetTarget, 0.12f);

            //引力拉拽领域内敌人(比左键温和,持续)
            float pullR = QuadSide * 0.55f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile) || npc.boss || npc.knockBackResist <= 0f) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < pullR && dist > 20f) {
                    float factor = 1f - dist / pullR;
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero)
                        * 3f * factor * npc.knockBackResist;
                }
            }

            //蓄满:沿喷流轴点射伽马射线
            if (ChargeProgress >= FireThreshold) {
                gammaRayTimer++;
                if (gammaRayTimer >= FireInterval) {
                    ShootGammaRay();
                    gammaRayTimer = 0;
                }
            }

            EmitParticles();

            Lighting.AddLight(Projectile.Center,
                GammaRayBeam.ColViolet.ToVector3() * (0.6f + ChargeProgress * 0.8f) * fade);
        }

        private void ShootGammaRay() {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item75 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);
            }
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //沿盘轴(瞄准方向)发射,微散布
            Vector2 dir = Projectile.rotation.ToRotationVector2().RotatedByRandom(0.06f);
            int damage = (int)(Projectile.damage * (0.4f + ChargeProgress * 0.35f));

            Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                Projectile.Center + dir * 30f, dir * 4f,
                ModContent.ProjectileType<GammaRayBeam>(), damage, 2f, Projectile.owner,
                0f, Main.rand.NextFloat());
        }

        private void EmitParticles() {
            if (VaultUtils.isServer) {
                return;
            }

            //蓝紫坠入流:贴视界的切向裂隙
            if (Projectile.timeLeft % 3 == 0 && fade > 0.5f) {
                float hr = QuadSide * 0.075f;
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * hr * Main.rand.NextFloat(1.4f, 2.4f);
                Vector2 vel = ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(2.5f, 5f)
                    + (Projectile.Center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(1f, 2.5f);
                PRTLoader.NewParticle<PRT_SpaceFracture>(pos, vel,
                    Color.Lerp(GammaRayBeam.ColViolet, GammaRayBeam.ColCheren, Main.rand.NextFloat()) * 0.9f,
                    Main.rand.NextFloat(0.3f, 0.6f))?.Configure(Main.rand.Next(12, 22), Main.rand.NextFloat(-0.4f, 0.4f));
            }

            //吸入光点
            if (Projectile.timeLeft % 6 == 0 && ChargeProgress > 0.2f) {
                PRTLoader.NewParticle<PRT_GravityVortex>(Projectile.Center, Vector2.Zero,
                    Color.Lerp(GammaRayBeam.ColCheren, GammaRayBeam.ColCore, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.35f, 0.65f))
                    ?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), QuadSide * Main.rand.NextFloat(0.3f, 0.5f), Main.rand.Next(35, 55));
            }

            //喷流工作时:沿轴电离飞沫
            if (jetPower > 0.3f && Main.rand.NextBool(2)) {
                Vector2 dir = Projectile.rotation.ToRotationVector2();
                Vector2 pos = Projectile.Center + dir * Main.rand.NextFloat(20f, QuadSide * 0.4f)
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-10f, 10f);
                PRTLoader.NewParticle<PRT_Spark>(pos, dir * Main.rand.NextFloat(6f, 14f),
                    Color.Lerp(GammaRayBeam.ColCore, GammaRayBeam.ColCheren, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);

            //领域坍缩:蓝紫裂隙内爆
            int count = (int)(18 * Projectile.scale) + 8;
            for (int i = 0; i < count; i++) {
                Vector2 spawn = Projectile.Center + Main.rand.NextVector2Circular(1f, 1f) * QuadSide * 0.35f;
                PRTLoader.NewParticle<PRT_SpaceFracture>(spawn,
                    (Projectile.Center - spawn).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(6f, 13f),
                    Color.Lerp(GammaRayBeam.ColViolet, GammaRayBeam.ColCheren, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.4f, 0.9f))?.Configure(Main.rand.Next(14, 26), Main.rand.NextFloat(-0.5f, 0.5f));
            }
        }

        //=================== 绘制 ===================

        public override bool PreDraw(ref Color lightColor) => false;
        public bool CanDrawCustom() => false;
        public void DrawCustom(SpriteBatch spriteBatch) { }

        public void Warp() {
            if (fade < 0.1f) {
                return;
            }
            float size = MathHelper.Clamp(QuadSide * 1.15f, 200f, 2200f);
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size,
                MathHelper.Lerp(0.1f, 0.42f, ChargeProgress) * fade, 1f, 0f, "GravitationalLens");
        }

        public void DrawPrimitives() {
            if (VaultUtils.isServer || fade <= 0.02f) {
                return;
            }

            Effect effect = EffectLoader.AriaBlackHole?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D white = VaultAsset.placeholder2?.Value;
            if (effect == null || noise == null || white == null) {
                return;
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float side = QuadSide;
            Vector2 texHalf = white.Size() * 0.5f;
            Vector2 quadScale = new(side / white.Width, side / white.Height);
            //quad 局部-y轴对准瞄准方向:喷流指向鼠标,盘面垂直于瞄准
            float quadRot = Projectile.rotation + MathHelper.PiOver2;

            Matrix finalMatrix = Main.GameViewMatrix.TransformationMatrix
                * Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

            float breath = (float)Math.Sin(visTime * MathHelper.TwoPi * 2.5f);
            float charge = ChargeProgress;

            effect.Parameters["transformMatrix"]?.SetValue(finalMatrix);
            effect.Parameters["uTime"]?.SetValue(visTime);
            effect.Parameters["uSpinPhase"]?.SetValue(spinPhase);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uFade"]?.SetValue(fade);
            effect.Parameters["uStretch"]?.SetValue(1f);
            effect.Parameters["uMotAngle"]?.SetValue(0f);
            effect.Parameters["uStarR"]?.SetValue(0f);
            effect.Parameters["uStarBright"]?.SetValue(0f);
            effect.Parameters["uCollapse"]?.SetValue(0f);
            effect.Parameters["uHorizonR"]?.SetValue(0.075f);
            effect.Parameters["uRingBright"]?.SetValue(0.9f + charge * 0.4f + jetPower * 0.25f * breath);
            effect.Parameters["uDiskIn"]?.SetValue(0.10f);
            effect.Parameters["uDiskOut"]?.SetValue(MathHelper.Lerp(0.20f, 0.42f, charge));
            effect.Parameters["uDiskFlat"]?.SetValue(MathHelper.Lerp(0.55f, 0.30f, charge));
            effect.Parameters["uDiskBright"]?.SetValue(0.9f + charge * 0.3f);
            effect.Parameters["uArc"]?.SetValue(charge);
            effect.Parameters["uDoppler"]?.SetValue(0.5f);
            effect.Parameters["uInflow"]?.SetValue(0.5f + charge * 0.4f + breath * 0.12f);
            effect.Parameters["uBlueshift"]?.SetValue(jetPower * (0.5f + 0.2f * breath));
            effect.Parameters["uFlash"]?.SetValue(0f);
            effect.Parameters["uJet"]?.SetValue(jetPower * (0.85f + 0.15f * breath));
            effect.Parameters["uJetAsym"]?.SetValue(1f);
            effect.Parameters["uPalShift"]?.SetValue(1f);
            effect.Parameters["noiseTexture"]?.SetValue(noise);

            //Pass1:暗背板+视界
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique = effect.Techniques["Backdrop"];
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(white, drawPos, null, Color.White, quadRot, texHalf, quadScale, SpriteEffects.None, 0);
            sb.End();

            //Pass2:发光层
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique = effect.Techniques["Glow"];
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(white, drawPos, null, Color.White, quadRot, texHalf, quadScale, SpriteEffects.None, 0);
            sb.End();
        }
    }
}
