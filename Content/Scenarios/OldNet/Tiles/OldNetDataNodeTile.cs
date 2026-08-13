using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
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
    /// 数据节点：旧网的基础采集点。右键回收 1-3 枚随机类别模具碎片进未铭刻账本，
    /// 节点随即消散。零贴图：占位纹理 + 程序化旋转菱晶绘制
    /// </summary>
    internal class OldNetDataNodeTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //不可采掘：只允许右键回收，防止镐子路径绕过账本
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(0, 220, 255), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetNodeHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server) {
                return false;
            }
            Player player = Main.LocalPlayer;
            OldNetPlayer session = OldNetPlayer.Get(player);

            int category = Main.rand.Next(SHPCData.SlotCount);
            int count = Main.rand.Next(OldNetMetrics.NodeShardMin, OldNetMetrics.NodeShardMax + 1);
            //满载拒收：节点保留不消散（M1b 容量决策）
            if (!session.TryAddHarvest(category, count)) {
                session.NotifyLedgerFull(new Vector2(i * 16 + 8, j * 16 + 8));
                return true;
            }

            Color color = SHPCModuleItem.SlotCategoryColor((SHPCSlotCategory)category);
            CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                color, OldNetTexts.OldNetHarvest.Format(count));
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.35f, Volume = 0.6f }, new Vector2(i, j) * 16f);
            //采集噪音 + 吸收演出
            session.AddNoise(OldNetMetrics.NoiseHarvest);
            OldNetAbsorbFX.Emit(new Vector2(i * 16 + 8, j * 16 + 8), color, count);

            //回收即消散
            WorldGen.KillTile(i, j, noItem: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f + (i * 7 + j * 13) * 0.7f);
            r = 0.05f * pulse;
            g = 0.28f * pulse;
            b = 0.34f * pulse;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 center = new Vector2(i * 16 + 8, j * 16 + 8) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);

            float seed = (i * 7 + j * 13) * 0.7f;
            float t = Main.GlobalTimeWrappedHourly;
            float pulse = 0.75f + 0.25f * MathF.Sin(t * 2.4f + seed);
            float bob = MathF.Sin(t * 1.3f + seed) * 1.5f;
            center.Y += bob;

            Color accent = new(0, 220, 255);
            //占位纹理尺寸未知，按轴归一化到目标像素尺寸
            Vector2 Size(float s) => new(s / px.Width, s / px.Height);

            //外晕（斜置正方形读作菱晶）
            spriteBatch.Draw(px, center, null, accent * (0.22f * pulse),
                MathHelper.PiOver4, origin, Size(15f), SpriteEffects.None, 0f);
            //旋转中层
            spriteBatch.Draw(px, center, null, accent * (0.65f * pulse),
                t * 0.9f + seed, origin, Size(8.5f), SpriteEffects.None, 0f);
            //反向内核
            spriteBatch.Draw(px, center, null, Color.White * (0.85f * pulse),
                -t * 1.4f + seed, origin, Size(3.5f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
