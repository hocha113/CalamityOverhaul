using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using InnoVault.PRT;
using System;
using Terraria;

namespace CalamityOverhaul.Content.Scenarios.Kiyume
{
    /// <summary>
    /// 鬼梦常驻氛围粒子：飘浮烬灰 + 雾面浮丝。<br/>
    /// 烬灰照搬鬼伞鬼梦相位的用法；潮雾换了落点，鬼伞那套贴地面，
    /// 这里贴<b>雾面</b>，浮丝顺着那条水位横漂，是"雾有表面"在粒子层的第二遍陈述。<br/>
    /// 纯客户端，强度吃 <see cref="KiyumeAmbienceSystem.Presence"/>
    /// </summary>
    internal static class KiyumeAmbienceFX
    {
        internal static void Update() {
            float presence = KiyumeAmbienceSystem.Presence;
            if (presence < 0.25f || Main.gameMenu) {
                return;
            }
            Player player = Main.LocalPlayer;
            if (player?.active != true) {
                return;
            }

            SpawnAsh(player, presence);
            SpawnSurfaceWisp(player, presence);
        }

        //烬灰：横跨屏幕的稀疏黑红碎屑缓缓上浮，偶有一粒还烧着
        private static void SpawnAsh(Player player, float presence) {
            if (Main.GameUpdateCount % 3 != 0) {
                return;
            }
            float x = player.Center.X + Main.rand.NextFloat(
                -Main.screenWidth * 0.6f - 120f, Main.screenWidth * 0.6f + 120f);
            float y = player.Center.Y + Main.rand.NextFloat(
                -Main.screenHeight * 0.55f, Main.screenHeight * 0.45f);
            bool isEmber = Main.rand.NextBool(7);
            Color color = isEmber ? new Color(214, 84, 34) : new Color(34, 12, 14);
            Vector2 vel = new(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.55f, -0.2f));
            PRTLoader.NewParticle<PRT_KikasaDreamAsh>(new Vector2(x, y), vel,
                color * presence, Main.rand.NextFloat(0.10f, 0.22f))
                ?.Configure(Main.rand.Next(120, 200), isEmber);
        }

        //雾面浮丝：只在那条水位上下几十像素内生成，顺着面横漂，让雾面在近处也看得见
        private static void SpawnSurfaceWisp(Player player, float presence) {
            if (Main.GameUpdateCount % 4 != 0) {
                return;
            }
            float x = player.Center.X + Main.rand.NextFloat(
                -Main.screenWidth * 0.55f - 180f, Main.screenWidth * 0.55f + 180f);
            float surface = KiyumeFogTide.SurfaceAt(x);
            //雾面跑出屏外就别生成了，屏幕中央看不见的粒子是白烧
            if (MathF.Abs(surface - player.Center.Y) > Main.screenHeight * 0.7f) {
                return;
            }
            float y = surface + Main.rand.NextFloat(-26f, 42f);
            PRTLoader.NewParticle<PRT_GhostRainMist>(new Vector2(x, y),
                new Vector2(Main.rand.NextFloat(-0.45f, 0.45f), Main.rand.NextFloat(-0.05f, 0.03f)),
                new Color(74, 24, 24) * (0.95f * presence),
                Main.rand.NextFloat(0.85f, 1.5f))
                ?.Configure(Main.rand.Next(110, 190));
        }
    }
}
