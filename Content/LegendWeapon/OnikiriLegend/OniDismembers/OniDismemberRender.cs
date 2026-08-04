using CalamityOverhaul.Common;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers
{
    /// <summary>肢解剪影 RT</summary>
    internal sealed class OniDismemberRender : RenderHandle
    {
        //单帧快照捕获上限

        //Entries 按到落刀点的距离序排列，先捕获的恰是玩家眼前的

        private const int MaxCapturesPerFrame = 24;
        private const int MaxCaptureFailures = 2;
        //孤儿 RT 清理暂存

        private static readonly List<int> pruneScratch = [];

        public override void UpdateBySystem(int index) {
            //回主菜单后实体状态已失效，释放全部快照

            if (Main.gameMenu && OniDismember.SnapRTs.Count > 0) {
                OniDismember.DisposeAllSnapshots();
            }
        }

        public override void DrawNPCsOverTiles(SpriteBatch spriteBatch, GraphicsDevice graphicsDevice, RenderTarget2D screenSwap) {
            if (Main.gameMenu) {
                return;
            }

            PruneOrphanRTs();

            if (!AnyPendingCapture()) {
                return;
            }

            //低质量光照/RT 异常时放弃捕获、目标仅定身不裂开，本体照常绘制

            if (RenderQualitySafety.ScreenTargetUnavailable()) {
                return;
            }
            if (screenSwap == null || screenSwap.IsDisposed) {
                return;
            }
            if (Main.screenTarget == null || Main.screenTarget.IsDisposed) {
                return;
            }
            if (!RenderQualitySafety.IsScreenTargetActive(graphicsDevice)) {
                return;
            }

            RenderTargetBinding[] previousTargets = graphicsDevice.GetRenderTargets();

            //先保屏、screenTarget 一旦重绑定内容即被丢弃

            graphicsDevice.SetRenderTarget(screenSwap);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            spriteBatch.Draw(Main.screenTarget, Vector2.Zero, Color.White);
            spriteBatch.End();

            //逐个捕获待处理目标，单帧限量防群组触发帧卡顿

            int captured = 0;
            foreach (DismemberEntry entry in OniDismember.Entries) {
                if (entry.Captured || entry.SnapWidth <= 0) {
                    continue;
                }
                NPC npc = Main.npc[entry.NpcIndex];
                if (!npc.active || npc.type != entry.NpcType) {
                    continue;
                }
                CaptureSnapshot(spriteBatch, graphicsDevice, entry, npc);
                if (++captured >= MaxCapturesPerFrame) {
                    break;
                }
            }

            //还屏

            graphicsDevice.SetRenderTarget(Main.screenTarget);
            graphicsDevice.Clear(Color.Transparent);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
            spriteBatch.Draw(screenSwap, Vector2.Zero, Color.White);
            spriteBatch.End();

            //还原进入时的 RT 绑定，避免改变上层管线对活动 RT 的预期

            if (previousTargets != null && previousTargets.Length > 0
                && previousTargets[0].RenderTarget != Main.screenTarget) {
                graphicsDevice.SetRenderTargets(previousTargets);
            }
        }

        /// <summary>NPC 完整外观 → 专属 RT；伪造 screenPos 让 npc.Center 落在</summary>
        private static void CaptureSnapshot(SpriteBatch sb, GraphicsDevice gd, DismemberEntry entry, NPC npc) {
            RenderTarget2D rt = OniDismember.EnsureSnapshotRT(gd, entry);
            if (rt == null) {
                entry.SnapWidth = 0;    //显存不足等异常、永久降级为仅定身
                entry.SnapHeight = 0;

                return;
            }

            if (CaptureNpcAppearance(sb, gd, npc, rt, entry.AnchorCenter, entry.BehindTiles)) {
                entry.Captured = true;
                entry.CaptureFailures = 0;
                return;
            }

            entry.CaptureFailures++;
            if (entry.CaptureFailures >= MaxCaptureFailures) {
                entry.SnapWidth = 0;
                entry.SnapHeight = 0;
                if (OniDismember.SnapRTs.Remove(entry.NpcIndex, out RenderTarget2D failedRT)) {
                    failedRT?.Dispose();
                }
            }
        }

        /// <summary>把 NPC 完整外观画进给定 RT，批次恢复成功才返回 true</summary>
        internal static bool CaptureNpcAppearance(SpriteBatch sb, GraphicsDevice gd, NPC npc,
            RenderTarget2D rt, Vector2 anchorCenter, bool behindTiles) {

            if (sb == null || gd == null || npc == null || rt == null || rt.IsDisposed) {
                return false;
            }

            Vector2 fakeScreenPos = anchorCenter - new Vector2(rt.Width, rt.Height) * 0.5f;
            Vector2 realScreenPos = Main.screenPosition;
            bool batchBegan = false;
            bool drawSucceeded = false;
            bool batchEnded = false;
            try {
                gd.SetRenderTarget(rt);
                gd.Clear(Color.Transparent);
                //部分模组 NPC 的 PreDraw 直接读 Main.screenPosition
                Main.screenPosition = fakeScreenPos;
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
                batchBegan = true;
                Main.instance.DrawNPCDirect(sb, npc, behindTiles, fakeScreenPos);
                drawSucceeded = true;
            } catch {
                //单个 NPC 绘制钩子异常不拖垮捕获管线

            } finally {
                Main.screenPosition = realScreenPos;
                if (batchBegan) {
                    try {
                        sb.End();
                        batchEnded = true;
                    } catch {
                        //绘制钩子破坏批次状态时判定捕获失败

                    }
                }
            }
            return drawSucceeded && batchEnded;
        }

        private static bool AnyPendingCapture() {
            foreach (DismemberEntry entry in OniDismember.Entries) {
                if (!entry.Captured && entry.SnapWidth > 0) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>释放已无对应肢解状态的快照 RT</summary>
        private static void PruneOrphanRTs() {
            if (OniDismember.SnapRTs.Count == 0) {
                return;
            }
            pruneScratch.Clear();
            foreach (int npcIndex in OniDismember.SnapRTs.Keys) {
                if (OniDismember.GetEntry(npcIndex) == null) {
                    pruneScratch.Add(npcIndex);
                }
            }
            foreach (int npcIndex in pruneScratch) {
                OniDismember.SnapRTs[npcIndex]?.Dispose();
                OniDismember.SnapRTs.Remove(npcIndex);
            }
        }
    }
}
