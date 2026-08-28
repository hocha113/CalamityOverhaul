using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent.Drawing;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 封锁区闸门：封死砖盒开口的实心立柱，不可采掘，
    /// 只能被事件节点拉闸整批解除（OldNetICEDirector.UnsealAll）。
    /// 零贴图：暗底 + 上下游走的警戒红光
    /// </summary>
    internal class OldNetSealGateTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Color WarnRed = new(235, 64, 44);

        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = false;
            Main.tileLighted[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            //不可采掘：唯一开门方式是拉闸
            MinPick = 999;
            MineResist = 30f;
            AddMapEntry(new Color(120, 24, 20), CreateMapEntryName());
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + j * 0.9f);
            r = 0.30f * pulse;
            g = 0.04f * pulse;
            b = 0.03f * pulse;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            //缓存 RT 路径下 PreDraw 非逐帧，只登记特殊绘制点（实心层用 CustomSolid）；
            //登记/回退在 SpecialDraw，否则扫描段一帧有一帧无
            Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.CustomSolid);
            return false;
        }

        public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch) {
            float t = Main.GlobalTimeWrappedHourly;

            //shader 路径：整根闸柱共相位的通电扫描（局部扫描行 CPU 折算）
            if (Renders.OldNetTileFX.TerminalShaderReady) {
                float scanPhase = (t * 0.8f + i * 0.13f) % 1f;
                float localScan = (scanPhase * 128f - j * 16 % 128) / 16f;
                Renders.OldNetTileFX.Gates.Add(new Renders.OldNetTileFX.GateEntry {
                    TopLeft = new Vector2(i * 16, j * 16),
                    LocalScan = localScan,
                    Seed = i * 0.13f,
                });
                return;
            }

            //CPU 回退：暗底 + 双缘警戒线 + 扫描亮段
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 tl = new Vector2(i * 16, j * 16) - Main.screenPosition + offset;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //暗色柱体 + 双缘警戒线
            spriteBatch.Draw(px, tl, null, new Color(16, 8, 10), 0f, Vector2.Zero,
                Size(16f, 16f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, tl + new Vector2(1f, 0f), null, WarnRed * 0.35f, 0f, Vector2.Zero,
                Size(1.6f, 16f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, tl + new Vector2(13.4f, 0f), null, WarnRed * 0.35f, 0f, Vector2.Zero,
                Size(1.6f, 16f), SpriteEffects.None, 0f);

            //上下游走的扫描亮段：整根柱共相位，读作一道通电的闸
            float phase = (t * 0.8f + i * 0.13f) % 1f;
            float scanWorldY = phase * 128f;
            float cellTop = j * 16 % 128;
            float local = scanWorldY - cellTop;
            if (local > -6f && local < 16f) {
                float yClamped = MathHelper.Clamp(local, 0f, 13f);
                spriteBatch.Draw(px, tl + new Vector2(2f, yClamped), null, WarnRed * 0.8f, 0f,
                    Vector2.Zero, Size(12f, 3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, tl + new Vector2(2f, yClamped + 1f), null, Color.White * 0.5f, 0f,
                    Vector2.Zero, Size(12f, 1f), SpriteEffects.None, 0f);
            }
        }
    }
}
