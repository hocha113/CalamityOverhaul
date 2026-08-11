using CalamityOverhaul.Content.HackTimes.PvP.Protocols;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.PvP.UI
{
    /// <summary>
    /// PvP 骇入的世界坐标表现层（红线 / 穿墙标记 / 旁观者光环）。全 CPU 折线，无 shader。<br/>
    /// · 防守方视角：来袭上传 = 琥珀细虚弧（被追踪的低压感）；落地翻红爆闪，
    ///   90f 全强度后坍缩成防守方身边的方向残段（<b>不做效果全程的免费反向
    ///   wallhack</b>——想全程看穿攻击方去开链路回溯）；回溯激活 = 亮青主动追踪线
    ///   900f 全程 + 攻击方穿墙标记。<br/>
    /// · 攻击方视角：自己的上传 = 细琥珀虚线上行链（与防守方看到的是同一条线的两端）。<br/>
    /// · 坐标广播：defender 挂穿墙标记，只画给施加者及其非零同队。<br/>
    /// · 旁观者：被骇玩家身上低频故障粒子光环（密度 = 在册条数），不画线不显名。<br/>
    /// 红线是 HUD 隐喻不是物理光束——穿墙可见，但被实心物块遮挡的分段降到
    /// 35% 透明度 + 虚线化（每 4 段采一次 CanHitLine）；攻击方隐身时后 45% 段散成
    /// 噪声云（方向可读、精确位置不可读）
    /// </summary>
    internal class PlayerHackLinkRender : RenderHandle
    {
        public override float Weight => 1.16f;

        private const int LineSegments = 36;
        /// <summary>落地后红线保持全强度的帧数，之后坍缩为方向残段</summary>
        private const int FullLineFrames = 90;
        /// <summary>坍缩后的方向残段长度（px）</summary>
        private const float StubLength = 60f;
        private const float WaveAmplitude = 18f;

        private static readonly List<PlayerHackMirror.MirrorEffect> mirrorCache = [];

        public override void UpdateBySystem(int index) {
            if (Main.gameMenu || Main.dedServ) return;
            SpawnSpectatorHalos();
        }

        //旁观者光环：低频故障粒子，密度 = 在册条数；路人只看到"那个人被入侵了"
        private static void SpawnSpectatorHalos() {
            var all = PlayerHackMirror.All;
            for (int i = 0; i < all.Count; i++) {
                PlayerHackMirror.MirrorEffect fx = all[i];
                if (fx.RemovedReason != null
                    || fx.DefenderIndex == Main.myPlayer) continue;
                if (!Main.rand.NextBool(26)) continue;
                Player defender = Main.player[fx.DefenderIndex];
                if (defender?.active != true || defender.dead) continue;
                Vector2 pos = defender.Center + new Vector2(
                    Main.rand.NextFloat(-24f, 24f), Main.rand.NextFloat(-30f, 30f));
                PRTLoader.NewParticle<PRT_TBUGGlitch>(pos,
                    new Vector2(0f, Main.rand.NextFloat(-0.5f, -0.1f)), default,
                    Main.rand.NextFloat(0.4f, 0.8f))?.Configure(22);
            }
        }

        public override void EndEntityDraw(SpriteBatch spriteBatch, Main main) {
            if (Main.gameMenu || Main.dedServ) return;
            Player local = Main.LocalPlayer;
            if (local?.active != true) return;

            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive,
                SamplerState.PointWrap, DepthStencilState.None,
                RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);

            var ledger = local.GetModPlayer<PlayerHackLedger>();
            DrawDefenderLines(spriteBatch, local, ledger);
            DrawTracebackLines(spriteBatch, local, ledger);
            DrawAttackerUplinks(spriteBatch, local);
            DrawPositionCastMarkers(spriteBatch);

            spriteBatch.End();
        }

        #region 防守方红线（Notice = 上传期琥珀；Ledger = 落地红线生命周期）

        private static void DrawDefenderLines(SpriteBatch sb, Player local,
            PlayerHackLedger ledger) {
            Vector2 origin = local.Center - Main.screenPosition;

            //上传期：琥珀 1px 细虚弧，"正在被追踪"的低压感
            IReadOnlyList<PlayerHackNotice> notices = ledger.IncomingUploads;
            for (int i = 0; i < notices.Count; i++) {
                PlayerHackNotice notice = notices[i];
                if (notice.Terminal) continue;
                Player attacker = ResolveActive(notice.AttackerIndex);
                if (attacker == null) continue;
                DrawSerpentine(sb, origin, attacker.Center - Main.screenPosition,
                    seed: notice.RequestId, PvPTheme.Amber, thickness: 1f,
                    alpha: 0.55f, dashed: true, attacker,
                    flowSpeed: 0.35f);
            }

            //落地效果：翻红 → 90f 全强度 → 坍缩为方向残段
            IReadOnlyList<PlayerHackEffect> effects = ledger.ActiveEffects;
            for (int i = 0; i < effects.Count; i++) {
                PlayerHackEffect effect = effects[i];
                Player attacker = ResolveActive(effect.CasterIndex);
                if (attacker == null) {
                    //攻击方掉线：已落地效果不消失，端点降级为"信号丢失"结点
                    DrawSignalLostNode(sb, origin, effect);
                    continue;
                }
                Vector2 target = attacker.Center - Main.screenPosition;
                if (effect.Elapsed <= FullLineFrames) {
                    //落地瞬间粗化翻红 + 爆闪一次
                    float flash = effect.Elapsed < 6
                        ? 1f - effect.Elapsed / 6f : 0f;
                    float alpha = 0.85f + flash * 0.6f;
                    DrawSerpentine(sb, origin, target, seed: effect.ActivationId,
                        Color.Lerp(HackTheme.Danger, Color.White, flash * 0.7f),
                        thickness: 2f, alpha, dashed: false, attacker,
                        flowSpeed: 0.8f);
                }
                else {
                    //方向残段：只保留 60px 指向，精确位置不再免费
                    Vector2 dir = target - origin;
                    if (dir.LengthSquared() > 1f) {
                        dir.Normalize();
                        Vector2 stubEnd = origin + dir * StubLength;
                        HackTheme.DrawLine(sb, origin + dir * 14f, stubEnd, 2f,
                            HackTheme.Danger * 0.5f);
                        HackTheme.DrawLine(sb, origin + dir * 14f, stubEnd, 4f,
                            PvPTheme.HostileGlow * 0.18f);
                        //残段端头的呼吸菱形
                        float pulse = 0.6f + 0.4f * MathF.Sin(
                            Main.GameUpdateCount * 0.12f + effect.ActivationId % 7);
                        HackTheme.DrawDiamondOutline(sb, stubEnd, 5f * pulse, 1f,
                            HackTheme.Danger * 0.6f);
                    }
                }
            }
        }

        private static void DrawSignalLostNode(SpriteBatch sb, Vector2 origin,
            PlayerHackEffect effect) {
            float pulse = 0.5f + 0.5f * MathF.Sin(Main.GameUpdateCount * 0.08f);
            Vector2 node = origin + new Vector2(0f, -56f);
            HackTheme.DrawDiamondOutline(sb, node, 6f, 1f,
                HackTheme.TextDim * (0.4f + pulse * 0.3f));
            HackTheme.DrawRawText(sb, PvPHudText.SignalLost.Value,
                node + new Vector2(10f, -8f), HackTheme.TextDim * 0.7f, 0.55f);
        }

        #endregion

        #region 链路回溯：防守方翻转主动的亮青追踪线 + 穿墙标记

        private static void DrawTracebackLines(SpriteBatch sb, Player local,
            PlayerHackLedger ledger) {
            IReadOnlyList<PlayerHackMarker> markers = ledger.TracebackMarkers;
            if (markers.Count == 0) return;
            Vector2 origin = local.Center - Main.screenPosition;

            for (int i = 0; i < markers.Count; i++) {
                PlayerHackMarker marker = markers[i];
                Player attacker = ResolveActive(marker.AttackerIndex);
                if (attacker == null) continue;
                Vector2 target = attacker.Center - Main.screenPosition;
                float fade = MathHelper.Clamp(marker.FramesLeft / 60f, 0f, 1f);

                //主动追踪线：无视隐身降级（这就是它 2 RAM 卖的东西）
                DrawSerpentine(sb, origin, target, seed: marker.AttackerIndex * 977L,
                    PvPTheme.TraceCyan, thickness: 1.6f, alpha: 0.8f * fade,
                    dashed: false, invisOverride: false, attacker, flowSpeed: -0.6f);
                DrawWallhackMarker(sb, attacker, PvPTheme.TraceCyan, fade,
                    marker.AttackerName);
            }
        }

        #endregion

        #region 攻击方上行链（自己的上传，与防守方红线是同一条线的两端视角）

        private static void DrawAttackerUplinks(SpriteBatch sb, Player local) {
            var queue = HackTimeUI.Instance?.Queue;
            if (queue == null || queue.IsEmpty) return;
            Vector2 origin = local.Center - Main.screenPosition;

            for (int i = 0; i < Main.maxPlayers; i++) {
                if (i == Main.myPlayer) continue;
                Player target = ResolveActive(i);
                if (target == null) continue;
                var probe = new PlayerScannable(i);
                if (!queue.TryGetActiveEntry(probe, out float progress,
                    out bool completed) || completed) {
                    continue;
                }
                //细琥珀上行链："我的数据在灌过去"
                Vector2 end = target.Center - Main.screenPosition;
                DrawSerpentine(sb, origin, end, seed: 4096L + i, PvPTheme.Amber,
                    thickness: 1f, alpha: 0.4f, dashed: true, target,
                    flowSpeed: 0.5f);
                //目标侧的进度环脚注
                Vector2 anchor = end + new Vector2(0f, -34f);
                HackTheme.DrawRawText(sb, $"{(int)(progress * 100)}%",
                    anchor, PvPTheme.Amber * 0.8f, 0.6f);
            }
        }

        #endregion

        #region 坐标广播标记（攻击方队伍限定的穿墙标记）

        private static void DrawPositionCastMarkers(SpriteBatch sb) {
            int slotIndex = QuickHackDef.Get<PositionCast>()?.SlotIndex ?? -1;
            if (slotIndex < 0) return;
            var all = PlayerHackMirror.All;
            for (int i = 0; i < all.Count; i++) {
                PlayerHackMirror.MirrorEffect fx = all[i];
                if (fx.SlotIndex != slotIndex || fx.RemovedReason != null) continue;
                if (fx.DefenderIndex == Main.myPlayer) continue;
                //表现分支按队过滤：施加者本人或其非零同队才画
                if (!PositionCast.ViewerBenefits(fx.CasterIndex)) continue;
                Player defender = ResolveActive(fx.DefenderIndex);
                if (defender == null) continue;
                DrawWallhackMarker(sb, defender, HackTheme.Danger, 1f, defender.name);
            }
        }

        /// <summary>穿墙标记：菱形框 + 名字 + 距离；屏外转边缘箭头</summary>
        private static void DrawWallhackMarker(SpriteBatch sb, Player target,
            Color color, float alpha, string name) {
            Vector2 pos = target.Center - Main.screenPosition;
            if (TryClampToScreenEdge(ref pos, out Vector2 inwardDir)) {
                //屏外：边缘箭标 + 距离读数（1m = 16px）
                float meters = Vector2.Distance(Main.LocalPlayer.Center,
                    target.Center) / 16f;
                DrawEdgeArrow(sb, pos, -inwardDir, color, alpha);
                HackTheme.DrawRawText(sb,
                    PvPHudText.DistanceFormat.Format((int)meters),
                    pos + inwardDir * 26f - new Vector2(12f, 8f), color * alpha, 0.6f);
                return;
            }
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GameUpdateCount * 0.1f);
            HackTheme.DrawDiamondOutline(sb, pos, 16f * pulse, 1.4f, color * alpha);
            HackTheme.DrawDiamondOutline(sb, pos, 22f * pulse, 1f,
                color * (alpha * 0.35f));
            if (!string.IsNullOrEmpty(name)) {
                var font = FontAssets.MouseText.Value;
                float w = font.MeasureString(name).X * 0.6f;
                HackTheme.DrawRawText(sb, name,
                    pos + new Vector2(-w * 0.5f, 24f), color * (alpha * 0.9f), 0.6f);
            }
        }

        private static void DrawEdgeArrow(SpriteBatch sb, Vector2 edgePos,
            Vector2 outwardDir, Color color, float alpha) {
            float rot = outwardDir.ToRotation();
            Vector2 tip = edgePos;
            Vector2 baseL = tip - outwardDir * 14f
                + outwardDir.RotatedBy(MathHelper.PiOver2) * 7f;
            Vector2 baseR = tip - outwardDir * 14f
                - outwardDir.RotatedBy(MathHelper.PiOver2) * 7f;
            HackTheme.DrawLine(sb, baseL, tip, 2f, color * alpha);
            HackTheme.DrawLine(sb, baseR, tip, 2f, color * alpha);
            HackTheme.DrawLine(sb, baseL, baseR, 1f, color * (alpha * 0.6f));
        }

        #endregion

        #region 蛇形电弧折线

        private static void DrawSerpentine(SpriteBatch sb, Vector2 start, Vector2 end,
            long seed, Color color, float thickness, float alpha, bool dashed,
            Player remoteEndpoint, float flowSpeed) {
            DrawSerpentine(sb, start, end, seed, color, thickness, alpha, dashed,
                invisOverride: true, remoteEndpoint, flowSpeed);
        }

        /// <summary>
        /// 蛇形电弧：正弦蜿蜒 + 每段稳定故障抖动（种子按 <paramref name="seed"/>，
        /// 各帧稳定不闪烁成噪声）。屏外端点截断到屏缘并画箭标。<br/>
        /// <paramref name="invisOverride"/> = true 时尊重远端隐身降级
        /// （后 45% 段散成噪声云），回溯线传 false 无视之
        /// </summary>
        private static void DrawSerpentine(SpriteBatch sb, Vector2 start, Vector2 end,
            long seed, Color color, float thickness, float alpha, bool dashed,
            bool invisOverride, Player remoteEndpoint, float flowSpeed) {
            Vector2 clampedEnd = end;
            bool offscreen = TryClampToScreenEdge(ref clampedEnd, out Vector2 inward);
            //超过 PvP 准入距离（对面拉走了）整体淡出
            float worldDist = Vector2.Distance(start, end);
            if (worldDist > HackPvPRules.MaxDistance) {
                alpha *= MathHelper.Clamp(
                    1f - (worldDist - HackPvPRules.MaxDistance) / 600f, 0f, 1f);
                if (alpha <= 0.02f) return;
            }

            Vector2 span = clampedEnd - start;
            float length = span.Length();
            if (length < 24f) return;
            Vector2 dir = span / length;
            Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
            float time = Main.GameUpdateCount / 60f;
            bool remoteInvis = invisOverride && remoteEndpoint?.invis == true;

            Vector2 prev = start;
            bool prevBlocked = false;
            for (int i = 1; i <= LineSegments; i++) {
                float t = i / (float)LineSegments;
                //正弦蜿蜒（振幅 18px、0.6Hz 相位流动）+ 稳定的 ±3px 故障抖动
                float wave = MathF.Sin(t * MathHelper.TwoPi * 2.2f
                    + time * MathHelper.TwoPi * 0.6f) * WaveAmplitude
                    * MathF.Sin(t * MathHelper.Pi);
                float jitter = (Hash(seed * 31 + i) - 0.5f) * 6f;
                Vector2 point = start + dir * (length * t)
                    + normal * (wave + jitter);
                if (i == LineSegments) point = clampedEnd;

                //隐身降级：后 45% 段散成噪声云——方向可读、精确位置不可读
                if (remoteInvis && t > 0.55f) {
                    if (Hash(seed * 17 + i + (long)(time * 8)) > 0.45f) {
                        Vector2 scatter = point + new Vector2(
                            (Hash(seed + i * 3) - 0.5f) * 46f,
                            (Hash(seed + i * 7) - 0.5f) * 46f);
                        sb.Draw(HackTheme.Pixel, scatter, HackTheme.SrcPixel,
                            color * (alpha * 0.35f), 0f, new Vector2(0.5f),
                            2f, SpriteEffects.None, 0f);
                    }
                    prev = point;
                    continue;
                }

                //遮挡采样：每 4 段一次 CanHitLine，挡住的分段 35% 透明 + 虚线化
                if (i % 4 == 0) {
                    prevBlocked = !Terraria.Collision.CanHitLine(
                        prev + Main.screenPosition, 1, 1,
                        point + Main.screenPosition, 1, 1);
                }
                float segAlpha = prevBlocked ? alpha * 0.35f : alpha;
                bool segDashed = dashed || prevBlocked;

                if (segDashed) {
                    HackTheme.DrawDashedLine(sb, prev, point, thickness,
                        color * segAlpha, 4f, 4f);
                }
                else {
                    HackTheme.DrawLine(sb, prev, point, thickness, color * segAlpha);
                    //外层辉光 pass（亮色 additive，合法发光）
                    HackTheme.DrawLine(sb, prev, point, thickness + 2f,
                        color * (segAlpha * 0.22f));
                }

                //每 ~200px 一两根 30px 短电弧叉（快速闪灭）
                if (!segDashed && i % Math.Max(2, (int)(200f / (length / LineSegments))) == 0
                    && Hash(seed + i + (long)(time * 6)) > 0.6f) {
                    Vector2 forkDir = dir.RotatedBy(
                        (Hash(seed * 5 + i) - 0.5f) * 2.4f);
                    HackTheme.DrawLine(sb, point, point + forkDir * 30f, 1f,
                        color * (segAlpha * 0.5f));
                }
                prev = point;
            }

            //数据流亮点：沿线流动，方向 = flowSpeed 符号（正 = 起点→终点）
            float flow = (time * MathF.Abs(flowSpeed)) % 1f;
            if (flowSpeed < 0f) flow = 1f - flow;
            float flowT = flow;
            float flowWave = MathF.Sin(flowT * MathHelper.TwoPi * 2.2f
                + time * MathHelper.TwoPi * 0.6f) * WaveAmplitude
                * MathF.Sin(flowT * MathHelper.Pi);
            Vector2 flowPos = start + dir * (length * flowT) + normal * flowWave;
            if (!(remoteInvis && flowT > 0.55f)) {
                sb.Draw(HackTheme.Pixel, flowPos, HackTheme.SrcPixel,
                    Color.Lerp(color, Color.White, 0.65f) * alpha, 0f,
                    new Vector2(0.5f), 3.4f, SpriteEffects.None, 0f);
            }

            //屏外端：边缘箭标（距离读数由标记绘制侧负责）
            if (offscreen) {
                DrawEdgeArrow(sb, clampedEnd, -inward, color, alpha);
            }
        }

        #endregion

        #region 几何辅助

        /// <summary>
        /// 把（world - screenPosition）空间的点截到可视区边缘。GameViewMatrix 缩放下
        /// 的可视范围 = 屏心 ± 屏半宽/缩放。返回 true 表示原点在屏外，
        /// <paramref name="inwardDir"/> 为指回屏内的方向
        /// </summary>
        private static bool TryClampToScreenEdge(ref Vector2 point,
            out Vector2 inwardDir) {
            Vector2 zoom = Main.GameViewMatrix.Zoom;
            Vector2 half = new(Main.screenWidth * 0.5f, Main.screenHeight * 0.5f);
            Vector2 min = half - half / zoom + new Vector2(28f);
            Vector2 max = half + half / zoom - new Vector2(28f);
            Vector2 clamped = Vector2.Clamp(point, min, max);
            if (clamped == point) {
                inwardDir = Vector2.Zero;
                return false;
            }
            inwardDir = clamped - point;
            if (inwardDir.LengthSquared() > 0.01f) inwardDir.Normalize();
            point = clamped;
            return true;
        }

        private static Player ResolveActive(int index) {
            if (index < 0 || index >= Main.maxPlayers) return null;
            Player player = Main.player[index];
            return player?.active == true && !player.dead && !player.ghost
                ? player : null;
        }

        private static float Hash(long p) {
            unchecked {
                ulong x = (ulong)p * 2654435761UL;
                x ^= x >> 16;
                x *= 2246822519UL;
                x ^= x >> 13;
                return (x % 10000UL) / 10000f;
            }
        }

        #endregion
    }
}
