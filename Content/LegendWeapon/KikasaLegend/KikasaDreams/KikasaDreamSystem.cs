using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦地雾的驱散场。恶犬/玩家/光标每帧登记为排斥源（x,y=源心 z=半径 w=推力/帧），
    /// 雾粒子（<see cref="PRT_KikasaDreamFog"/>）在 AI 里读取让位；
    /// Active=false 时在场雾加速收场，归返后的真实世界不留梦雾
    /// </summary>
    internal static class KikasaDreamFogField
    {
        internal static readonly List<Vector4> Repulsors = new();

        /// <summary>梦侧是否仍在供雾</summary>
        internal static bool Active { get; private set; }

        internal static void Rebuild(Player viewer) {
            Repulsors.Clear();
            Active = true;
            //人拨雾、准星处让位：走路与瞄准的可读窗
            Repulsors.Add(new Vector4(viewer.Center.X, viewer.Center.Y, 120f, 0.085f));
            Vector2 mouse = Main.MouseWorld;
            Repulsors.Add(new Vector4(mouse.X, mouse.Y, 90f, 0.07f));
            //在场恶犬：狗趟过雾，雾从狗身边分开
            int houndType = ModContent.ProjectileType<KikasaDreamHound>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == houndType) {
                    Repulsors.Add(new Vector4(proj.Center.X, proj.Center.Y, 150f, 0.10f));
                }
            }
        }

        internal static void Clear() {
            Repulsors.Clear();
            Active = false;
        }
    }

    /// <summary>鬼梦表现泵（延迟雷、梦中氛围：烬灰/贴地雾毯）</summary>
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
                KikasaDreamFogField.Clear();
            }
        }

        /// <summary>梦中常驻表现：飘浮烬灰 + 贴地雾毯，强度吃观看域的 DreamBlend</summary>
        private static void UpdateDreamAmbience() {
            if (Main.gameMenu) {
                KikasaDreamFogField.Clear();
                return;
            }
            KikasaDomainPlayer viewed = KikasaDomain.Viewed;
            float dream = viewed?.DreamBlend ?? 0f;
            if (dream < 0.4f) {
                KikasaDreamFogField.Clear();
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                KikasaDreamFogField.Clear();
                return;
            }

            //驱散场每帧重建，源位置跟着实体走
            KikasaDreamFogField.Rebuild(player);

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

            //贴地雾毯：宽扁低雾沿地表连成带、随梦里的死风缓缓爬行。
            //旧潮雾（%6、色比地面还暗）读不出来；提密提亮后靠驱散场保可读，
            //主体深红灰，偶有一缕吃到红天光的暖亮缘，亮雾衬暗地才分得出层
            if (Main.GameUpdateCount % 2 == 0) {
                float x = player.Center.X + Main.rand.NextFloat(
                    -Main.screenWidth * 0.6f - 200f, Main.screenWidth * 0.6f + 200f);
                if (TryFindGround(x, player.Center.Y - 60f, out float groundY)) {
                    bool lit = Main.rand.NextBool(6);
                    Color color = lit ? new Color(152, 74, 60) : new Color(92, 42, 40);
                    float wind = MathF.Sin(Main.worldID % 255 * 0.37f) * 0.8f;
                    PRTLoader.NewParticle<PRT_KikasaDreamFog>(
                        new Vector2(x, groundY - Main.rand.NextFloat(2f, 18f)),
                        new Vector2(wind * Main.rand.NextFloat(0.5f, 1f), 0f),
                        color * (dream * Main.rand.NextFloat(0.75f, 1f)),
                        Main.rand.NextFloat(0.55f, 1.0f))
                        ?.Configure(Main.rand.Next(130, 210), groundY, wind);
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
