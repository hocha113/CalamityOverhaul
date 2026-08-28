using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 旧网接入终端：主世界侧的标准深潜入口（坠舱中舱随世界生成）。
    /// 两次交互确认制，首次预热链路，5 秒内再次交互越墙深潜；
    /// 深潜仅单人模式开放（旧网系统单人优先，MP 后置）。<br/>
    /// 物理格只有基座 1x1，但视觉是整根天线光柱，悬停与右键由
    /// <see cref="OldNetAccessTerminalInteract"/> 扩展到整根柱身；
    /// 确认窗口内光柱增亮脉动，亮度随剩余时间回落读作倒计时
    /// </summary>
    internal class OldNetAccessTerminalTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //确认窗口：首次交互后 5 秒内再次交互才深潜
        private const int ConfirmWindowTicks = 300;
        private static int armedUntilTick;
        private static Point armedAt = new(-1, -1);

        //交互包络（与光柱画布 48x168 对齐）：基座为锚，向上 10 格、左右各 1 格
        internal const int ColumnTilesUp = 10;
        internal const int ColumnTilesSide = 1;

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //入口锚点不可破坏
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(140, 200, 210), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        /// <summary>确认窗口剩余量 0..1；渲染读它做预热增亮，交互读它判武装态</summary>
        internal static float ArmedBoost01(int i, int j) {
            if (armedAt.X != i || armedAt.Y != j) {
                return 0f;
            }
            int remain = armedUntilTick - (int)Main.GameUpdateCount;
            return remain <= 0 ? 0f : remain / (float)ConfirmWindowTicks;
        }

        /// <summary>悬停提示：原生 MouseOver 与柱身区域悬停共用</summary>
        internal static void ShowHover(Player player, int i, int j) {
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = ArmedBoost01(i, j) > 0f
                ? OldNetTexts.OldNetEnterArmedHint.Value
                : OldNetTexts.OldNetEnterHint.Value;
        }

        /// <summary>两段确认交互：原生 RightClick 与柱身区域点击共用，锚定同一基座坐标</summary>
        internal static bool Interact(int i, int j) {
            if (Main.netMode == NetmodeID.Server || OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;

            //单人门禁：旧网系统单人优先（与 /oldnet 同口径）
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CombatText.NewText(player.getRect(), Color.IndianRed, OldNetTexts.OldNetEnterSPOnly.Value);
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.6f }, player.Center);
                return true;
            }

            if (ArmedBoost01(i, j) <= 0f) {
                //首次交互：预热链路，武装确认窗口
                armedAt = new Point(i, j);
                armedUntilTick = (int)Main.GameUpdateCount + ConfirmWindowTicks;
                CombatText.NewText(player.getRect(), new Color(140, 200, 210),
                    OldNetTexts.OldNetEnterConfirm.Value, dramatic: true);
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = -0.3f },
                    new Vector2(i, j) * 16f);
                return true;
            }

            //确认交互：越墙深潜
            armedAt = new Point(-1, -1);
            SoundEngine.PlaySound(SoundID.Item78 with { Volume = 0.7f, Pitch = -0.2f }, player.Center);
            OldNetWorld.EnterWorld();
            return true;
        }

        public override void MouseOver(int i, int j) => ShowHover(Main.LocalPlayer, i, j);

        public override bool RightClick(int i, int j) => Interact(i, j);

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.8f);
            r = 0.22f * pulse;
            g = 0.40f * pulse;
            b = 0.44f * pulse;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            //彩色照明下本层走缓存 RT，PreDraw 只在重绘帧被调用；
            //这里只登记逐帧特殊绘制点，真正的登记/回退在 SpecialDraw（每帧必跑），否则光柱闪烁
            Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.CustomNonSolid);
            return false;
        }

        public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch) {
            //shader 路径：上行天线柱（与旧网内登出终端同语汇，两端是同一条链路）
            if (Renders.OldNetTileFX.TerminalShaderReady) {
                Renders.OldNetTileFX.Columns.Add(new Renders.OldNetTileFX.ColumnEntry {
                    BasePos = new Vector2(i * 16 + 8, j * 16 + 16),
                    Relay = false,
                    Seed = i * 0.53f,
                    Boost = ArmedBoost01(i, j),
                });
                return;
            }

            //CPU 回退：冷青三层光柱
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 + 8, j * 16 + 16) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height);
            float t = Main.GlobalTimeWrappedHourly;
            float pulse = 0.8f + 0.2f * MathF.Sin(t * 1.8f);
            //预热增亮与 shader 路径同节奏
            float boost = 1f + ArmedBoost01(i, j) * (0.4f + 0.25f * MathF.Sin(t * 9f));
            Color accent = new(140, 200, 210);
            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            spriteBatch.Draw(px, basePos, null, accent * (0.10f * boost), 0f, origin,
                Size(22f, 120f * pulse), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, accent * (0.28f * pulse * boost), 0f, origin,
                Size(10f, 88f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, Color.White * (0.7f * pulse * boost), 0f, origin,
                Size(3f, 58f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, accent * 0.9f, 0f, origin,
                Size(18f, 4f), SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 接入终端的柱身区域交互：物理格只有 1x1 基座，视觉却是整根 48x168 光柱，
    /// 只点得中基座的话手感极差。本系统把悬停提示与右键覆盖到整根柱身，
    /// 命中后折算回基座锚点，走 <see cref="OldNetAccessTerminalTile.Interact"/> 同一套两段确认。<br/>
    /// 挂 PreItemCheck：时点在原生 tile 交互（LookForTileInteractions）之后、
    /// 物品右键之前——鼠标点在实体格上的情况全部让给原生路径（见 HasTile 闸），
    /// 这里只接管光柱空气区的点击，消耗掉的点击不会再触发物品右键
    /// </summary>
    internal class OldNetAccessTerminalInteract : ModPlayer
    {
        public override bool PreItemCheck() {
            TryRegionInteract();
            return true;
        }

        private void TryRegionInteract() {
            if (Player.whoAmI != Main.myPlayer || Main.dedServ || OldNetWorld.Active) {
                return;
            }
            if (Player.dead || Player.mouseInterface || Main.mapFullscreen || Main.HoveringOverAnNPC) {
                return;
            }

            int mx = Player.tileTargetX;
            int my = Player.tileTargetY;
            //鼠标须点在光柱的空气区；任何实体格（基座本身、旁边玩家摆的箱子等）
            //一律让原生 tile 路径处理，防双触发与悬停提示互抢
            Tile hover = Framing.GetTileSafely(mx, my);
            if (hover.HasTile) {
                return;
            }

            int type = ModContent.TileType<OldNetAccessTerminalTile>();
            if (!TryFindAnchor(mx, my, type, out Point anchor)) {
                return;
            }
            if (!Player.IsInTileInteractionRange(anchor.X, anchor.Y, TileReachCheckSettings.Simple)) {
                return;
            }

            OldNetAccessTerminalTile.ShowHover(Player, anchor.X, anchor.Y);
            if (Main.mouseRight && Main.mouseRightRelease) {
                //消耗本次点击，防物品右键同帧触发
                Main.mouseRightRelease = false;
                OldNetAccessTerminalTile.Interact(anchor.X, anchor.Y);
            }
        }

        /// <summary>光柱包络内找基座：锚点只会在鼠标格下方 0..10 格、左右各 1 格</summary>
        private static bool TryFindAnchor(int mx, int my, int type, out Point anchor) {
            for (int dy = 0; dy <= OldNetAccessTerminalTile.ColumnTilesUp; dy++) {
                for (int dx = -OldNetAccessTerminalTile.ColumnTilesSide;
                    dx <= OldNetAccessTerminalTile.ColumnTilesSide; dx++) {
                    Tile tile = Framing.GetTileSafely(mx + dx, my + dy);
                    if (tile.HasTile && tile.TileType == type) {
                        anchor = new Point(mx + dx, my + dy);
                        return true;
                    }
                }
            }
            anchor = default;
            return false;
        }
    }
}
