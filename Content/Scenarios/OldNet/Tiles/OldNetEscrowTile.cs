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
    /// 保险契约终端（02 交互经济）：给"死亡/烧断丢全部未结算"装可购的对冲盘口。
    /// 右键收当前账本 30% 作保费（按类别占比扣，逐类 floor 余数从最大类补），
    /// 其余快照进 OldNetPlayer.InsuredShards；链路烧断或死亡时按快照兑付进 MoldShards。
    /// 安全登出不兑付不退款，保费沉没，这就是它的确定成本。
    /// 期望均衡点 = 自估暴毙率 30%：浅区智商税，深区刚需，定价随胆量自动分层
    /// </summary>
    internal class OldNetEscrowTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //暗金：金融件专色，区别薄荷绿账本族与琥珀中继族
        private static readonly Color DimGold = new(190, 150, 60);
        private static readonly Color DarkSlab = new(26, 22, 12);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //只允许右键交互
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(190, 150, 60), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetEscrowHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;
            OldNetPlayer session = OldNetPlayer.Get(player);
            Vector2 worldPos = new(i * 16 + 8, j * 16 + 8);

            int total = session.PendingTotal;
            if (total <= 0) {
                CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                    new Color(150, 160, 175), OldNetTexts.OldNetEscrowEmpty.Value);
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f }, worldPos);
                return true;
            }

            //保费 = ceil(总量 × 30%)，按类别占比扣：逐类 floor，余数从存量最大类补，保证扣满
            int premium = (int)MathF.Ceiling(total * OldNetMetrics.EscrowPremium);
            //账本太薄：保费吃掉全部（total=1 时保额为 0），0 保额的合约不签，终端保留
            if (total <= premium) {
                CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                    new Color(150, 160, 175), OldNetTexts.OldNetEscrowTooThin.Value);
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f }, worldPos);
                return true;
            }
            int[] cut = new int[session.PendingShards.Length];
            int assigned = 0;
            for (int k = 0; k < cut.Length; k++) {
                cut[k] = premium * session.PendingShards[k] / total;
                assigned += cut[k];
            }
            int remainder = premium - assigned;
            while (remainder > 0) {
                int best = -1;
                int bestLeft = 0;
                for (int k = 0; k < cut.Length; k++) {
                    int left = session.PendingShards[k] - cut[k];
                    if (left > bestLeft) {
                        bestLeft = left;
                        best = k;
                    }
                }
                if (best < 0) {
                    break;
                }
                cut[best]++;
                remainder--;
            }

            //扣保费 + 快照剩余账本（重复投保 = 新快照覆盖旧快照，再收一次保费）
            //TODO MP: 投保与快照是 per-player 本机写，服务器权威化时随结算整体重排
            for (int k = 0; k < cut.Length; k++) {
                session.PendingShards[k] -= cut[k];
                session.InsuredShards[k] = session.PendingShards[k];
            }
            session.AddNoise(OldNetMetrics.EscrowNoise);

            CombatText.NewText(player.getRect(), DimGold,
                OldNetTexts.OldNetEscrowSigned.Format(session.PendingTotal, premium), dramatic: true);
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.2f, Volume = 0.7f }, worldPos);
            SoundEngine.PlaySound(SoundID.CoinPickup with { Pitch = -0.5f, Volume = 0.6f }, worldPos);
            OldNetAbsorbFX.Emit(worldPos, DimGold, premium);

            //一次性消耗
            WorldGen.KillTile(i, j, noItem: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.2f + i * 0.4f);
            r = 0.24f * pulse;
            g = 0.18f * pulse;
            b = 0.05f * pulse;
        }

        //暗金矮碑 + 打字机式逐条点亮的微型"合约条文"横线 + 顶部印章方
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 + 8, j * 16 + 16) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            float seed = i * 0.47f;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //碑体（矮碑，微沉到地）
            spriteBatch.Draw(px, basePos + new Vector2(0f, -9f), null, DarkSlab, 0f,
                origin, Size(13f, 18f), SpriteEffects.None, 0f);
            //碑顶受光线
            spriteBatch.Draw(px, basePos + new Vector2(0f, -18f), null, DimGold * 0.55f, 0f,
                origin, Size(13f, 1.2f), SpriteEffects.None, 0f);

            //合约条文：5 行随机长短横线，打字机式循环逐条点亮
            const int lines = 5;
            float cycle = (t * 0.7f + seed) % 1f;
            int hot = (int)(cycle * lines);
            for (int k = 0; k < lines; k++) {
                //行宽由行序哈希出长短（合约条文的参差感），静态不随时间跳
                float w = 6.5f + 3.5f * MathF.Sin(seed * 7.3f + k * 12.9898f);
                float glow = k == hot ? 0.85f : 0.30f;
                spriteBatch.Draw(px, basePos + new Vector2(-0.5f, -14.5f + k * 2.4f), null,
                    DimGold * glow, 0f, origin, Size(MathF.Abs(w) + 4f, 0.9f), SpriteEffects.None, 0f);
            }

            //顶部印章方：斜置小方缓慢呼吸（等着落印）
            float pulse = 0.6f + 0.4f * MathF.Sin(t * 1.2f + seed);
            spriteBatch.Draw(px, basePos + new Vector2(0f, -21.5f), null,
                Color.Lerp(DimGold, Color.White, 0.25f) * (0.55f * pulse),
                MathHelper.PiOver4, origin, Size(3.2f, 3.2f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
