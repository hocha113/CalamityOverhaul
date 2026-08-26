using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.RAMSystems;
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
    /// 冷存储节点（02 交互经济）：用 RAM 付账的无声节点。
    /// 右键耗 2 RAM 产出 (1-3)×2 枚碎片，全程零噪音（连 AddNoise 都不调）。
    /// 撒布在废墟带深段与衰减区：RAM 底噪最贵的地方，同一价签自动随位置膨胀。
    /// 与回声节点的分工：回声吃时停 build，冷存储吃现货 RAM，两条静默路线
    /// </summary>
    internal class OldNetColdNodeTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //冰白偏青"实心霜壳"：区别回声节点的幽白残影感（无描边，走实心）
        private static readonly Color FrostShell = new(190, 230, 240);
        private static readonly Color FrostDeep = new(80, 130, 150);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //只允许右键回收，防镐子绕过账本
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(190, 230, 240), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetColdHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;
            OldNetPlayer session = OldNetPlayer.Get(player);
            Vector2 worldPos = new(i * 16 + 8, j * 16 + 8);

            int category = Main.rand.Next(SHPCData.SlotCount);
            int count = Main.rand.Next(OldNetMetrics.NodeShardMin, OldNetMetrics.NodeShardMax + 1)
                * OldNetMetrics.ColdShardMul;

            //先验容量再扣 RAM：满载拒收时一分钱都不能收
            if (session.PendingTotal + count > session.LedgerCapacity) {
                session.NotifyLedgerFull(worldPos);
                return true;
            }
            //TODO MP: 采集请求需走服务器授账（TryConsume 带权威守卫，MP 客户端直调必失败）
            if (!RamSystem.TryConsume(player, OldNetMetrics.ColdNodeRamCost)) {
                CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                    new Color(255, 120, 60), OldNetTexts.OldNetColdNoRam.Value);
                SoundEngine.PlaySound(CWRSound.Fault with { Volume = 0.4f }, worldPos);
                return true;
            }

            session.TryAddHarvest(category, count);
            Color color = SHPCModuleItem.SlotCategoryColor((SHPCSlotCategory)category);
            CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                color, OldNetTexts.OldNetHarvest.Format(count));
            //无声是卖点：不调 AddNoise，音效也压低（冰壳碎开的闷响）
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.4f, Volume = 0.4f }, worldPos);
            OldNetAbsorbFX.Emit(worldPos, color, count);

            WorldGen.KillTile(i, j, noItem: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            //冷 = 静止：不脉冲，恒定弱冷光
            r = 0.10f;
            g = 0.18f;
            b = 0.22f;
        }

        //实心霜壳方晶：不旋转不起伏（冷 = 静止），表面横向霜纹缓慢明灭
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 center = new Vector2(i * 16 + 8, j * 16 + 8) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            float seed = i * 0.61f + j * 0.37f;

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //底晕（静止斜置方，读作晶但不转）
            spriteBatch.Draw(px, center, null, FrostDeep * 0.30f,
                MathHelper.PiOver4, origin, Size(15f, 15f), SpriteEffects.None, 0f);
            //霜壳主体（实心，正置）
            spriteBatch.Draw(px, center, null, FrostShell * 0.80f, 0f,
                origin, Size(10f, 12f), SpriteEffects.None, 0f);
            //内芯沉色（壳里冻着东西）
            spriteBatch.Draw(px, center + new Vector2(0f, 1f), null, FrostDeep * 0.85f, 0f,
                origin, Size(6f, 7f), SpriteEffects.None, 0f);

            //三条横向霜纹：各自极慢明灭（唯一动态，读作低温呼吸）
            for (int k = 0; k < 3; k++) {
                float glow = 0.25f + 0.35f * (0.5f + 0.5f * MathF.Sin(t * 0.6f + seed + k * 2.1f));
                spriteBatch.Draw(px, center + new Vector2(0f, -3f + k * 3f), null,
                    Color.White * glow, 0f, origin, Size(8.5f, 0.9f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
