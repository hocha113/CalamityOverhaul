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
            //柱体：核心亮线+外层宽柔光，喷发起立/塌落收缩
            float erupt = EruptProgress;
            float collapse = Timer > DelayFrames + EruptTime
                ? 1f - MathHelper.Clamp((Timer - DelayFrames - EruptTime) / CollapseTime, 0f, 1f) : 1f;
            float env = erupt * collapse;
            if (env <= 0.01f) {
                return false;
            }

            Texture2D line = TextureAssets.Projectile[Type].Value;
            Vector2 vent = new(Projectile.Center.X, Projectile.position.Y + Projectile.height);
            Vector2 drawPos = vent - Main.screenPosition;
            float len = ColumnHeight * env / line.Width;
            Vector2 origin = new(0, line.Height / 2f);
            float wobble = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 22f + Projectile.whoAmI) * 0.06f;

            Color outer = new(FishronMotionFX.SeaGreen.R, FishronMotionFX.SeaGreen.G, FishronMotionFX.SeaGreen.B, 0);
            Color core = new(FishronMotionFX.FoamWhite.R, FishronMotionFX.FoamWhite.G, FishronMotionFX.FoamWhite.B, 0);
            Main.EntitySpriteDraw(line, drawPos, null, outer * (0.55f * env),
                -MathHelper.PiOver2 + wobble, origin, new Vector2(len, 3.4f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, outer * (0.4f * env),
                -MathHelper.PiOver2 - wobble * 0.7f, origin, new Vector2(len * 0.96f, 5.2f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(line, drawPos, null, core * (0.75f * env),
                -MathHelper.PiOver2 + wobble * 0.4f, origin, new Vector2(len, 1.3f), SpriteEffects.None, 0);
            return false;
        }
    }
}
