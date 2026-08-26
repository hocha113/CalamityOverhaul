using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalSkeletron.Rendering;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Skeletron
{
    /// <summary>
    /// 缚魂之腕演出层：诅咒领域边界纱幕 + 魂环鬼火 + 收魂/格挡爆点 + 满环凝聚涡流。<br/>
    /// 暗纱与冷焰入队走 <see cref="RenderHandle.DrawAfterTiles"/>（物块后实体前，暗层压底，
    /// 冷焰同帧被 1.0925 的 <see cref="SkeletronFlameRender"/> 批消费）；
    /// 拖尾绸带/爆点环/涡流走 EndEntityDraw 压在实体之上。
    /// 领域与魂环对所有装备者绘制（装备状态经原版同步，魂数镜像由信道喂）
    /// </summary>
    internal sealed class SoulbindingArmRender : RenderHandle
    {
        /// <summary>认领表槽位 1.76（骷髅王本体屏效在 1.092，互不相扰）</summary>
        public override float Weight => 1.76f;

        #region 视觉队列（纯本端演出，每客户端一份，不跨网络——合法 static，同冷焰批先例）
        private struct RingPop
        {
            public Vector2 Pos;
            public long StartTick;
            public float Scale;
        }
        private static readonly List<RingPop> pops = [];
        private const int PopFrames = 16;

        private static readonly Vector2[] ribbonPts = new Vector2[8];
        private static readonly Vector2[] streakPts = new Vector2[5];
        #endregion

        #region 对外演出入口
        /// <summary>登记一圈冲击环（格挡 1.0 / 掌攫 1.8）</summary>
        internal static void AddPop(Vector2 pos, float scale) {
            if (Main.dedServ) {
                return;
            }
            pops.Add(new RingPop { Pos = pos, StartTick = Main.GameUpdateCount, Scale = scale });
        }

        /// <summary>格挡爆点：魂魄替身而灭的一拍（拥有者与转播端共用）</summary>
        internal static void BlockPopFx(Vector2 pos) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit36 with { Volume = 0.6f, Pitch = 0.35f }, pos);
            for (int i = 0; i < 10; i++) {
                float ang = MathHelper.TwoPi * i / 10f + Main.rand.NextFloat(0.3f);
                PRTLoader.NewParticle<PRT_SkeleGhostFlame>(pos, ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5f),
                    Main.rand.NextBool() ? SkeletronRenderHelper.GhostCyan : SkeletronRenderHelper.GhostDeep,
                    Main.rand.NextFloat(0.9f, 1.5f))?.Configure(Main.rand.Next(16, 28));
            }
            AddPop(pos, 1f);
        }

        /// <summary>收魂演出：尸位飞出吸入魂环；满环时补一记集满拍（拥有者与转播端共用）</summary>
        internal static void GainFx(SoulbindingArmPlayer mp, Vector2 from) {
            if (Main.dedServ || mp == null) {
                return;
            }
            mp.Streaks.Add(new SoulbindingArmPlayer.SoulStreak { From = from, StartTick = Main.GameUpdateCount });
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = 0.5f }, from);

            if (mp.SoulCount >= SoulbindingArmPlayer.MaxSouls) {
                Vector2 center = mp.Player.Center;
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = 0.1f }, center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.6f, Pitch = -0.3f }, center);
                for (int i = 0; i < 12; i++) {
                    float ang = MathHelper.TwoPi * i / 12f;
                    PRTLoader.NewParticle<PRT_SkeleGhostFlame>(
                        center + ang.ToRotationVector2() * SoulbindingArmPlayer.RingRadius,
                        ang.ToRotationVector2() * 2.2f, SkeletronRenderHelper.GhostCyan,
                        Main.rand.NextFloat(1f, 1.6f))?.Configure(Main.rand.Next(18, 30));
                }
            }
        }
        #endregion

        #region 几何
        /// <summary>魂位：phaseBack 沿轨道向后回溯（拖尾采样用），bob 用相位驱动（暂停即冻结）</summary>
        private static Vector2 SoulPos(Player plr, SoulbindingArmPlayer mp, int index, float phaseBack, out float tangentAngle) {
            float ang = mp.SpinPhase - phaseBack + index * (MathHelper.TwoPi / SoulbindingArmPlayer.MaxSouls);
            float radius = SoulbindingArmPlayer.RingRadius * (1f - 0.62f * mp.VisualConverge)
                + MathF.Sin(mp.SpinPhase * 2.3f + index * 1.7f) * 5f;
            tangentAngle = ang + MathHelper.PiOver2;
            return plr.Center + ang.ToRotationVector2() * radius;
        }

        /// <summary>收魂飞行位置：尸位→玩家的吸入弧（二次贝塞尔 + 加速吸入缓动）</summary>
        private static Vector2 StreakPos(Vector2 from, Player plr, float t) {
            float eased = MathF.Pow(MathHelper.Clamp(t, 0f, 1f), 1.55f);
            Vector2 to = plr.Center;
            Vector2 mid = (from + to) * 0.5f
                + (to - from).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * 60f;
            float u = 1f - eased;
            return u * u * from + 2f * u * eased * mid + eased * eased * to;
        }

        private static bool DomainOnScreen(Player plr) {
            float pad = SoulbindingArmPlayer.DomainRadius + 200f;
            Vector2 screen = Main.screenPosition;
            return plr.Center.X + pad >= screen.X && plr.Center.X - pad <= screen.X + Main.screenWidth
                && plr.Center.Y + pad >= screen.Y && plr.Center.Y - pad <= screen.Y + Main.screenHeight;
        }
        #endregion

        #region 暗纱层与冷焰入队（物块后、实体前）
        public override void DrawAfterTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }

            bool begun = false;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player plr = Main.player[i];
                if (plr == null || !plr.active || plr.dead
                    || !plr.TryGetModPlayer(out SoulbindingArmPlayer mp) || !mp.DomainActive) {
                    continue;
                }

                AdvanceVisuals(mp);

                if (!DomainOnScreen(plr)) {
                    continue;
                }
                if (!begun) {
                    begun = true;
                    spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                        DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                }
                DrawDomainGauze(spriteBatch, plr);
                PushBoundaryFlames(plr);
                PushSoulRing(plr, mp);
                PushStreakFlames(plr, mp);
            }

            if (begun) {
                spriteBatch.End();
            }
        }

        /// <summary>每帧一次的视觉推进（本钩子每帧只触发一次；暂停冻结）</summary>
        private static void AdvanceVisuals(SoulbindingArmPlayer mp) {
            //收魂演出过期清理（寿命走 GameUpdateCount，暂停自然冻结）
            for (int k = mp.Streaks.Count - 1; k >= 0; k--) {
                if (Main.GameUpdateCount - mp.Streaks[k].StartTick > SoulbindingArmPlayer.StreakFrames) {
                    mp.Streaks.RemoveAt(k);
                }
            }
            if (Main.gamePaused) {
                return;
            }
            float target = mp.SoulCount >= SoulbindingArmPlayer.MaxSouls ? 1f : 0f;
            mp.VisualConverge = target > mp.VisualConverge
                ? MathF.Min(mp.VisualConverge + 0.045f, target)
                : MathF.Max(mp.VisualConverge - 0.045f, target);
            mp.SpinPhase += 0.030f + 0.055f * mp.VisualConverge;
        }

        /// <summary>
        /// 领域边界咒焰纱幕：暗层必须真 alpha 图（Extra_98），加色与 A=0 画不出暗；
        /// 幽青微光走同一预乘批的 A=0 加色技法
        /// </summary>
        private static void DrawDomainGauze(SpriteBatch spriteBatch, Player plr) {
            Texture2D gauze = CWRAsset.Extra_98?.Value;
            if (gauze == null) {
                return;
            }
            float t = Main.GlobalTimeWrappedHourly;
            Vector2 origin = gauze.Size() / 2f;

            //主暗纱带：切向纺锤片层叠成环，半径低频呼吸
            for (int i = 0; i < 26; i++) {
                float ang = MathHelper.TwoPi * i / 26f + t * 0.05f;
                float r = SoulbindingArmPlayer.DomainRadius + MathF.Sin(t * 1.3f + i * 2.13f) * 7f;
                Vector2 pos = plr.Center + ang.ToRotationVector2() * r - Main.screenPosition;
                float alpha = 0.40f + 0.10f * MathF.Sin(t * 2.1f + i);
                spriteBatch.Draw(gauze, pos, null, SkeletronRenderHelper.CurseDark * alpha,
                    ang + MathHelper.PiOver2, origin, new Vector2(1.9f, 0.85f), SpriteEffects.None, 0f);
            }
            //外圈咒紫淡纱：反向缓移出层次
            for (int i = 0; i < 14; i++) {
                float ang = MathHelper.TwoPi * i / 14f - t * 0.03f;
                Vector2 pos = plr.Center + ang.ToRotationVector2() * (SoulbindingArmPlayer.DomainRadius + 18f)
                    - Main.screenPosition;
                Color col = Color.Lerp(SkeletronRenderHelper.CurseDark, SkeletronRenderHelper.CurseViolet, 0.4f) * 0.20f;
                spriteBatch.Draw(gauze, pos, null, col, ang + MathHelper.PiOver2, origin,
                    new Vector2(1.6f, 0.7f), SpriteEffects.None, 0f);
            }
            //稀疏幽青掠光（预乘批 A=0 纯加色）
            for (int i = 0; i < 6; i++) {
                float ang = MathHelper.TwoPi * i / 6f + t * 0.11f;
                Vector2 pos = plr.Center + ang.ToRotationVector2() * SoulbindingArmPlayer.DomainRadius
                    - Main.screenPosition;
                float glint = 0.22f + 0.10f * MathF.Sin(t * 3.1f + i * 2.4f);
                spriteBatch.Draw(gauze, pos, null,
                    SkeletronRenderHelper.AsAdditive(SkeletronRenderHelper.GhostCyan) * glint,
                    ang + MathHelper.PiOver2, origin, new Vector2(0.9f, 0.5f), SpriteEffects.None, 0f);
            }
        }

        /// <summary>边界舔焰：低亮冷焰沿环外张，诅咒紫混比偏高（将熄之火）</summary>
        private static void PushBoundaryFlames(Player plr) {
            float t = Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < 18; i++) {
                float ang = MathHelper.TwoPi * i / 18f + t * 0.07f;
                Vector2 root = plr.Center + ang.ToRotationVector2() * (SoulbindingArmPlayer.DomainRadius - 6f);
                float sway = MathF.Sin(t * (2.1f + (i % 3) * 0.6f) + i * 2.3f) * 0.14f;
                float flick = 0.75f + 0.25f * MathF.Sin(t * (7f + (i % 4) * 1.5f) + i * 2.9f);
                SkeletronFlameRender.Push(root, ang + sway,
                    new Vector2(13f, (24f + 10f * flick)),
                    0.3f, i * 0.173f + plr.whoAmI * 0.31f, 0.6f, 0.5f);
            }
        }

        /// <summary>魂环鬼火：外鞘 + 骨白内芯双层，焰轴顺运动方向（旋转即拉丝）</summary>
        private static void PushSoulRing(Player plr, SoulbindingArmPlayer mp) {
            int count = Math.Clamp(mp.SoulCount, 0, SoulbindingArmPlayer.MaxSouls);
            for (int i = 0; i < count; i++) {
                Vector2 pos = SoulPos(plr, mp, i, 0f, out float tangent);
                float flick = 0.85f + 0.15f * MathF.Sin(mp.SpinPhase * 3f + i * 2.1f);
                SkeletronFlameRender.Push(pos, tangent,
                    new Vector2(12f, (24f + 6f * flick) * (1f + 0.4f * mp.VisualConverge)),
                    0.5f + 0.35f * mp.VisualConverge, i * 0.137f + plr.whoAmI * 0.29f, 0.25f, 0.85f);
                SkeletronFlameRender.Push(pos, tangent,
                    new Vector2(6f, 13f), 0.95f, i * 0.137f + 0.41f, 0.05f, 0.8f);
            }
        }

        /// <summary>收魂在途的火体（速度拉伸沿弧线切向）</summary>
        private static void PushStreakFlames(Player plr, SoulbindingArmPlayer mp) {
            foreach (SoulbindingArmPlayer.SoulStreak streak in mp.Streaks) {
                float t = (Main.GameUpdateCount - streak.StartTick) / (float)SoulbindingArmPlayer.StreakFrames;
                Vector2 pos = StreakPos(streak.From, plr, t);
                Vector2 dir = (StreakPos(streak.From, plr, t + 0.04f) - pos).SafeNormalize(Vector2.UnitX);
                SkeletronFlameRender.Push(pos - dir * 10f, dir.ToRotation(),
                    new Vector2(11f, 26f), 0.7f, streak.From.X * 0.001f % 1f, 0.2f, 0.9f);
            }
        }
        #endregion

        #region 拖尾绸带 / 爆点环 / 凝聚涡流（实体之上）
        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }

            bool anyVortex = false;
            //绸带自管设备状态，无需活动批
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player plr = Main.player[i];
                if (plr == null || !plr.active || plr.dead
                    || !plr.TryGetModPlayer(out SoulbindingArmPlayer mp) || !mp.DomainActive) {
                    continue;
                }
                if (!DomainOnScreen(plr)) {
                    continue;
                }
                DrawSoulRibbons(plr, mp);
                DrawStreakRibbons(plr, mp);
                if (mp.VisualConverge > 0.05f) {
                    anyVortex = true;
                }
            }

            PrunePops();
            if (pops.Count == 0 && !anyVortex) {
                return;
            }

            //爆点环与涡流需要活动实体批（两者内部各自 End/Begin，参数与本批同构）
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            DrawPops(spriteBatch);
            if (anyVortex) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player plr = Main.player[i];
                    if (plr == null || !plr.active || plr.dead
                        || !plr.TryGetModPlayer(out SoulbindingArmPlayer mp)
                        || !mp.DomainActive || mp.VisualConverge <= 0.05f || !DomainOnScreen(plr)) {
                        continue;
                    }
                    SkeletronRenderHelper.DrawSpinVortex(spriteBatch, plr.Center, mp.SpinPhase * 2f,
                        0.85f * mp.VisualConverge, mp.VisualConverge, 240f);
                }
            }
            spriteBatch.End();
        }

        /// <summary>魂魄拖尾：沿轨道向后回溯采样的灵息绸带（青绿鬼火拖尾环绕体）</summary>
        private static void DrawSoulRibbons(Player plr, SoulbindingArmPlayer mp) {
            int count = Math.Clamp(mp.SoulCount, 0, SoulbindingArmPlayer.MaxSouls);
            for (int i = 0; i < count; i++) {
                for (int k = 0; k < 7; k++) {
                    //uv.x=0 是尾端：先采最旧的回溯点
                    ribbonPts[k] = SoulPos(plr, mp, i, (6 - k) * 0.09f, out _);
                }
                SkeletronRenderHelper.DrawSpecterRibbon(ribbonPts, 7, 1.5f, 6.5f,
                    0.5f + 0.3f * mp.VisualConverge, 0.5f,
                    i * 0.137f + plr.whoAmI * 0.29f, 0.3f, 0.15f, 2.2f);
            }
        }

        /// <summary>收魂在途的拖尾绸带</summary>
        private static void DrawStreakRibbons(Player plr, SoulbindingArmPlayer mp) {
            foreach (SoulbindingArmPlayer.SoulStreak streak in mp.Streaks) {
                float t = (Main.GameUpdateCount - streak.StartTick) / (float)SoulbindingArmPlayer.StreakFrames;
                for (int k = 0; k < 5; k++) {
                    streakPts[k] = StreakPos(streak.From, plr, t - (4 - k) * 0.05f);
                }
                SkeletronRenderHelper.DrawSpecterRibbon(streakPts, 5, 1f, 5f,
                    0.7f, 0.7f, streak.From.X * 0.001f % 1f, 0.25f, 0.1f, 2.6f);
            }
        }

        private static void PrunePops() {
            for (int i = pops.Count - 1; i >= 0; i--) {
                if (Main.GameUpdateCount - pops[i].StartTick > PopFrames) {
                    pops.RemoveAt(i);
                }
            }
        }

        private static void DrawPops(SpriteBatch spriteBatch) {
            foreach (RingPop pop in pops) {
                float t = MathHelper.Clamp((Main.GameUpdateCount - pop.StartTick) / (float)PopFrames, 0f, 1f);
                float easeOut = 1f - MathF.Pow(1f - t, 2.4f);
                ShockRingDraw.Draw(spriteBatch, pop.Pos,
                    MathHelper.Lerp(18f, 64f, easeOut) * pop.Scale, 10f * pop.Scale,
                    SkeletronRenderHelper.BonePale, SkeletronRenderHelper.GhostCyan, SkeletronRenderHelper.GhostDeep,
                    (1f - t) * 0.85f, squish: 1f, innerGlow: 0.35f, timeSeed: pop.Pos.X * 0.001f);
            }
        }
        #endregion
    }
}
