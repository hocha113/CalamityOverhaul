using CalamityOverhaul.Content.Scenarios.OldNet.NPCs;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 噪音联动封锁闸格：T3 落闸时由 <see cref="OldNetThreatField"/> 运行时逐格写入
    /// 竖井咽喉（SealGate 同族：实心、不可采掘），噪音回落或泄压杆临时开启时整组移除。
    /// 视觉为 TechGate 语汇转 90°：暗底 + 上下缘警戒线 + 横向游走扫描亮段（纯 CPU）
    /// </summary>
    internal class OldNetBulkheadTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Color WarnRed = new(235, 64, 44);

        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = false;
            Main.tileLighted[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;
            //不可采掘：唯一通路是噪音管理或泄压杆
            MinPick = 999;
            MineResist = 30f;
            AddMapEntry(new Color(140, 30, 24), CreateMapEntryName());
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + i * 0.8f);
            r = 0.32f * pulse;
            g = 0.05f * pulse;
            b = 0.03f * pulse;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 tl = new Vector2(i * 16, j * 16) - Main.screenPosition + offset;
            float t = Main.GlobalTimeWrappedHourly;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //暗色闸体 + 上下缘警戒线（横闸：缘线在水平边，区别于 SealGate 竖柱）
            spriteBatch.Draw(px, tl, null, new Color(16, 8, 10), 0f, Vector2.Zero,
                Size(16f, 16f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, tl + new Vector2(0f, 1f), null, WarnRed * 0.35f, 0f, Vector2.Zero,
                Size(16f, 1.6f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, tl + new Vector2(0f, 13.4f), null, WarnRed * 0.35f, 0f, Vector2.Zero,
                Size(16f, 1.6f), SpriteEffects.None, 0f);

            //横向游走扫描亮段：整组（4 格宽=64px 周期）共相位，读作一扇通电的闸
            float phase = (t * 0.9f + j * 0.17f) % 1f;
            float scanWorldX = phase * 64f;
            float cellLeft = i * 16 % 64;
            float local = scanWorldX - cellLeft;
            if (local > -6f && local < 16f) {
                float xClamped = MathHelper.Clamp(local, 0f, 13f);
                spriteBatch.Draw(px, tl + new Vector2(xClamped, 2f), null, WarnRed * 0.8f, 0f,
                    Vector2.Zero, Size(3f, 12f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, tl + new Vector2(xClamped + 1f, 2f), null, Color.White * 0.5f, 0f,
                    Vector2.Zero, Size(1f, 12f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
