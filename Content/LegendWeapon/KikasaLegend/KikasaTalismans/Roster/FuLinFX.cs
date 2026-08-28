using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaRains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaTalismans.Roster
{
    /// <summary>霖的演出集中处：伞下雨丝帘与霖成边沿，全部纯表现（各客户端本地）</summary>
    internal static class FuLinFX
    {
        /// <summary>找归属玩家当前的悬伞</summary>
        internal static Projectile FindUmbrella(Player owner) {
            if (owner?.active != true) {
                return null;
            }
            int type = ModContent.ProjectileType<KikasaRainUmbrella>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == owner.whoAmI && proj.type == type) {
                    return proj;
                }
            }
            return null;
        }

        /// <summary>
        /// 伞下雨丝帘：伞缘随机位垂下发丝细雨，密度随连绵度渐涨，霖成再添一缕。
        /// 帘就是连绵读数本身，不另设 UI 条
        /// </summary>
        internal static void DrizzleVeil(Player owner, float meter, bool steady, Color accent) {
            Projectile umbrella = FindUmbrella(owner);
            if (umbrella == null) {
                return;
            }
            //密度曲线：起步稀疏，满档约每帧一缕
            if (Main.rand.NextFloat() < 0.12f + 0.55f * meter) {
                SpawnStrand(umbrella, accent, steady);
            }
            if (steady && Main.rand.NextBool(3)) {
                SpawnStrand(umbrella, accent, true);
            }
        }

        /// <summary>一缕雨丝：无重力短寿细线，随速拉伸直坠</summary>
        private static void SpawnStrand(Projectile umbrella, Color accent, bool bright) {
            Vector2 pos = umbrella.Center
                + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(0f, 6f));
            PRTLoader.NewParticle<PRT_Line>(pos,
                new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(4.2f, 6.8f)),
                Color.Lerp(accent, Color.White, bright ? 0.45f : 0.2f)
                    * Main.rand.NextFloat(0.3f, bright ? 0.6f : 0.45f),
                Main.rand.NextFloat(0.3f, 0.5f))?.Configure(false, Main.rand.Next(9, 14));
        }

        /// <summary>霖成边沿：伞缘荡一圈细珠+雨声上一档，各端本地</summary>
        internal static void SteadyRainSet(Player owner, Color accent) {
            if (Main.dedServ) {
                return;
            }
            Projectile umbrella = FindUmbrella(owner);
            Vector2 at = umbrella?.Center ?? owner.Top - Vector2.UnitY * 40f;
            KikasaInk.Play(KikasaInk.InkSplash, at, 0.4f, 0.35f, 3);
            for (int i = 0; i < 10; i++) {
                Vector2 dir = (MathHelper.TwoPi * i / 10f + 0.2f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_KikasaInkBead>(at + dir * 26f,
                    dir * Main.rand.NextFloat(1.2f, 2.2f) + Vector2.UnitY * 0.6f,
                    Color.Lerp(accent, Color.White, 0.3f),
                    Main.rand.NextFloat(0.16f, 0.26f))?.Configure(Main.rand.Next(14, 22));
            }
        }
    }
}
