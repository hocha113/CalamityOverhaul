using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.TileProcessors;
using Terraria;
using Terraria.DataStructures;

namespace CalamityOverhaul.Content.Industrials.ElectricPowers.ControlVisuals
{
    /// <summary>
    /// 控制层共用线路表现:扫描 TP 外缘一圈的机关线,向有线方向发射示意流光。
    /// 纯客户端,由绘制帧边沿调用;不做寻路,只回答"信号进了哪根线"
    /// </summary>
    internal static class CtrlWireFX
    {
        //原版四色机关线的示意配色
        private static readonly Color RedWireTint = new(236, 94, 80);
        private static readonly Color BlueWireTint = new(96, 146, 242);
        private static readonly Color GreenWireTint = new(112, 222, 132);
        private static readonly Color YellowWireTint = new(240, 218, 108);

        /// <summary>
        /// 沿 TP 外缘发射线路流光;外缘没有任何机关线时在原地放一点
        /// <paramref name="fallback"/> 色微光,提示"脉冲发了但没接线"
        /// </summary>
        public static void EmitWirePulse(TileProcessor tp, Color fallback) {
            if (VaultUtils.isServer) {
                return;
            }

            int tileWidth = tp.Width / 16;
            int tileHeight = tp.Height / 16;
            int emitted = 0;

            //四条边逐格探线,方向=外法线;上限防大机器整圈布线时爆量
            for (int i = 0; i < tileWidth && emitted < 8; i++) {
                emitted += TryEmitAt(new Point16(tp.Position.X + i, tp.Position.Y - 1), new Vector2(0f, -1f));
                emitted += TryEmitAt(new Point16(tp.Position.X + i, tp.Position.Y + tileHeight), new Vector2(0f, 1f));
            }
            for (int j = 0; j < tileHeight && emitted < 8; j++) {
                emitted += TryEmitAt(new Point16(tp.Position.X - 1, tp.Position.Y + j), new Vector2(-1f, 0f));
                emitted += TryEmitAt(new Point16(tp.Position.X + tileWidth, tp.Position.Y + j), new Vector2(1f, 0f));
            }

            if (emitted == 0) {
                SpawnDot(tp.CenterInWorld, Vector2.Zero, fallback, 0.4f);
            }
        }

        private static int TryEmitAt(Point16 point, Vector2 dir) {
            Vector2 basePos = point.ToWorldCoordinates();
            if (!VaultUtils.IsPointOnScreen(basePos - Main.screenPosition, 200)) {
                return 0;
            }

            Tile tile = Framing.GetTileSafely(point);
            int count = 0;
            if (tile.RedWire) {
                count += SpawnDot(basePos - dir * 4f, dir, RedWireTint);
            }
            if (tile.BlueWire) {
                count += SpawnDot(basePos - dir * 4f, dir, BlueWireTint);
            }
            if (tile.GreenWire) {
                count += SpawnDot(basePos - dir * 4f, dir, GreenWireTint);
            }
            if (tile.YellowWire) {
                count += SpawnDot(basePos - dir * 4f, dir, YellowWireTint);
            }
            return count;
        }

        private static int SpawnDot(Vector2 pos, Vector2 dir, Color color, float scale = 0.55f) {
            Vector2 vel = dir * Main.rand.NextFloat(2.2f, 3.0f) + Main.rand.NextVector2Circular(0.2f, 0.2f);
            PRTLoader.NewParticle<PRT_CtrlWirePulse>(pos, vel, color, scale)?.Configure(Main.rand.Next(18, 26));
            return 1;
        }
    }
}
