using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules;
using CalamityOverhaul.Content.Scenarios.OldNet.Gen;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldNet.Tiles
{
    /// <summary>
    /// 深潜缓存（M3 产出扩容）：衰减区限定的封存件箱。
    /// 撬开直接掉落一件 SHPC 模块，优先抽 CanGenerateInLabChest=false 的
    /// 深潜保留池（DESIGN 留的口子），保留池为空时退回全池；开箱有噪音
    /// </summary>
    internal class OldNetCacheTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private static readonly Color Amber = new(255, 180, 80);
        private static readonly Color DarkShell = new(24, 18, 14);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
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
            player.cursorItemIconText = OldNetTexts.OldNetCacheHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || !OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;
            OldNetPlayer session = OldNetPlayer.Get(player);

            //深潜保留池优先（CanGenerateInLabChest=false），空则全池兜底
            int[] pool = [.. ModContent.GetContent<SHPCModuleItem>()
                .Where(m => !m.CanGenerateInLabChest)
                .Select(m => m.Type)
                .OrderBy(t => t)];
            if (pool.Length == 0) {
                pool = [.. ModContent.GetContent<SHPCModuleItem>()
                    .Select(m => m.Type)
                    .OrderBy(t => t)];
            }
            if (pool.Length == 0) {
                CWRMod.Instance.Logger.Warn("[OldNet] 深潜缓存开箱：SHPC 模块池为空");
                return true;
            }

            int itemType = pool[Main.rand.Next(pool.Length)];
            var source = new EntitySource_TileInteraction(player, i, j);
            Item.NewItem(source, new Rectangle(i * 16, j * 16 - 8, 16, 16), itemType);

            //撬箱是响的：高值高噪
            session.AddNoise(OldNetMetrics.NoiseCacheOpen);
            SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.8f, Pitch = 0.1f },
                new Vector2(i, j) * 16f);
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.5f, Volume = 0.5f },
                new Vector2(i, j) * 16f);

            WorldGen.KillTile(i, j, noItem: true);
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.4f + i * 0.5f);
            r = 0.26f * pulse;
            g = 0.16f * pulse;
            b = 0.04f * pulse;
        }

        //封存件箱：暗壳方箱 + 琥珀封缝 + 锁芯呼吸，"箱"的读法区别于一切晶体节点
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 center = new Vector2(i * 16 + 8, j * 16 + 9) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height * 0.5f);
            float t = Main.GlobalTimeWrappedHourly;
            float pulse = 0.7f + 0.3f * MathF.Sin(t * 1.4f + i * 0.5f);

            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            //箱体（微沉到地）
            spriteBatch.Draw(px, center, null, DarkShell, 0f,
                origin, Size(15f, 11f), SpriteEffects.None, 0f);
            //顶盖受光线
            spriteBatch.Draw(px, center + new Vector2(0f, -5f), null, Amber * 0.45f, 0f,
                origin, Size(15f, 1.2f), SpriteEffects.None, 0f);
            //封缝：横向亮缝 + 中央锁芯
            spriteBatch.Draw(px, center + new Vector2(0f, -1f), null, Amber * (0.55f * pulse), 0f,
                origin, Size(13f, 1.4f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, center + new Vector2(0f, -1f), null, Color.White * (0.75f * pulse),
                MathHelper.PiOver4, origin, Size(3.4f, 3.4f), SpriteEffects.None, 0f);
            //四角铆点
            for (int cx = -1; cx <= 1; cx += 2) {
                for (int cy = -1; cy <= 1; cy += 2) {
                    spriteBatch.Draw(px, center + new Vector2(cx * 5.6f, cy * 3.6f), null,
                        Amber * 0.35f, 0f, origin, Size(1.6f, 1.6f), SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
