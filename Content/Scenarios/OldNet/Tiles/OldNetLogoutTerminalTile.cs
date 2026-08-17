using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 登出终端：钉在黑墙脚下的撤离锚点。右键把未铭刻账本写进
    /// SHPCPlayer.MoldShards 并安全断链回主世界。零贴图：程序化光柱绘制
    /// </summary>
    internal class OldNetLogoutTerminalTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //撤离锚点不可破坏
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(120, 255, 170), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetTerminalHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            OldNetPlayer.Get(Main.LocalPlayer).SettleAndLogout();
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = 0.10f;
            g = 0.55f;
            b = 0.45f;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            //shader 路径：上行天线柱（源头球根/顶端撕散在着色器内）
            if (Renders.OldNetTileFX.TerminalShaderReady) {
                Renders.OldNetTileFX.Columns.Add(new Renders.OldNetTileFX.ColumnEntry {
                    BasePos = new Vector2(i * 16 + 8, j * 16 + 16),
                    Relay = false,
                    Seed = i * 0.53f,
                });
                return false;
            }

            //CPU 回退：三层同轴光柱
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 + 8, j * 16 + 16) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height);

            float t = Main.GlobalTimeWrappedHourly;
            float pulse = 0.8f + 0.2f * MathF.Sin(t * 1.6f);
            Color accent = new(120, 255, 170);
            //占位纹理尺寸未知，按轴归一化到目标像素尺寸
            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //向上光柱：三层同轴渐窄，登出点在远处也读得到
            spriteBatch.Draw(px, basePos, null, accent * 0.10f, 0f, origin,
                Size(22f, 130f * pulse), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, accent * (0.28f * pulse), 0f, origin,
                Size(10f, 96f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, Color.White * (0.75f * pulse), 0f, origin,
                Size(3f, 64f), SpriteEffects.None, 0f);

            //基座横杠
            spriteBatch.Draw(px, basePos, null, accent * 0.9f, 0f, origin,
                Size(18f, 4f), SpriteEffects.None, 0f);

            //环圈脉冲：上升的细横线
            float ringPhase = (t * 0.5f) % 1f;
            float ringY = 100f * ringPhase;
            spriteBatch.Draw(px, basePos - new Vector2(0f, ringY), null,
                accent * (0.5f * (1f - ringPhase)), 0f, origin,
                Size(14f, 2f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
