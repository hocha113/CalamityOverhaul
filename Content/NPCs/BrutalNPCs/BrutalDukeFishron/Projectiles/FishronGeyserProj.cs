using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles
{
    /// <summary>
    /// 间歇泉水柱：地面泡沫预兆→喷发→塌落。
    /// ai[0]=预兆延迟帧 ai[1]=柱高 localAI[0]=计时
    /// </summary>
    internal class FishronGeyserProj : Terraria.ModLoader.ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "MaskLaserLine";

        internal const int GeyserDamage = 40;
        private const int EruptTime = 42;
        private const int CollapseTime = 18;

        private ref float DelayFrames => ref Projectile.ai[0];
        private ref float ColumnHeight => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];

        private bool Erupting => Timer > DelayFrames && Timer <= DelayFrames + EruptTime;
        private float EruptProgress => MathHelper.Clamp((Timer - DelayFrames) / 10f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 70;
            Projectile.height = 400;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void SetStaticDefaults() {
            //顶部摆动与外层柔光宽于命中盒
            Terraria.ID.ProjectileID.Sets.DrawScreenCheckFluff[Type] = 160;
        }

        public override void AI() {
            Timer++;

            //首帧锚定：Center 视作地面喷口，向上立起判定箱
            if (Timer == 1) {
                if (ColumnHeight < 100f) {
                    ColumnHeight = 400f;
                }
                Vector2 mouth = FishronMotionFX.FindSurfaceBelow(Projectile.Center - new Vector2(0, 60f), out _);
                Projectile.position = new Vector2(mouth.X - Projectile.width / 2f, mouth.Y - ColumnHeight);
                Projectile.height = (int)ColumnHeight;
                Projectile.timeLeft = (int)(DelayFrames + EruptTime + CollapseTime);
            }

            Vector2 vent = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);

            //判定只在喷发段
            Projectile.damage = Erupting ? GeyserDamage : 0;

            if (VaultUtils.isServer) {
                return;
            }

            //预兆：喷口泡沫渐密
            if (Timer <= DelayFrames) {
                float t = Timer / Math.Max(DelayFrames, 1f);
                if (Main.rand.NextBool(3)) {
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_FishronFoam>(
                        vent + new Vector2(Main.rand.NextFloat(-30f, 30f), -4f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f),
                        FishronMotionFX.FoamWhite * (0.3f + t * 0.35f),
                        Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(18, 30), Main.rand.NextFloat(-0.03f, 0.03f));
                }
                if (Timer % 5 == 0) {
                    FishronMotionFX.SpawnSprayCone(vent, -Vector2.UnitY, 1, 1f, 2.5f + t * 3f, 0.3f, 0.7f);
                }
                return;
            }

            //喷发帧
            if ((int)Timer == (int)DelayFrames + 1) {
                FishronMotionFX.SpawnSplashBurst(vent, 1.2f);
                SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.7f, Pitch = -0.2f, MaxInstances = 4 }, vent);
            }

            //喷发中：整柱高压水花
            if (Erupting) {
                for (int i = 0; i < 3; i++) {
                    float h = Main.rand.NextFloat();
                    Vector2 pos = vent - new Vector2(0, ColumnHeight * h * EruptProgress);
                    FishronMotionFX.SpawnSprayCone(pos, -Vector2.UnitY, 1, 4f, 11f, 0.35f, 1f);
                }
                Lighting.AddLight(vent - new Vector2(0, ColumnHeight * 0.5f),
                    FishronMotionFX.SeaGreen.ToVector3() * 0.8f);
            }
        }

        public override bool CanHitPlayer(Player target) => Erupting;

        public override bool PreDraw(ref Color lightColor) {
            if (Timer <= DelayFrames) {
                return false;
            }
            //喷发/塌落包络
            float erupt = EruptProgress;
            float collapse = Timer > DelayFrames + EruptTime
                ? 1f - MathHelper.Clamp((Timer - DelayFrames - EruptTime) / CollapseTime, 0f, 1f) : 1f;
            float env = erupt * collapse;
            if (env <= 0.01f) {
                return false;
            }

            Vector2 vent = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            float wobble = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 22f + Projectile.whoAmI) * 0.06f;

            //主路径走预警线着色器：根实(0.015羽化)、生长前沿毛边、末端渐隐、
            //退场向轴心收细——包络全在像素层连续，杜绝分段量化条带
            Effect effect = EffectLoader.FishronTelegraph?.Value;
            if (effect != null) {
                DrawShaderColumn(effect, vent, erupt, collapse, wobble);
                return false;
            }

            DrawSpriteColumn(vent, env, wobble);
            return false;
        }

        /// <summary>单 quad 着色器水柱：uGrow=喷发爬升，uCollapse=断流收细，整柱轻摆</summary>
        private void DrawShaderColumn(Effect effect, Vector2 vent, float erupt, float collapse, float wobble) {
            const float Width = 118f;
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uIntensity"]?.SetValue(0.9f * (0.35f + collapse * 0.65f));
            effect.Parameters["uGrow"]?.SetValue(erupt);
            effect.Parameters["uLockProgress"]?.SetValue(0f);
            effect.Parameters["uCollapse"]?.SetValue(1f - collapse);
            effect.Parameters["uAspect"]?.SetValue(ColumnHeight / Width);
            effect.Parameters["uRootFeather"]?.SetValue(0.015f);
            effect.Parameters["uColor"]?.SetValue(new Vector3(0.24f, 0.72f, 0.74f));

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            float rot = -MathHelper.PiOver2 + wobble * 0.5f;
            Vector2 scale = new(ColumnHeight / pixel.Width, Width / pixel.Height);
            sb.Draw(pixel, vent - Main.screenPosition, null, Color.White,
                rot, new Vector2(0, pixel.Height / 2f), scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>
        /// 着色器缺失兜底：MaskLaserLine 沿长全亮无端衰减，整条拉伸必在柱顶留硬切平面，
        /// 故分 28 段、包络按段中点取值收针——段间仍有小台阶，仅作兜底档
        /// </summary>
        private void DrawSpriteColumn(Vector2 vent, float env, float wobble) {
            Texture2D line = TextureAssets.Projectile[Type].Value;
            Color outer = new(FishronMotionFX.SeaGreen.R, FishronMotionFX.SeaGreen.G, FishronMotionFX.SeaGreen.B, 0);
            Color core = new(FishronMotionFX.FoamWhite.R, FishronMotionFX.FoamWhite.G, FishronMotionFX.FoamWhite.B, 0);

            const int Segments = 28;
            float colLen = ColumnHeight * env;
            float segLen = colLen / Segments;
            for (int i = 0; i < Segments; i++) {
                float t0 = i / (float)Segments;
                //顶端 45% 收针：按段中点取包络，首尾段各贴 0/1 端，台阶减半
                float tMid = (i + 0.5f) / Segments;
                float tipT = MathHelper.Clamp((1f - tMid) / 0.45f, 0f, 1f);
                tipT = tipT * tipT * (3f - 2f * tipT);
                float envSeg = env * tipT;
                float widthSeg = 0.45f + 0.55f * tipT;
                float rot0 = -MathHelper.PiOver2 + wobble * t0;
                Vector2 segPos = vent + rot0.ToRotationVector2() * (t0 * colLen) - Main.screenPosition;
                Vector2 segScale = new(segLen / line.Width * 1.05f, 0f);

                segScale.Y = 3.4f * widthSeg;
                Main.EntitySpriteDraw(line, segPos, null, outer * (0.55f * envSeg),
                    rot0, new Vector2(0, line.Height / 2f), segScale, SpriteEffects.None, 0);
                segScale.Y = 5.2f * widthSeg;
                Main.EntitySpriteDraw(line, segPos, null, outer * (0.35f * envSeg),
                    rot0 - wobble * 0.5f, new Vector2(0, line.Height / 2f), segScale, SpriteEffects.None, 0);
                segScale.Y = 1.3f * widthSeg;
                Main.EntitySpriteDraw(line, segPos, null, core * (0.75f * envSeg * tipT),
                    rot0, new Vector2(0, line.Height / 2f), segScale, SpriteEffects.None, 0);
            }
        }
    }
}
