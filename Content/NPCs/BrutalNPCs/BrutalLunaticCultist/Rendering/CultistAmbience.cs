using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering
{
    /// <summary>
    /// 分相环境层(各端本地演出量,无网络):每相一套 风场+灰烬+风暴流线+云絮 的配比,天幕极光帘由同一股风驱动<br/>
    /// 星旋=墨云奔流+雨丝+远雷;星云=紫灰烬缓落+薄紫雾;星尘=晶尘斜落+冰丝;日耀=炽缘灰烬雨+余烬上浮+热烟;月明=冷灰烬雪+死雾<br/>
    /// 粒子全部在屏幕系生成(顶缘/上风缘),只在本机玩家身处场域附近时生成,远处玩家的屏幕不下灰
    /// </summary>
    internal static class CultistAmbience
    {
        /// <summary>生成门:本机玩家离场心超此距离不生成(黄道环半径+一屏余量)</summary>
        private const float SpawnRange = CultistStateContext.ArenaRadius + 900f;

        internal static void Update(CultistStateContext context) {
            if (VaultUtils.isServer || context == null || !context.ArenaSpawned || Main.gameMenu) {
                return;
            }
            Vector2 arena = context.ArenaCenter;
            if (Main.LocalPlayer.Distance(arena) > SpawnRange) {
                return;
            }
            float gustBoost = 1f + CultistScreenFX.Gust * 1.5f;

            switch (context.Phase) {
                case 0:
                    UpdateVortex(arena, gustBoost);
                    break;
                case 1:
                    UpdateNebula(gustBoost);
                    break;
                case 2:
                    UpdateStardust(gustBoost);
                    break;
                case 3:
                    UpdateSolar(arena, gustBoost);
                    break;
                default:
                    if (context.Phase >= 4) {
                        UpdateMoon(arena, gustBoost);
                    }
                    break;
            }
        }

        /// <summary>星旋:风暴相。墨云絮沿上半屏奔流,雨丝横掠,风吹碎屑,远雷白闪+闷雷;云盖涌激常驻不落底</summary>
        private static void UpdateVortex(Vector2 arena, float gustBoost) {
            CultistScreenFX.SetWind(3.2f);
            CultistScreenFX.StormSurge = MathHelper.Max(CultistScreenFX.StormSurge, 0.35f);
            if (Main.rand.NextBool(200)) {
                CultistScreenFX.PushFlash(0.10f + Main.rand.NextFloat(0.08f));
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.35f, Pitch = -0.5f },
                    arena + Main.rand.NextVector2Circular(900f, 500f));
            }
            if (Main.rand.NextBool(24)) {
                SpawnWisp(new Color(24, 34, 48), Main.rand.NextFloat(0.16f, 0.24f), Main.rand.NextFloat(0.7f, 1.3f),
                    Main.rand.Next(140, 200), 0f, 0.55f);
            }
            if (Chance(0.25f * gustBoost)) {
                SpawnStreak(new Color(150, 190, 215), Main.rand.NextFloat(0.9f, 1.4f), Main.rand.Next(30, 50));
            }
            if (Main.rand.NextBool(12)) {
                SpawnAsh(new Color(70, 85, 100), 0.8f, null, 0.9f, 1.5f, 90, 150);
            }
        }

        /// <summary>星云:阴森去饱和垫底,紫灰烬缓缓落,薄紫雾整屏缓漂,偶有淡紫丝</summary>
        private static void UpdateNebula(float gustBoost) {
            CultistScreenFX.SetWind(1.3f);
            CultistScreenFX.BreakDesat = MathHelper.Max(CultistScreenFX.BreakDesat, 0.16f);
            if (Main.rand.NextBool(7)) {
                SpawnAsh(new Color(98, 74, 112), 0.7f, null, 0.9f, 1.6f, 110, 170);
            }
            if (Main.rand.NextBool(40)) {
                SpawnWisp(new Color(60, 20, 70), Main.rand.NextFloat(0.10f, 0.15f), Main.rand.NextFloat(0.9f, 1.5f),
                    Main.rand.Next(160, 220), 0f, 1f);
            }
            if (Chance(0.07f * gustBoost)) {
                SpawnStreak(new Color(230, 150, 220), Main.rand.NextFloat(0.7f, 1.1f), Main.rand.Next(36, 60));
            }
        }

        /// <summary>星尘:晶尘随风斜落,冷灰烬点缀,冰丝横掠</summary>
        private static void UpdateStardust(float gustBoost) {
            CultistScreenFX.SetWind(1.6f);
            if (Main.rand.NextBool(5)) {
                Vector2 pos = TopEdge();
                PRTLoader.NewParticle<PRT_CultistFrostMote>(pos,
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(1.2f, 2.0f)),
                    Color.Lerp(CultistMotion.StardustCore, CultistMotion.StardustEdge, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.5f, 1.0f))?.Configure(Main.rand.Next(80, 140), 1f);
            }
            if (Main.rand.NextBool(10)) {
                SpawnAsh(new Color(120, 140, 160), 0.9f, null, 0.8f, 1.4f, 100, 160);
            }
            if (Chance(0.12f * gustBoost)) {
                SpawnStreak(new Color(170, 225, 240), Main.rand.NextFloat(0.8f, 1.2f), Main.rand.Next(30, 52));
            }
        }

        /// <summary>日耀:炙烤相。暖幕常驻,余烬上浮,炽缘灰烬像雨一样落,火线横掠,热烟沿上半屏滚</summary>
        private static void UpdateSolar(Vector2 arena, float gustBoost) {
            CultistScreenFX.SetWind(2.4f);
            CultistScreenFX.SetVeil(0.22f, arena, CultistMotion.SolarEdge, 1100f);
            if (Main.rand.NextBool(4)) {
                Vector2 pos = Main.screenPosition + new Vector2(Main.rand.NextFloat(Main.screenWidth), Main.screenHeight + 16f);
                PRTLoader.NewParticle<PRT_CultistEmber>(pos,
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1.6f, 3.4f)),
                    Color.Lerp(CultistMotion.SolarCore, CultistMotion.SolarEdge, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.7f, 1.4f))?.Configure(Main.rand.Next(60, 110), 0.02f, 1f);
            }
            if (Main.rand.NextBool(5)) {
                SpawnAsh(new Color(38, 26, 24), 1.2f, new Color(255, 110, 30), 1.0f, 1.8f, 90, 150);
            }
            if (Chance(0.11f * gustBoost)) {
                SpawnStreak(new Color(255, 170, 90), Main.rand.NextFloat(0.8f, 1.3f), Main.rand.Next(28, 46));
            }
            if (Main.rand.NextBool(36)) {
                SpawnWisp(new Color(50, 28, 20), Main.rand.NextFloat(0.12f, 0.18f), Main.rand.NextFloat(0.8f, 1.4f),
                    Main.rand.Next(130, 190), 0f, 0.6f);
            }
        }

        /// <summary>月明:死寂相。冷暗压场+低鸣,冷灰烬像雪一样落,死雾缓卷,偶有苍白丝</summary>
        private static void UpdateMoon(Vector2 arena, float gustBoost) {
            CultistScreenFX.SetWind(1.9f);
            CultistScreenFX.BreakDesat = MathHelper.Max(CultistScreenFX.BreakDesat, 0.10f);
            CultistScreenFX.SetVeil(0.18f, arena, CultistMotion.MoonCore, 1200f);
            if (Main.GameUpdateCount % 300 == 0) {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.4f, Pitch = -0.9f }, arena);
            }
            if (Main.rand.NextBool(6)) {
                SpawnAsh(new Color(150, 168, 160), 0.75f, null, 0.9f, 1.6f, 120, 180);
            }
            if (Main.rand.NextBool(34)) {
                SpawnWisp(new Color(30, 44, 42), Main.rand.NextFloat(0.10f, 0.16f), Main.rand.NextFloat(0.9f, 1.6f),
                    Main.rand.Next(160, 230), 0f, 1f);
            }
            if (Chance(0.06f * gustBoost)) {
                SpawnStreak(new Color(180, 230, 210), Main.rand.NextFloat(0.7f, 1.1f), Main.rand.Next(36, 60));
            }
        }

        #region 生成
        //环境粒子的寿命远短于横越一屏所需帧数(灰烬 1px/f 落 150 帧只走 150px),
        //所以一律屏内随机点起生,靠缓显缓隐遮住出生/消亡,读作"整屏都在飘",而不是只有一缘在下
        private static bool Chance(float probability) => Main.rand.NextFloat() < probability;

        /// <summary>屏幕顶缘随机点(含两侧余量):供落速快、寿命够穿屏的粒子(晶尘)</summary>
        private static Vector2 TopEdge() =>
            Main.screenPosition + new Vector2(Main.rand.NextFloat(-120f, Main.screenWidth + 120f), -20f);

        /// <summary>屏内随机点(含余量),yMin/yMax 为屏高比例</summary>
        private static Vector2 ScreenPoint(float yMin, float yMax) =>
            Main.screenPosition + new Vector2(Main.rand.NextFloat(-100f, Main.screenWidth + 100f),
                Main.rand.NextFloat(yMin, yMax) * Main.screenHeight);

        /// <summary>灰烬片:屏内起生,随风缓落</summary>
        private static void SpawnAsh(Color tint, float fall, Color? rim, float scaleMin, float scaleMax, int lifeMin, int lifeMax) {
            Vector2 pos = ScreenPoint(-0.08f, 1.0f);
            Vector2 vel = new(CultistScreenFX.Wind * 0.6f, fall * Main.rand.NextFloat(0.6f, 1f));
            PRTLoader.NewParticle<PRT_CultistAsh>(pos, vel, tint, Main.rand.NextFloat(scaleMin, scaleMax))?
                .Configure(Main.rand.Next(lifeMin, lifeMax), fall * Main.rand.NextFloat(0.85f, 1.15f), rim);
        }

        /// <summary>风暴流线:屏内起生顺风飞;无风不生(零速的丝只是个亮点)</summary>
        private static void SpawnStreak(Color tint, float scale, int life) {
            float wind = CultistScreenFX.Wind;
            if (System.MathF.Abs(wind) < 0.5f) {
                return;
            }
            Vector2 pos = ScreenPoint(-0.05f, 1.05f);
            Vector2 vel = new(wind * 4.5f, Main.rand.NextFloat(-0.6f, 0.6f));
            PRTLoader.NewParticle<PRT_CultistWindStreak>(pos, vel, tint, scale)?.Configure(life);
        }

        /// <summary>云絮:屏内起生随风滚,yMin/yMax 限定它住的屏高带</summary>
        private static void SpawnWisp(Color tint, float alpha, float scale, int life, float yMin, float yMax) {
            Vector2 pos = ScreenPoint(yMin, yMax);
            Vector2 vel = new(CultistScreenFX.Wind * 1.4f, Main.rand.NextFloat(-0.15f, 0.15f));
            PRTLoader.NewParticle<PRT_CultistStormWisp>(pos, vel, tint, scale)?.Configure(life, alpha);
        }
        #endregion
    }
}
