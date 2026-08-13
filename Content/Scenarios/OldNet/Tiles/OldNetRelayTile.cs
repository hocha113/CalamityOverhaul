using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 中继站：废墟带的部分结算点。右键把账本铭刻进 MoldShards、人不弹出、
    /// 账本清零继续潜；上行广播加噪——存完钱正是最危险的时候。
    /// 琥珀色光柱区别于登出终端的薄荷绿："能存钱但不是家"
    /// </summary>
    internal class OldNetRelayTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Color Amber = new(255, 180, 80);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //结算锚点不可破坏
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(255, 180, 80), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetRelayHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;
            OldNetPlayer session = OldNetPlayer.Get(player);

            if (session.PendingTotal <= 0) {
                CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                    new Color(150, 160, 175), OldNetTexts.OldNetRelayEmpty.Value);
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f }, new Vector2(i, j) * 16f);
                return true;
            }

            int total = session.SettleLedger();
            CombatText.NewText(player.getRect(), Amber,
                OldNetTexts.OldNetRelayDone.Format(total), dramatic: true);
            SoundEngine.PlaySound(SoundID.ResearchComplete with { Pitch = -0.25f }, player.Center);
            //上行广播惹注意：部分结算的代价
            session.AddNoise(OldNetMetrics.NoiseRelaySettle);
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            r = 0.42f;
            g = 0.28f;
            b = 0.08f;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 + 8, j * 16 + 16) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height);

            float t = Main.GlobalTimeWrappedHourly;
            float pulse = 0.8f + 0.2f * MathF.Sin(t * 2.1f + i * 0.5f);
            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //琥珀光柱：比终端矮一截——"驿站不是家"
            spriteBatch.Draw(px, basePos, null, Amber * 0.10f, 0f, origin,
                Size(18f, 92f * pulse), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, Amber * (0.30f * pulse), 0f, origin,
                Size(8f, 68f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, Color.White * (0.7f * pulse), 0f, origin,
                Size(2.5f, 44f), SpriteEffects.None, 0f);

            //基座
            spriteBatch.Draw(px, basePos, null, Amber * 0.9f, 0f, origin,
                Size(15f, 3.5f), SpriteEffects.None, 0f);

            //上行数据点：离散上升的短划，区别于终端的整环——中继在"发包"
            for (int k = 0; k < 3; k++) {
                float phase = (t * 0.9f + k * 0.33f + i * 0.17f) % 1f;
                float y = 76f * phase;
                float alpha = phase < 0.15f ? phase / 0.15f : 1f - (phase - 0.15f) / 0.85f;
                spriteBatch.Draw(px, basePos - new Vector2(0f, y), null,
                    Amber * (0.7f * alpha), 0f, origin, Size(5f, 2f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
