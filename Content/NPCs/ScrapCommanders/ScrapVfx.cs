using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.ScrapCommanders
{
    /// <summary>
    /// 废钢统帅的视觉工具箱：BeamLine 射线绘制 + 全事件粒子配方。
    /// 粒子配方对应审查定下的分配表——坠地=重剥落+烟+冲击环、爆炸=机械爆炸+热烬、
    /// 枪口=曳光+火花、命中=火花+剥落。全部自带 dedServ 门
    /// </summary>
    internal static class ScrapVfx
    {
        internal static readonly Vector3 BeamCoreWarm = new(1f, 0.92f, 0.76f);
        internal static readonly Vector3 BeamEdgeRust = new(1f, 0.4f, 0.16f);
        internal static readonly Vector3 BeamEdgeRed = new(1f, 0.24f, 0.16f);

        //==================== BeamLine 批与绘制 ====================

        /// <summary>切入射线批（Immediate + Additive + 绑噪声）；配对 EndBeamBatch</summary>
        internal static void BeginBeamBatch(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                Main.instance.GraphicsDevice.Textures[1] = noise;
                Main.instance.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            }
        }

        /// <summary>回默认弹幕/NPC 批</summary>
        internal static void EndBeamBatch(SpriteBatch sb) {
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>画一条 BeamLine（须在 BeginBeamBatch 批内）。hot 驱动亮度，dash&gt;0 为预警虚线</summary>
        internal static void DrawBeam(SpriteBatch sb, Vector2 start, Vector2 end, float width,
            float hot, float dash, float seed, Vector3 core, Vector3 edge,
            float fadeHead = 0.08f, float fadeTail = 0.3f, float alpha = 1f) {
            Effect beam = EffectLoader.ScrapBeamLine?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (beam == null || pixel == null) {
                return;
            }
            Vector2 to = end - start;
            float len = to.Length();
            if (len < 4f) {
                return;
            }

            beam.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            beam.Parameters["uSeed"]?.SetValue(seed);
            beam.Parameters["uHot"]?.SetValue(MathHelper.Clamp(hot, 0f, 1f));
            beam.Parameters["uDash"]?.SetValue(MathHelper.Clamp(dash, 0f, 1f));
            beam.Parameters["uAspect"]?.SetValue(len / width);
            beam.Parameters["uFadeHead"]?.SetValue(fadeHead);
            beam.Parameters["uFadeTail"]?.SetValue(fadeTail);
            beam.Parameters["uCoreColor"]?.SetValue(core);
            beam.Parameters["uEdgeColor"]?.SetValue(edge);
            beam.CurrentTechnique.Passes[0].Apply();

            sb.Draw(pixel, start + to * 0.5f - Main.screenPosition, null, Color.White * alpha,
                to.ToRotation(), pixel.Size() * 0.5f,
                new Vector2(len, width) / pixel.Size(), SpriteEffects.None, 0f);
        }

        /// <summary>就近震屏：只震看得见战斗的本地玩家，带距离衰减门（状态基类与弹幕侧共用）</summary>
        internal static void ShakeNearby(Vector2 pos, float amount, float range = 1300f) {
            if (Main.dedServ || Main.LocalPlayer == null) {
                return;
            }
            if (Vector2.Distance(Main.LocalPlayer.Center, pos) > range) {
                return;
            }
            Main.LocalPlayer.CWR()?.GetScreenShake(amount);
        }

        //==================== 粒子配方 ====================

        /// <summary>坠地重砸：重剥落弹跳 + 尘土 + 烟 + 贴地冲击环</summary>
        internal static void GroundSlam(Vector2 pos, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < (int)(5 * scale); i++) {
                PRTLoader.NewParticle<PRT_SHPCHeavySpall>(pos + Main.rand.NextVector2Circular(16f, 6f),
                    new Vector2(Main.rand.NextFloat(-3.4f, 3.4f), -Main.rand.NextFloat(2f, 6f)) * scale,
                    ScrapCommander.WeldOrange, Main.rand.NextFloat(0.5f, 0.9f) * scale)
                    ?.Configure(new Color(96, 64, 46), Main.rand.Next(22, 40), 0.3f);
            }
            for (int i = 0; i < 10; i++) {
                Dust dust = Dust.NewDustPerfect(pos + new Vector2(Main.rand.NextFloat(-24f, 24f) * scale, -2f),
                    DustID.Dirt, new Vector2(Main.rand.NextFloat(-2.6f, 2.6f), -Main.rand.NextFloat(1.5f, 5f)) * scale,
                    70, default, Main.rand.NextFloat(1f, 1.6f) * scale);
                dust.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Smoke>(pos + new Vector2(0f, -8f),
                new Vector2(0f, -0.6f), ScrapCommander.SmokeGray, 0.9f * scale)
                ?.Configure(Main.rand.Next(40, 60), 0.55f, Main.rand.NextFloat(-0.01f, 0.01f));
            var wave = PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero,
                ScrapCommander.WeldOrange * 0.7f, 0.08f * scale);
            wave?.Configure(new Vector2(1f, 0.36f), 0f, 0.3f * scale, 10);
        }

        /// <summary>机械爆炸：爆芯 + 热烬喷发 + 冲击环 + 火花</summary>
        internal static void MetalExplosion(Vector2 pos, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_MechExplosion>(pos, Vector2.Zero,
                new Color(255, 120, 40), Main.rand.NextFloat(0.8f, 1.05f) * scale)
                ?.Configure(Main.rand.Next(22, 30), new Color(255, 176, 90));
            for (int i = 0; i < (int)(6 * scale); i++) {
                PRTLoader.NewParticle<PRT_SHPCThermalEmber>(pos + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 7f) * scale,
                    ScrapCommander.WeldOrange, Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(new Color(120, 46, 26), Main.rand.Next(26, 44));
            }
            for (int i = 0; i < (int)(6 * scale); i++) {
                PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 10f) * scale,
                    Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(true, Main.rand.Next(12, 20));
            }
            var wave = PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero,
                ScrapCommander.WeldOrange * 0.8f, 0.1f * scale);
            wave?.Configure(Vector2.One, 0f, 0.42f * scale, 12);
        }

        /// <summary>枪口拍：短曳光 + 锥形火花 + 一口烟</summary>
        internal static void MuzzleFlash(Vector2 pos, Vector2 dir, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_PallbearerTracer>(pos, Vector2.Zero,
                ScrapCommander.WeldOrange, 1f)
                ?.Configure(pos, pos + dir * 46f * scale, 5f * scale, 10);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    dir.RotatedBy(Main.rand.NextFloat(-0.35f, 0.35f)) * Main.rand.NextFloat(4f, 9f) * scale,
                    Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.4f)),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(8, 14));
            }
            PRTLoader.NewParticle<PRT_Smoke>(pos + dir * 10f, dir * 1.4f,
                ScrapCommander.SmokeGray * 0.9f, 0.5f * scale)
                ?.Configure(Main.rand.Next(24, 36), 0.5f, Main.rand.NextFloat(-0.02f, 0.02f));
        }

        /// <summary>金属受击：火花 + 一两片剥落</summary>
        internal static void HitSparks(Vector2 pos, Vector2 dir, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(pos + Main.rand.NextVector2Circular(10f, 10f),
                    dir.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(2.5f, 6f) * scale,
                    Color.Lerp(ScrapCommander.WeldOrange, Color.White, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(true, Main.rand.Next(9, 15));
            }
            PRTLoader.NewParticle<PRT_SHPCHeavySpall>(pos, dir * Main.rand.NextFloat(1.5f, 3.5f)
                + new Vector2(0f, -2f), ScrapCommander.WeldOrange, Main.rand.NextFloat(0.4f, 0.6f) * scale)
                ?.Configure(new Color(96, 64, 46), Main.rand.Next(18, 30), 0.3f);
        }

        /// <summary>速度线：冲撞/突刺身后的短曳光帧</summary>
        internal static void SpeedStreak(Vector2 pos, Vector2 vel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 back = -vel.SafeNormalize(Vector2.UnitX);
            Vector2 from = pos + Main.rand.NextVector2Circular(20f, 20f);
            PRTLoader.NewParticle<PRT_PallbearerTracer>(from, Vector2.Zero,
                ScrapCommander.WeldOrange * 0.55f, 1f)
                ?.Configure(from, from + back * Main.rand.NextFloat(40f, 90f), 3f, 8);
        }
    }
}
