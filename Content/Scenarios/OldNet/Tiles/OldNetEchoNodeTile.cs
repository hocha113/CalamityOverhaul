using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using CalamityOverhaul.Content.TimeFreezes;
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
    /// 回声节点（M3 回声考古）：过去数据的残影，常态只剩幽淡轮廓不可触及；
    /// 时停中显影为实体，可右键回收，产出 ×2 且零噪音，
    /// 是"时停考古低噪路线"（NoiseFreezeMul）的正面报偿
    /// </summary>
    internal class OldNetEchoNodeTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //幽白青：回声的光谱色，区别于普通节点的实体冷青
        private static readonly Color SpectralWhite = new(210, 240, 250);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(210, 240, 250), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetEchoHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;
            OldNetPlayer session = OldNetPlayer.Get(player);

            //常态触不到：回声只活在时停里
            if (!WorldFreezeSystem.IsActive) {
                CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                    new Color(150, 160, 175), OldNetTexts.OldNetEchoFizzle.Value);
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.4f, Pitch = -0.7f },
                    new Vector2(i, j) * 16f);
                return true;
            }

            int category = Main.rand.Next(SHPCData.SlotCount);
            int count = Main.rand.Next(OldNetMetrics.NodeShardMin, OldNetMetrics.NodeShardMax + 1)
                * OldNetMetrics.EchoShardMul;
            if (!session.TryAddHarvest(category, count)) {
                session.NotifyLedgerFull(new Vector2(i * 16 + 8, j * 16 + 8));
                return true;
            }

            Color color = SHPCModuleItem.SlotCategoryColor((SHPCSlotCategory)category);
            CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                color, OldNetTexts.OldNetHarvest.Format(count));
            //回声回收音：低哑倒放感，注意零噪音，这是低噪路线的糖
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = -0.45f, Volume = 0.55f },
                new Vector2(i, j) * 16f);
            OldNetAbsorbFX.Emit(new Vector2(i * 16 + 8, j * 16 + 8), color, count);

            WorldGen.KillTile(i, j, noItem: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            //时停中显影更亮
            float vis = WorldFreezeSystem.IsActive ? 1f : 0.3f;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.2f + i * 0.7f);
            r = 0.16f * pulse * vis;
            g = 0.22f * pulse * vis;
            b = 0.25f * pulse * vis;
        }

        //回声=轮廓幽灵：常态只有描边残影，时停中补上体积，刻意不走节点 shader（材质是"残影"不是晶体）
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
            bool frozen = WorldFreezeSystem.IsActive;
            //显影强度：时停中满强，常态幽淡且缓慢明灭
            float vis = frozen ? 1f : 0.16f + 0.08f * MathF.Sin(t * 0.7f + seed);
            center.Y += MathF.Sin(t * 0.9f + seed) * 1.2f;

            Vector2 Size(float s) => new(s / px.Width, s / px.Height);

            //轮廓菱形描边（残影的骨）
            DrawDiamondOutline(spriteBatch, px, center, 9f, SpectralWhite * (0.75f * vis));
            DrawDiamondOutline(spriteBatch, px, center, 5.5f, SpectralWhite * (0.5f * vis));

            if (frozen) {
                //时停显影：体积补全 + 白芯
                spriteBatch.Draw(px, center, null, SpectralWhite * 0.30f,
                    MathHelper.PiOver4, origin, Size(12f), SpriteEffects.None, 0f);
                spriteBatch.Draw(px, center, null, Color.White * 0.85f,
                    -t * 1.2f + seed, origin, Size(3.2f), SpriteEffects.None, 0f);
            }
            return false;
        }

        private static void DrawDiamondOutline(SpriteBatch sb, Texture2D px, Vector2 center,
            float radius, Color color) {
            for (int k = 0; k < 4; k++) {
                float ang = MathHelper.PiOver2 * k + MathHelper.PiOver4;
                Vector2 a = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
                Vector2 b = center + new Vector2(
                    MathF.Cos(ang + MathHelper.PiOver2), MathF.Sin(ang + MathHelper.PiOver2)) * radius;
                Vector2 diff = b - a;
                sb.Draw(px, a, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                    new Vector2(0f, 0.5f), new Vector2(diff.Length(), 1.1f), SpriteEffects.None, 0f);
            }
        }
    }
}
