using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 事件节点（拉闸）：右键两件事同时发生，噪音直上清剿波档 +
    /// 全图封锁区闸门解除。一次决策、全局后果；节点一次性消耗。
    /// 零贴图：警戒红闸杆 + 慢闪信标
    /// </summary>
    internal class OldNetEventNodeTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Color WarnRed = new(235, 64, 44);
        private static readonly Color AmberDim = new(255, 170, 60);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(235, 64, 44), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetEventHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;
            OldNetPlayer session = OldNetPlayer.Get(player);

            //①噪音跳档：直入 T4 清剿波
            session.SetNoiseFloor(OldNetMetrics.NoiseEventPull);
            //②全图解封
            OldNetICEDirector.UnsealAll();

            CombatText.NewText(player.getRect(), WarnRed, OldNetTexts.OldNetEventPulled.Value, dramatic: true);
            SoundEngine.PlaySound(CWRSound.FaultOccurred with { Volume = 0.9f, Pitch = -0.5f }, player.Center);
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.9f, Pitch = -0.4f }, new Vector2(i, j) * 16f);

            //拉过的闸不复位
            WorldGen.KillTile(i, j, noItem: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            //慢闪信标：亮-灭节律区别于常亮节点
            float blink = MathF.Sin(Main.GlobalTimeWrappedHourly * 4f + i * 0.7f) > 0.3f ? 1f : 0.25f;
            r = 0.34f * blink;
            g = 0.06f * blink;
            b = 0.04f * blink;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            //缓存 RT 路径下 PreDraw 非逐帧，只登记特殊绘制点；登记/回退在 SpecialDraw（防闪烁）
            Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.CustomNonSolid);
            return false;
        }

        public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch) {
            //shader 路径：闸杆信标技法（慢闪节律在着色器内）
            if (Renders.OldNetTileFX.NodeShaderReady) {
                Renders.OldNetTileFX.Nodes.Add(new Renders.OldNetTileFX.NodeEntry {
                    Center = new Vector2(i * 16 + 8, j * 16 + 8),
                    Kind = 2,
                    Seed = i * 0.7f,
                    Progress = 0f,
                });
                return;
            }

            //CPU 回退：警戒红闸杆 + 慢闪信标
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 center = new Vector2(i * 16 + 8, j * 16 + 8) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            float blink = MathF.Sin(t * 4f + i * 0.7f) > 0.3f ? 1f : 0.35f;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //底座横杠
            spriteBatch.Draw(px, center + new Vector2(0f, 6f), null, new Color(40, 20, 18), 0f,
                origin, Size(14f, 3f), SpriteEffects.None, 0f);
            //闸杆：斜置的杆读作"待扳的开关"
            spriteBatch.Draw(px, center + new Vector2(0f, 1f), null, AmberDim * 0.85f,
                -0.5f, origin, Size(3f, 14f), SpriteEffects.None, 0f);
            //杆头信标
            Vector2 head = center + new Vector2(3.4f, -5.2f);
            spriteBatch.Draw(px, head, null, WarnRed * blink, MathHelper.PiOver4,
                origin, Size(5f, 5f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, head, null, Color.White * (0.7f * blink), MathHelper.PiOver4,
                origin, Size(2f, 2f), SpriteEffects.None, 0f);

            //警戒圈：慢速扩散的菱形描边
            float ringPhase = (t * 0.7f + i * 0.31f) % 1f;
            float ringR = 8f + ringPhase * 14f;
            Color ringCol = WarnRed * (0.4f * (1f - ringPhase) * blink);
            for (int k = 0; k < 4; k++) {
                float ang = MathHelper.PiOver2 * k + MathHelper.PiOver4;
                Vector2 a = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * ringR;
                Vector2 b = center + new Vector2(MathF.Cos(ang + MathHelper.PiOver2), MathF.Sin(ang + MathHelper.PiOver2)) * ringR;
                Vector2 diff = b - a;
                spriteBatch.Draw(px, a, new Rectangle(0, 0, 1, 1), ringCol, diff.ToRotation(),
                    new Vector2(0f, 0.5f), new Vector2(diff.Length(), 1f), SpriteEffects.None, 0f);
            }
        }
    }
}
