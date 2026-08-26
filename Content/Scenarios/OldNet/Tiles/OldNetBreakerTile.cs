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
    /// 应急泄压杆：每口竖井落点厅内壁一枚（布防见 OldNetThreatField.SeedBulkheads）。
    /// 落闸期间右键 → 本组闸门开 8s 后重落，代价 +10 噪。
    /// 用更多噪音买一次通行，付款方式本身延长刑期
    /// </summary>
    internal class OldNetBreakerTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Color Amber = new(255, 170, 60);
        private static readonly Color Mint = new(120, 255, 170);
        private static readonly Color DarkShell = new(24, 60, 68);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(80, 200, 140), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetBreakerHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            //落闸态裁决在 ThreatField（时停冻结/非落闸期死杆都在里面处理）
            return OldNetThreatField.TryPullBreaker(i, j, Main.LocalPlayer);
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            bool live = OldNetThreatField.GateState == OldNetThreatField.BulkheadState.Shut;
            float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * (live ? 6f : 1.5f) + i);
            if (live) {
                r = 0.10f * pulse;
                g = 0.30f * pulse;
                b = 0.18f * pulse;
            }
            else {
                r = 0.12f * pulse;
                g = 0.08f * pulse;
                b = 0.02f * pulse;
            }
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

            bool shut = OldNetThreatField.GateState == OldNetThreatField.BulkheadState.Shut;
            int window = OldNetThreatField.BreakerWindowTicks(i, j);
            bool venting = window > 0;
            //落闸期活杆薄荷绿快闪（"这里能开门"），平时暗琥珀待命
            Color accent = shut ? Mint : Amber;
            float live = shut ? 0.75f + 0.25f * MathF.Sin(t * 7f + i) : 0.4f;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //底座 + 竖导轨
            spriteBatch.Draw(px, center + new Vector2(0f, 6f), null, DarkShell, 0f,
                origin, Size(13f, 3.5f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, center + new Vector2(0f, 0.5f), null, DarkShell * 1.2f, 0f,
                origin, Size(3f, 13f), SpriteEffects.None, 0f);
            //杆体：泄压中垂到下位，待命立在上位（斜杆读作"待扳的开关"）
            float leverRot = venting ? 0.55f : -0.45f;
            Vector2 leverTip = center + new Vector2(venting ? 3.2f : -2.8f, venting ? 1.5f : -4.5f);
            spriteBatch.Draw(px, center + new Vector2(0f, 0.5f), null, accent * (0.65f * live + 0.25f),
                leverRot, origin, Size(2.6f, 12f), SpriteEffects.None, 0f);
            //杆头指示灯
            spriteBatch.Draw(px, leverTip, null, accent * live, MathHelper.PiOver4,
                origin, Size(4.2f, 4.2f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, leverTip, null, Color.White * (0.6f * live), MathHelper.PiOver4,
                origin, Size(2f, 2f), SpriteEffects.None, 0f);

            //泄压倒数弧：杆顶剩余窗口短条
            if (venting) {
                float left01 = window / (float)OldNetMetrics.BreakerOpenTicks;
                Vector2 barTl = center + new Vector2(-9f, -12f);
                spriteBatch.Draw(px, barTl, null, new Color(10, 20, 24) * 0.85f, 0f,
                    Vector2.Zero, Size(18f, 3f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, barTl, null, Mint, 0f,
                    Vector2.Zero, Size(18f * left01, 3f), SpriteEffects.None, 0f);
            }

            //杆头辉光（A=0 加色亮层）
            Texture2D glowTex = CWRAsset.SoftGlow?.Value;
            if (glowTex != null && (shut || venting)) {
                Color glowCol = accent * (0.4f * live);
                glowCol.A = 0;
                spriteBatch.Draw(glowTex, leverTip, null, glowCol, 0f,
                    glowTex.Size() * 0.5f, 0.14f, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
