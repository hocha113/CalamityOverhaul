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
    /// 旧网接入终端：主世界侧的标准深潜入口（坠舱中舱随世界生成）。
    /// 两次交互确认制，首次预热链路，5 秒内再次交互越墙深潜；
    /// 深潜仅单人模式开放（旧网系统单人优先，MP 后置）
    /// </summary>
    internal class OldNetAccessTerminalTile : ModTile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        //确认窗口：首次交互后 5 秒内再次交互才深潜
        private const int ConfirmWindowTicks = 300;
        private static int armedUntilTick;
        private static Point armedAt = new(-1, -1);

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileSolid[Type] = false;
            Main.tileLavaDeath[Type] = false;
            Main.tileLighted[Type] = true;
            //入口锚点不可破坏
            MinPick = 999;
            MineResist = 10f;
            AddMapEntry(new Color(140, 200, 210), CreateMapEntryName());
        }

        public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;

        public override void MouseOver(int i, int j) {
            Player player = Main.LocalPlayer;
            player.noThrow = 2;
            player.cursorItemIconEnabled = true;
            player.cursorItemIconID = 0;
            player.cursorItemIconText = OldNetTexts.OldNetEnterHint.Value;
        }

        public override bool RightClick(int i, int j) {
            if (Main.netMode == NetmodeID.Server || OldNetWorld.Active) {
                return false;
            }
            Player player = Main.LocalPlayer;

            //单人门禁：旧网系统单人优先（与 /oldnet 同口径）
            if (Main.netMode != NetmodeID.SinglePlayer) {
                CombatText.NewText(player.getRect(), Color.IndianRed, OldNetTexts.OldNetEnterSPOnly.Value);
                SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = -0.6f }, player.Center);
                return true;
            }

            int now = (int)Main.GameUpdateCount;
            bool armed = armedAt.X == i && armedAt.Y == j && now < armedUntilTick;
            if (!armed) {
                //首次交互：预热链路，武装确认窗口
                armedAt = new Point(i, j);
                armedUntilTick = now + ConfirmWindowTicks;
                CombatText.NewText(player.getRect(), new Color(140, 200, 210),
                    OldNetTexts.OldNetEnterConfirm.Value, dramatic: true);
                SoundEngine.PlaySound(SoundID.Item15 with { Volume = 0.6f, Pitch = -0.3f },
                    new Vector2(i, j) * 16f);
                return true;
            }

            //确认交互：越墙深潜
            armedAt = new Point(-1, -1);
            SoundEngine.PlaySound(SoundID.Item78 with { Volume = 0.7f, Pitch = -0.2f }, player.Center);
            OldNetWorld.EnterWorld();
            return true;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 1.8f);
            r = 0.22f * pulse;
            g = 0.40f * pulse;
            b = 0.44f * pulse;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            //shader 路径：上行天线柱（与旧网内登出终端同语汇，两端是同一条链路）
            if (Renders.OldNetTileFX.TerminalShaderReady) {
                Renders.OldNetTileFX.Columns.Add(new Renders.OldNetTileFX.ColumnEntry {
                    BasePos = new Vector2(i * 16 + 8, j * 16 + 16),
                    Relay = false,
                    Seed = i * 0.53f,
                });
                return false;
            }

            //CPU 回退：冷青三层光柱
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return false;
            }
            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 basePos = new Vector2(i * 16 + 8, j * 16 + 16) - Main.screenPosition + offset;
            Vector2 origin = new(px.Width * 0.5f, px.Height);
            float t = Main.GlobalTimeWrappedHourly;
            float pulse = 0.8f + 0.2f * MathF.Sin(t * 1.8f);
            Color accent = new(140, 200, 210);
            Vector2 Size(float w, float h) => new(w / px.Width, h / px.Height);

            spriteBatch.Draw(px, basePos, null, accent * 0.10f, 0f, origin,
                Size(22f, 120f * pulse), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, accent * (0.28f * pulse), 0f, origin,
                Size(10f, 88f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, Color.White * (0.7f * pulse), 0f, origin,
                Size(3f, 58f), SpriteEffects.None, 0f);
            spriteBatch.Draw(px, basePos, null, accent * 0.9f, 0f, origin,
                Size(18f, 4f), SpriteEffects.None, 0f);
            return false;
        }
    }
}
