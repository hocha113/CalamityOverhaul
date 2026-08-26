using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 静默哨雷：贴着糖放的尖叫地雷，不炸人、炸匿名性。
    /// 快速接近武装 30 tick 后引爆（+14 噪 + 25 HP + 2 RAM），慢速接近不触发；
    /// 慢速贴身按住右键 40 tick 可静默拆除（零噪音、无掉落）。
    /// 状态机在 <see cref="OldNetThreatField"/> 注册表，本 tile 只负责交互入口与绘制
    /// </summary>
    internal class OldNetSentryMineTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Color WarnRed = new(235, 64, 44);
        private static readonly Color Amber = new(255, 170, 60);
        private static readonly Color Mint = new(120, 255, 170);
        private static readonly Color DarkShell = new(20, 44, 50);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(200, 120, 40), CreateMapEntryName());
        }

        /// <summary>
        /// 撒布委托（P55 RunZoneEntries 消费，gen 期）：验证 12 列内存在加密节点或
        /// 深潜缓存（贴糖检查，本地自查不碰 ScatterPass 的 IsCrowded 表）后直写落位
        /// </summary>
        internal static bool TryPlaceNearLoot(int x, int y) {
            int encryptType = ModContent.TileType<OldNetEncryptedNodeTile>();
            int cacheType = ModContent.TileType<OldNetCacheTile>();
            bool nearLoot = false;
            for (int dx = -OldNetMetrics.MineNearLootCols;
                dx <= OldNetMetrics.MineNearLootCols && !nearLoot; dx++) {
                for (int dy = -10; dy <= 10; dy++) {
                    Tile probe = Framing.GetTileSafely(x + dx, y + dy);
                    if (probe.HasTile
                        && (probe.TileType == encryptType || probe.TileType == cacheType)) {
                        nearLoot = true;
                        break;
                    }
                }
            }
            if (!nearLoot) {
                return false;
            }
            return OldNetNodeBudget.WriteNodeTile(x, y, ModContent.TileType<OldNetSentryMineTile>());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetMineHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;
            //快速状态下点不动引信：拆除的入场券就是慢
            if (player.velocity.Length() > OldNetMetrics.MineArmSpeedGate) {
                return true;
            }
            //懒扫描间隙自愈入册，随后开拆除信道（受击/移动/松开右键即中断）
            OldNetThreatField.EnsureMineTracked(i, j);
            player.GetModPlayer<OldNetThreatPlayer>()
                .BeginChannel(OldNetThreatPlayer.KindDefuseMine, i, j);
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            //休眠琥珀慢闪（2s 周期），与数据节点的常亮明确区分
            float blink = MathF.Sin(Main.GlobalTimeWrappedHourly * MathHelper.Pi + i * 0.77f) > 0.55f ? 1f : 0.2f;
            r = 0.20f * blink;
            g = 0.12f * blink;
            b = 0.02f * blink;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 center = new Vector2(i * 16 + 8, j * 16 + 8) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;

            OldNetThreatField.MineState state = OldNetThreatField.GetMineState(i, j);
            bool arming = state?.Phase == OldNetThreatField.MinePhase.Arming;
            float armFrac = arming
                ? MathHelper.Clamp(state.ArmTimer / (float)OldNetMetrics.MineArmTicks, 0f, 1f) : 0f;

            OldNetThreatPlayer channel = Main.LocalPlayer?.active == true
                ? Main.LocalPlayer.GetModPlayer<OldNetThreatPlayer>() : null;
            bool defusing = channel?.IsChanneling(OldNetThreatPlayer.KindDefuseMine, i, j) == true;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //半埋暗壳：下宽台 + 上窄顶（"埋进地里的方顶"轮廓）
            spriteBatch.Draw(px, center + new Vector2(0f, 4f), null, DarkShell, 0f,
                origin, Size(14f, 7f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, center + new Vector2(0f, -1f), null, DarkShell * 1.1f, 0f,
                origin, Size(9f, 5f), SpriteEffects.None, 0f);

            //指示灯：休眠琥珀慢闪 / 武装红色快闪
            float blink = arming
                ? (MathF.Sin(t * (14f + armFrac * 18f)) > 0f ? 1f : 0.25f)
                : (MathF.Sin(t * MathHelper.Pi + i * 0.77f) > 0.55f ? 1f : 0.15f);
            Color lampCol = arming ? WarnRed : Amber;
            Vector2 lampPos = center + new Vector2(0f, -2.5f);
            spriteBatch.Draw(px, lampPos, null, lampCol * blink, MathHelper.PiOver4,
                origin, Size(3.4f, 3.4f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, lampPos, null, Color.White * (0.6f * blink), MathHelper.PiOver4,
                origin, Size(1.6f, 1.6f), SpriteEffects.None, 0f);

            //武装警戒环：从雷心扩散的菱形虚线（半径 90px 显式画出，范围威胁必须画范围）
            if (arming) {
                float ringR = 20f + armFrac * (OldNetMetrics.MineWakeRadius - 20f);
                Color ringCol = WarnRed * (0.28f + 0.35f * blink);
                for (int k = 0; k < 4; k++) {
                    float ang = MathHelper.PiOver2 * k + MathHelper.PiOver4;
                    Vector2 a = center + ang.ToRotationVector2() * ringR;
                    Vector2 b = center + (ang + MathHelper.PiOver2).ToRotationVector2() * ringR;
                    //每条边拆 4 段虚线
                    for (int seg = 0; seg < 4; seg++) {
                        if (seg % 2 == 1) {
                            continue;
                        }
                        Vector2 p0 = Vector2.Lerp(a, b, seg / 4f);
                        Vector2 p1 = Vector2.Lerp(a, b, (seg + 1) / 4f);
                        Vector2 diff = p1 - p0;
                        spriteBatch.Draw(px, p0, new Rectangle(0, 0, 1, 1), ringCol,
                            diff.ToRotation(), new Vector2(0f, 0.5f),
                            new Vector2(diff.Length(), 1.2f), SpriteEffects.None, 0f);
                    }
                }
            }

            //拆除进度：雷顶薄荷绿短条（PatrolICE 头顶进度条语汇反色）
            if (defusing) {
                float prog = channel.ChannelProgress;
                Vector2 barTl = center + new Vector2(-10f, -13f);
                spriteBatch.Draw(px, barTl, null, new Color(10, 20, 24) * 0.85f, 0f,
                    Vector2.Zero, Size(20f, 3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, barTl, null, Mint, 0f,
                    Vector2.Zero, Size(20f * prog, 3f), SpriteEffects.None, 0f);
            }

            //灯芯辉光（A=0 加色亮层）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null && blink > 0.3f) {
                Color glowCol = lampCol * (0.35f * blink + armFrac * 0.25f);
                glowCol.A = 0;
                spriteBatch.Draw(glowTex, lampPos, null, glowCol, 0f,
                    glowTex.Size() * 0.5f, 0.13f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
