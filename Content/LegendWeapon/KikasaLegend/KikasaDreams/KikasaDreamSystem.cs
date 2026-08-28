using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDreams
{
    /// <summary>
    /// 鬼梦地雾的驱散场。恶犬/玩家/光标每帧登记为排斥源（x,y=世界源心 z=半径px w=孔强01），
    /// 雾带着色器（<see cref="KikasaDreamFogRender"/> 喂进 uRepulse[6]）在源处让净、孔缘微堆。
    /// 槽位超出着色器 6 个上限时按登记序截断：玩家与光标先登记，恒不掉
    /// </summary>
    internal static class KikasaDreamFogField
    {
        internal static readonly List<Vector4> Repulsors = new();

        internal static void Rebuild(Player viewer) {
            Repulsors.Clear();
            //人拨雾、准星处让位：走路与瞄准的可读窗
            Repulsors.Add(new Vector4(viewer.Center.X, viewer.Center.Y, 120f, 0.95f));
            Vector2 mouse = Main.MouseWorld;
            Repulsors.Add(new Vector4(mouse.X, mouse.Y, 90f, 0.85f));
            //在场恶犬：狗趟过雾从身边分开，身后拖一个小尾点，冲刺读出雾道
            int houndType = ModContent.ProjectileType<KikasaDreamHound>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type != houndType) {
                    continue;
                }
                Repulsors.Add(new Vector4(proj.Center.X, proj.Center.Y, 150f, 1f));
                if (proj.velocity.LengthSquared() > 4f) {
                    Vector2 tail = proj.Center - proj.velocity * 8f;
                    Repulsors.Add(new Vector4(tail.X, tail.Y, 100f, 0.8f));
                }
            }
        }

        internal static void Clear() => Repulsors.Clear();
    }

    /// <summary>鬼梦表现泵（延迟雷、梦中氛围：烬灰/贴地雾毯）</summary>
    internal class KikasaDreamSystem : ModSystem, ICWRLoader
    {
        void ICWRLoader.UnLoadData() => KikasaDreamGroundField.Unload();

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
                KikasaDreamGroundField.Reset();
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

            //贴地雾毯本体已改走连续雾场（KikasaDreamGroundField 距离场 + KikasaDreamFog.fx）：
            //粒子堆叠的生灭错相会读成闪烁，这里只负责重建驱散场，不再撒雾粒
        }

        /// <summary>拒绝反馈：轻点一声，别让按键静默吞掉</summary>
        internal static void Refuse(Player player) {
            SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.55f, Pitch = -0.7f, MaxInstances = 2 }, player.Center);
        }
    }
}
