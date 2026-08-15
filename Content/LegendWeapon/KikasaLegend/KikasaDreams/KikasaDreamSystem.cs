using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>鬼梦表现泵（延迟雷、梦中氛围：烬灰/潮雾）</summary>
    internal class KikasaDreamSystem : ModSystem
    {
        public override void PostUpdateEverything() {
            if (Main.dedServ) {
                return;
            }
            KikasaDreamFX.Update();
            UpdateDreamAmbience();
        }

        public override void ClearWorld() {
            if (!Main.dedServ) {
                KikasaDreamFX.Clear();
            }
        }

        /// <summary>梦中常驻表现：飘浮烬灰 + 贴地潮雾，强度吃观看域的 DreamBlend</summary>
        private static void UpdateDreamAmbience() {
            if (Main.gameMenu) {
                return;
            }
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            float dream = viewed?.DreamBlend ?? 0f;
            if (dream < 0.4f) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }

            //烬灰：横跨屏幕的稀疏黑红碎屑缓缓上浮，偶有一粒还烧着
            if (Main.GameUpdateCount % 3 == 0) {
                float x = player.Center.X + Main.rand.NextFloat(
                    -Main.screenWidth * 0.6f - 120f, Main.screenWidth * 0.6f + 120f);
                float y = player.Center.Y + Main.rand.NextFloat(
                    -Main.screenHeight * 0.55f, Main.screenHeight * 0.45f);
                bool isEmber = Main.rand.NextBool(7);
                Color color = isEmber
                    ? new Color(214, 84, 34)
                    : new Color(34, 12, 14);
                Vector2 vel = new(Main.rand.NextFloat(-0.2f, 0.2f),
                    Main.rand.NextFloat(-0.55f, -0.2f));
                PRTLoader.NewParticle<PRT_KikasaDreamAsh>(new Vector2(x, y), vel,
                    color * dream, Main.rand.NextFloat(0.10f, 0.22f))
                    ?.Configure(Main.rand.Next(120, 200), isEmber);
            }

            //贴地潮雾：暗红色的低雾趴在地表
            if (Main.GameUpdateCount % 6 == 0) {
                float x = player.Center.X + Main.rand.NextFloat(
                    -Main.screenWidth * 0.55f - 200f, Main.screenWidth * 0.55f + 200f);
                if (TryFindGround(x, player.Center.Y - 60f, out float groundY)) {
                    PRTLoader.NewParticle<PRT_GhostRainMist>(
                        new Vector2(x, groundY - Main.rand.NextFloat(6f, 36f)),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.06f, 0f)),
                        new Color(58, 18, 20) * (0.9f * dream),
                        Main.rand.NextFloat(0.7f, 1.2f))
                        ?.Configure(Main.rand.Next(90, 150));
                }
            }
        }

        /// <summary>从起始高度向下探地表</summary>
        private static bool TryFindGround(float x, float fromY, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < 46; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 40)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile && Main.tileSolid[tile.TileType]
                    && !Main.tileSolidTop[tile.TileType]) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        /// <summary>拒绝反馈：轻点一声，别让按键静默吞掉</summary>
        internal static void Refuse(Player player) {
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 }, player.Center);
        }
    }
}
