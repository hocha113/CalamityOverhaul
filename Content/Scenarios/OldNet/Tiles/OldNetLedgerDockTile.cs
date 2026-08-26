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
    /// 账本扩容坞（02 交互经济）：兑现 LedgerCapacityBonus 留缝的首件内容。
    /// 右键一次性：本次深潜账本容量 +8 格、噪音 +15，坞体消散。
    /// HUD 的 LEDGER 行读 LedgerCapacity 计算属性，容量变更零改动自动显示。
    /// 价签逻辑：15 噪 vs 一趟中继 25 噪 + 折返路费，两张价签摆一张桌上让玩家自己算
    /// </summary>
    internal class OldNetLedgerDockTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //薄荷绿族：与登出终端同族，都是"账本设施"语汇
        private static readonly Color Mint = new(110, 240, 170);
        private static readonly Color DarkShell = new(14, 26, 20);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //只允许右键交互，防镐子路径绕过
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(110, 240, 170), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetDockHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;
            OldNetPlayer session = OldNetPlayer.Get(player);

            //TODO MP: 容量变更是 per-player 会话字段本机写，将来进玩家状态同步包
            session.LedgerCapacityBonus += OldNetMetrics.LedgerDockBonus;
            session.AddNoise(OldNetMetrics.LedgerDockNoise);

            CombatText.NewText(player.getRect(), Mint,
                OldNetTexts.OldNetDockDone.Value, dramatic: true);
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.15f, Volume = 0.6f },
                new Vector2(i, j) * 16f);
            SoundEngine.PlaySound(SoundID.ResearchComplete with { Pitch = 0.3f, Volume = 0.5f },
                player.Center);
            OldNetAbsorbFX.Emit(new Vector2(i * 16 + 8, j * 16 + 8), Mint, 8);

            //一次性消耗
            WorldGen.KillTile(i, j, noItem: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.8f + i * 0.6f);
            r = 0.08f * pulse;
            g = 0.26f * pulse;
            b = 0.16f * pulse;
        }

        //矮桩 + 顶部 6 段"扩展槽"短格环：段格轮流点亮，读作待装配的空槽
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 + 8, j * 16 + 16) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            float pulse = 0.75f + 0.25f * MathF.Sin(t * 1.8f + i * 0.6f);

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //桩体（微沉到地）
            spriteBatch.Draw(px, basePos + new Vector2(0f, -6f), null, DarkShell, 0f,
                origin, Size(12f, 12f), SpriteEffects.None, 0f);
            //桩顶受光线
            spriteBatch.Draw(px, basePos + new Vector2(0f, -12f), null, Mint * 0.5f, 0f,
                origin, Size(12f, 1.2f), SpriteEffects.None, 0f);
            //桩身中缝指示灯
            spriteBatch.Draw(px, basePos + new Vector2(0f, -6f), null, Mint * (0.55f * pulse), 0f,
                origin, Size(8f, 1.4f), SpriteEffects.None, 0f);

            //顶部 6 段短格环：逐段轮流点亮（空槽在等着被买下）
            const int segs = 6;
            const float radius = 9f;
            Vector2 ringCenter = basePos + new Vector2(0f, -19f);
            int hot = (int)(t * 2.2f + i * 0.7f) % segs;
            for (int s = 0; s < segs; s++) {
                float ang = MathHelper.TwoPi * s / segs - MathHelper.PiOver2;
                Vector2 p = ringCenter + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
                bool lit = s == hot;
                Color c = lit ? Color.Lerp(Mint, Color.White, 0.35f) * 0.95f : Mint * 0.22f;
                spriteBatch.Draw(px, p, null, c, ang + MathHelper.PiOver2,
                    origin, Size(3.6f, 1.6f), SpriteEffects.None, 0f);
            }
            //环心弱芯
            spriteBatch.Draw(px, ringCenter, null, Mint * (0.30f * pulse),
                MathHelper.PiOver4, origin, Size(3f, 3f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
