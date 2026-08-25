using CalamityOverhaul.Content.Scenarios.Dungeonworld.Fog;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.Ambience
{
    /// <summary>
    /// 空气签名的屏幕层绘制：尘埃光锥（亮灯下垂的窄梯形光柱，3 段叠绘收口）
    /// 与深渊上升气流光丝。挂 EndEntityDraw：光柱要盖在敌人/玩家之上（人走进光柱里），
    /// 前景瘴气滤镜在 EndCapture 阶段仍压其上，层序正确。自开自收加色批，无 RT 槽
    /// </summary>
    internal sealed class DungeonworldAmbientRender : RenderHandle
    {
        /// <summary>权重 1.17（全仓查重空闲；邻位 1.16=月总黑闪前置/骇入链路，1.2=Warp/弹幕层）</summary>
        public override float Weight => 1.17f;

        //LightBeam 256x1024 真alpha 竖梁（2026-08-25 实测：ext 0.76x0.89 → 内容约 195x911）
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> LightBeam = null;
        //SpeedLines01 1024 黑底横速度线：随机截条转竖，加色批
        [VaultLoaden(CWRConstant.Masking)]
        private static Asset<Texture2D> SpeedLines01 = null;

        private const float BeamContentW = 195f;
        private const float BeamContentH = 911f;
        //深渊气流光丝出现的行界（L7 下缘，深渊带 5600 起）
        private const int AbyssThreadRow = 5560;
        private const int MaxThreads = 8;

        private static readonly Color CandleWarm = new(233, 185, 102);
        private static readonly Color ThreadCold = new(150, 140, 190);

        private struct Thread
        {
            internal bool Active;
            internal Vector2 Pos;
            internal float Speed;
            internal int Life;
            internal int MaxLife;
            internal int SrcY;
            internal float Width;
            internal float LenScale;
        }

        private static readonly Thread[] threads = new Thread[MaxThreads];
        private static int threadSpawnIn;

        //==================== 逻辑更新（气流光丝）====================

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.gamePaused) {
                return;
            }
            float presence = DungeonworldAmbientFX.Presence;
            if (presence < 0.02f) {
                for (int i = 0; i < threads.Length; i++) {
                    threads[i].Active = false;
                }
                return;
            }

            //推进在途光丝
            for (int i = 0; i < threads.Length; i++) {
                if (!threads[i].Active) {
                    continue;
                }
                threads[i].Pos.Y -= threads[i].Speed;
                threads[i].Life++;
                if (threads[i].Life >= threads[i].MaxLife
                    || threads[i].Pos.Y < Main.screenPosition.Y - 120f) {
                    threads[i].Active = false;
                }
            }

            //只在 L7 下缘/深渊带补充
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.Center.Y / 16f < AbyssThreadRow) {
                return;
            }
            if (--threadSpawnIn > 0) {
                return;
            }
            threadSpawnIn = Main.rand.Next(18, 32);
            for (int i = 0; i < threads.Length; i++) {
                if (threads[i].Active) {
                    continue;
                }
                threads[i] = new Thread {
                    Active = true,
                    Pos = new Vector2(
                        Main.screenPosition.X + Main.rand.NextFloat(Main.screenWidth),
                        Main.screenPosition.Y + Main.screenHeight + Main.rand.NextFloat(0f, 60f)),
                    Speed = Main.rand.NextFloat(2.2f, 4.5f),
                    Life = 0,
                    MaxLife = Main.rand.Next(90, 140),
                    SrcY = Main.rand.Next(0, 1010),
                    Width = Main.rand.NextFloat(0.6f, 1f),
                    LenScale = Main.rand.NextFloat(0.10f, 0.15f)
                };
                return;
            }
        }

        //==================== 绘制 ====================

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main
            , GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            float presence = DungeonworldAmbientFX.Presence;
            if (presence < 0.02f) {
                return;
            }

            bool anyShaft = false;
            if (!DungeonworldAmbientFX.DisableShafts) {
                var shafts = DungeonworldAmbientFX.Shafts;
                for (int i = 0; i < shafts.Length; i++) {
                    if (shafts[i].Active) {
                        anyShaft = true;
                        break;
                    }
                }
            }
            bool anyThread = false;
            for (int i = 0; i < threads.Length; i++) {
                if (threads[i].Active) {
                    anyThread = true;
                    break;
                }
            }
            if (!anyShaft && !anyThread) {
                return;
            }

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            if (anyShaft) {
                DrawShafts(spriteBatch, presence);
            }
            if (anyThread) {
                DrawThreads(spriteBatch, presence);
            }
            spriteBatch.End();
        }

        //光柱：3 段叠绘做长度渐隐（防"灰度贴图整条拉伸两端硬切"），宽度沿柱身微张成梯形
        private static void DrawShafts(SpriteBatch sb, float presence) {
            Texture2D beam = LightBeam?.Value;
            if (beam == null || beam.IsDisposed) {
                return;
            }
            //段长比 / 段透明度阶梯 / 段宽度扩张
            ReadOnlySpan<float> segFrac = [0.42f, 0.34f, 0.24f];
            ReadOnlySpan<float> segAlpha = [1f, 0.55f, 0.28f];
            ReadOnlySpan<float> segWide = [1f, 1.12f, 1.25f];

            var shafts = DungeonworldAmbientFX.Shafts;
            for (int i = 0; i < shafts.Length; i++) {
                if (!shafts[i].Active) {
                    continue;
                }
                Vector2 mid = shafts[i].TopPx + new Vector2(0f, shafts[i].LengthPx * 0.5f);
                //雾越浓光柱越有形；无雾层保底 0.45（否则 L1/L3 主场光柱直接消失）
                float fogK = 0.45f + 0.55f * MathHelper.Clamp(DungeonworldFogSim.DensityAt(mid), 0f, 1f);
                float alpha = (0.05f + 0.11f * MathHelper.Clamp(shafts[i].Bright, 0f, 1f)) * fogK * presence;
                if (alpha < 0.004f) {
                    continue;
                }
                //轻微摆动：柱顶为轴
                float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * 1.1f + shafts[i].Phase) * 0.022f;
                Vector2 down = new(MathF.Sin(sway), MathF.Cos(sway));

                float cum = 0f;
                for (int s = 0; s < 3; s++) {
                    float segLen = shafts[i].LengthPx * segFrac[s];
                    Vector2 top = shafts[i].TopPx + down * cum - Main.screenPosition;
                    Vector2 scale = new(
                        shafts[i].WidthPx * segWide[s] / BeamContentW,
                        segLen / BeamContentH);
                    //加色批染色：A 随强度走
                    sb.Draw(beam, top, null, CandleWarm * (alpha * segAlpha[s]), sway,
                        new Vector2(beam.Width * 0.5f, 0f), scale, SpriteEffects.None, 0f);
                    //8% 重叠防段间露缝
                    cum += segLen * 0.92f;
                }
            }
        }

        //深渊气流光丝：横速度线随机截条转竖，短寿命淡入淡出。
        //SpeedLines01 实测 ext_w=1.00（长轴零端部衰减）——整条拉伸=两端一刀切（VFX.md 禁令），
        //按截条三段透明度阶梯收口：暗-亮-暗
        private static void DrawThreads(SpriteBatch sb, float presence) {
            Texture2D lines = SpeedLines01?.Value;
            if (lines == null || lines.IsDisposed) {
                return;
            }
            //三段源截条（沿长轴）与端部收口透明度
            ReadOnlySpan<int> segX = [0, 307, 717];
            ReadOnlySpan<int> segW = [307, 410, 307];
            ReadOnlySpan<float> segA = [0.35f, 1f, 0.35f];

            for (int i = 0; i < threads.Length; i++) {
                if (!threads[i].Active) {
                    continue;
                }
                float t = threads[i].Life / (float)threads[i].MaxLife;
                float env = Math.Min(t / 0.2f, 1f) * MathHelper.Clamp((1f - t) / 0.4f, 0f, 1f);
                float alpha = 0.16f * env * presence;
                if (alpha < 0.004f) {
                    continue;
                }
                Vector2 basePos = threads[i].Pos - Main.screenPosition;
                for (int s = 0; s < 3; s++) {
                    var src = new Rectangle(segX[s], threads[i].SrcY, segW[s], 12);
                    //旋转 -PiOver2 后贴图 +X 轴映射到屏幕 -Y：按截条中心沿竖轴摆位
                    float axisOffset = (segX[s] + segW[s] * 0.5f - 512f) * threads[i].LenScale;
                    sb.Draw(lines, basePos + new Vector2(0f, -axisOffset), src,
                        ThreadCold * (alpha * segA[s]), -MathHelper.PiOver2,
                        new Vector2(segW[s] * 0.5f, 6f),
                        new Vector2(threads[i].LenScale, threads[i].Width),
                        SpriteEffects.None, 0f);
                }
            }
        }
    }
}
