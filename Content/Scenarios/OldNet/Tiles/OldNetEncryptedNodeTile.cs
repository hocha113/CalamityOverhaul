using CalamityOverhaul.Content.LegendWeapon.SHPCLegend;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 加密节点：右键开始站桩引导（约 3s，期间高噪音、受击/出圈中断且节点保留），
    /// 完成后产出普通节点同分布 ×3。琥珀红双层菱晶区别于青色普通节点；
    /// 零贴图程序化绘制，引导反馈 = 向内收缩脉冲环 + 头顶进度弧
    /// </summary>
    internal class OldNetEncryptedNodeTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //琥珀红族：与普通节点的冷青一眼区分
        private static readonly Color AmberEdge = new(255, 150, 50);
        private static readonly Color EmberCore = new(235, 64, 44);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //只允许引导回收，防镐子绕过账本
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(255, 150, 50), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            //衰减区双态悬停（2.9）：疯域的锁会反咬，决策前就把代价讲清
            player.cursorItemIconText = i >= OldNetMetrics.FadeLeft
                ? OldNetTexts.OldNetEncryptFadeHint.Value
                : OldNetTexts.OldNetEncryptHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            OldNetPlayer.Get(Main.LocalPlayer).StartChannel(i, j);
            return true;
        }

        /// <summary>
        /// 引导完成结算（OldNetPlayer.TickChannel 调用，本机）：
        /// 普通节点同分布 ×3，过容量检查后消散；拒收则节点保留。
        /// 返回是否真正完成（满载拒收 false，衰减区余震据此不触发，失败的破解不该挨打）
        /// </summary>
        internal static bool CompleteHarvest(int i, int j, OldNetPlayer session) {
            int category = Main.rand.Next(SHPCData.SlotCount);
            int count = Main.rand.Next(OldNetMetrics.NodeShardMin, OldNetMetrics.NodeShardMax + 1)
                * OldNetMetrics.EncryptValueMul;
            if (!session.TryAddHarvest(category, count)) {
                session.NotifyLedgerFull(new Vector2(i * 16 + 8, j * 16 + 8));
                return false;
            }

            Color color = SHPCModuleItem.SlotCategoryColor((SHPCSlotCategory)category);
            CombatText.NewText(new Rectangle(i * 16, j * 16, 16, 16),
                color, OldNetTexts.OldNetHarvest.Format(count), dramatic: true);
            //完成音调比普通节点高
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.7f, Volume = 0.65f }, new Vector2(i, j) * 16f);
            OldNetAbsorbFX.Emit(new Vector2(i * 16 + 8, j * 16 + 8), color, count);

            WorldGen.KillTile(i, j, noItem: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.1f + (i * 7 + j * 13) * 0.7f);
            r = 0.36f * pulse;
            g = 0.16f * pulse;
            b = 0.05f * pulse;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            //缓存 RT 路径下 PreDraw 非逐帧，只登记特殊绘制点；登记/回退在 SpecialDraw
            //（防闪烁，引导进度读数也须逐帧刷新）
            Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.CustomNonSolid);
            return false;
        }

        public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch) {
            float seed = (i * 7 + j * 13) * 0.7f;
            float t = Main.GlobalTimeWrappedHourly;
            float bob = MathF.Sin(t * 1.1f + seed) * 1.5f;

            //本机引导状态（单人语义：绘制读本地玩家即可）
            OldNetPlayer session = OldNetPlayer.Get(Main.LocalPlayer);
            bool channeling = session.Channeling
                && session.ChannelNode.X == i && session.ChannelNode.Y == j;
            float progress = channeling ? session.ChannelProgress : 0f;

            //shader 路径：重写波前沿半径=引导进度，进度读数长在材质上
            if (Renders.OldNetTileFX.NodeShaderReady) {
                Renders.OldNetTileFX.Nodes.Add(new Renders.OldNetTileFX.NodeEntry {
                    Center = new Vector2(i * 16 + 8, j * 16 + 8 + bob),
                    Kind = 1,
                    Seed = seed,
                    Progress = progress,
                });
                return;
            }

            //CPU 回退：琥珀红双层菱晶 + 进度弧
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 center = new Vector2(i * 16 + 8, j * 16 + 8) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float pulse = 0.75f + 0.25f * MathF.Sin(t * 3.1f + seed);
            center.Y += bob;

            Vector2 Size(float s) => new(s / px.Width, s / px.Height);

            //外晕
            spriteBatch.Draw(px, center, null, AmberEdge * (0.20f * pulse),
                MathHelper.PiOver4, origin, Size(17f), SpriteEffects.None, 0f);
            //双层旋转菱晶：外琥珀内绯红，反向旋转读作"上锁"
            spriteBatch.Draw(px, center, null, AmberEdge * (0.7f * pulse),
                t * 0.8f + seed, origin, Size(10f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, center, null, EmberCore * (0.8f * pulse),
                -t * 1.1f + seed, origin, Size(6.5f), SpriteEffects.None, 0f);
            //核芯
            spriteBatch.Draw(px, center, null, Color.White * (0.9f * pulse),
                t * 1.6f + seed, origin, Size(2.8f), SpriteEffects.None, 0f);

            if (channeling) {
                //向内收缩的脉冲环：破解正在收拢
                float ringPhase = 1f - (t * 1.4f + seed) % 1f;
                float ringR = 6f + ringPhase * 18f;
                DrawDiamondOutline(spriteBatch, px, center, ringR,
                    AmberEdge * (0.65f * (1f - ringPhase)));

                //头顶进度弧：-135°→-45°，12 段
                const int segs = 12;
                const float radius = 17f;
                int lit = (int)MathF.Ceiling(progress * segs);
                for (int s = 0; s < segs; s++) {
                    float a0 = MathHelper.ToRadians(-135f) + s / (float)segs * MathHelper.ToRadians(90f);
                    Vector2 p = center + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
                    Color c = s < lit
                        ? Color.Lerp(AmberEdge, Color.White, 0.3f) * 0.95f
                        : AmberEdge * 0.2f;
                    spriteBatch.Draw(px, p, null, c, a0 + MathHelper.PiOver2,
                        origin, Size(3.4f), SpriteEffects.None, 0f);
                }
            }
        }

        //斜置正方形描边：4 条线段
        private static void DrawDiamondOutline(SpriteBatch sb, Texture2D px, Vector2 center,
            float radius, Color color) {
            Vector2[] corners = new Vector2[4];
            for (int k = 0; k < 4; k++) {
                float ang = MathHelper.PiOver2 * k + MathHelper.PiOver4;
                corners[k] = center + new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * radius;
            }
            for (int k = 0; k < 4; k++) {
                Vector2 a = corners[k];
                Vector2 b = corners[(k + 1) % 4];
                Vector2 diff = b - a;
                sb.Draw(px, a, new Rectangle(0, 0, 1, 1), color, diff.ToRotation(),
                    new Vector2(0f, 0.5f), new Vector2(diff.Length(), 1.2f), SpriteEffects.None, 0f);
            }
        }
    }
}
