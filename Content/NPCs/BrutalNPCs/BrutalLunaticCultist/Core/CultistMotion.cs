using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core
{
    /// <summary>教徒运动与仪式材质演出库，状态共用</summary>
    internal static class CultistMotion
    {
        #region 元素调色板
        /// <summary>焚焰芯金橙</summary>
        internal static Color FlameCore => new(255, 168, 82);
        /// <summary>焚焰缘赤橙</summary>
        internal static Color FlameEdge => new(255, 92, 24);
        /// <summary>霜辉芯冰白</summary>
        internal static Color FrostCore => new(168, 226, 255);
        /// <summary>霜辉缘湖蓝</summary>
        internal static Color FrostEdge => new(84, 142, 255);
        /// <summary>雷律芯堇白</summary>
        internal static Color StormCore => new(206, 176, 255);
        /// <summary>雷律缘电紫</summary>
        internal static Color StormEdge => new(128, 86, 255);
        /// <summary>假身苍灰，无元素饱和度，识破谎言的颜色线索</summary>
        internal static Color PaleClone => new(186, 208, 208);
        /// <summary>仪式符金，法阵与符文的中性底色</summary>
        internal static Color RuneGold => new(255, 214, 128);

        /// <summary>元素芯色 0火 1冰 2雷</summary>
        public static Color ElementCore(int element) => element switch {
            1 => FrostCore,
            2 => StormCore,
            _ => FlameCore,
        };

        /// <summary>元素缘色</summary>
        public static Color ElementEdge(int element) => element switch {
            1 => FrostEdge,
            2 => StormEdge,
            _ => FlameEdge,
        };

        //---- 五阶段天体调色板(与 CultistPlanet.fx 各 technique 的沙盒定稿一致) ----
        /// <summary>星旋·涡青芯</summary>
        internal static Color VortexCore => new(102, 199, 220);
        /// <summary>星旋·墨蓝缘</summary>
        internal static Color VortexEdge => new(30, 90, 140);
        /// <summary>星云·魔紫芯</summary>
        internal static Color NebulaCore => new(242, 133, 217);
        /// <summary>星云·深紫缘</summary>
        internal static Color NebulaEdge => new(117, 26, 117);
        /// <summary>星尘·晶青芯</summary>
        internal static Color StardustCore => new(158, 230, 242);
        /// <summary>星尘·冷蓝缘</summary>
        internal static Color StardustEdge => new(41, 97, 122);
        /// <summary>日耀·炽橙芯</summary>
        internal static Color SolarCore => new(255, 184, 64);
        /// <summary>日耀·熔橙缘</summary>
        internal static Color SolarEdge => new(217, 82, 13);
        /// <summary>月明·蚀青芯(月总真眼色系)</summary>
        internal static Color MoonCore => new(140, 255, 217);
        /// <summary>月明·灰岩缘</summary>
        internal static Color MoonEdge => new(82, 102, 97);

        /// <summary>阶段芯色 0星旋 1星云 2星尘 3日耀 4月明</summary>
        public static Color PhaseCore(int phase) => phase switch {
            1 => NebulaCore,
            2 => StardustCore,
            3 => SolarCore,
            4 => MoonCore,
            _ => VortexCore,
        };

        /// <summary>阶段缘色</summary>
        public static Color PhaseEdge(int phase) => phase switch {
            1 => NebulaEdge,
            2 => StardustEdge,
            3 => SolarEdge,
            4 => MoonEdge,
            _ => VortexEdge,
        };

        /// <summary>阶段→旧三元素粒子语汇(ImpactBurst 用):星旋/月明走电火花,星云/星尘走晶尘,日耀走余烬</summary>
        public static int PhaseLegacyElement(int phase) => phase switch {
            1 => 1,
            2 => 1,
            3 => 0,
            _ => 2,
        };
        #endregion

        #region 运动
        /// <summary>阻尼弹簧悬停</summary>
        public static void SpringHover(NPC npc, Vector2 target, float stiffness = 0.012f, float damping = 0.09f, float maxSpeed = 22f) {
            npc.velocity += (target - npc.Center) * stiffness;
            npc.velocity *= 1f - damping;
            if (npc.velocity.Length() > maxSpeed) {
                npc.velocity = npc.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
        }

        /// <summary>呼吸浮动偏移</summary>
        public static Vector2 BreathingOffset(float seed, float amplitude = 14f) {
            float time = Main.GlobalTimeWrappedHourly * 1.7f + seed;
            return new Vector2((float)Math.Sin(time * 0.6f) * amplitude * 0.4f, (float)Math.Sin(time) * amplitude);
        }

        /// <summary>线性预判落点</summary>
        public static Vector2 PredictTarget(Player player, Vector2 from, float projSpeed, float leadFactor = 1f) {
            float flightTime = Vector2.Distance(from, player.Center) / Math.Max(projSpeed, 1f);
            return player.Center + player.velocity * flightTime * leadFactor;
        }
        #endregion

        #region 仪式演出
        /// <summary>符文散射：帷幕挪移/假身破碎/死亡崩解的身份粒子</summary>
        public static void RuneBurst(Vector2 pos, Color color, int count, float speed = 5f) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.35f, 1f) * speed;
                PRTLoader.NewParticle<PRT_CultistRune>(pos + Main.rand.NextVector2Circular(18f, 26f), vel,
                    color, Main.rand.NextFloat(0.7f, 1.25f))?.Configure(Main.rand.Next(24, 42));
            }
        }

        /// <summary>出手蓄闪：施法点星芒+短促咏唱音（launch 阶段）</summary>
        public static void CastFlash(Vector2 pos, Color color, float strength = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_CultistGlyphFlash>(pos, Vector2.Zero, color, 0.8f * strength)?.Configure(12);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(pos, Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f) * strength,
                    color, Main.rand.NextFloat(0.6f, 1f))?.Configure(true, Main.rand.Next(8, 14));
            }
            SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.42f * strength, Pitch = 0.25f }, pos);
        }

        /// <summary>印记定形：收拢冲击环+定形音（commit 语调）</summary>
        public static void SigilCommitFX(Vector2 pos, Color color, float strength = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, color, 0.2f * strength)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 0.9f * strength, 12);
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f * strength, Pitch = -0.15f }, pos);
        }

        /// <summary>元素撞击：冲击环+元素碎屑+光点，impact 阶段</summary>
        public static void ImpactBurst(Vector2 pos, int element, float strength = 1f, bool playSound = true) {
            if (VaultUtils.isServer) {
                return;
            }
            Color core = ElementCore(element);
            Color edge = ElementEdge(element);
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, edge, 0.18f * strength)?
                .Configure(Vector2.One, Main.rand.NextFloat(MathHelper.TwoPi), 1f * strength, 14);
            int bits = (int)(8 * strength);
            for (int i = 0; i < bits; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 8f) * strength;
                if (element == 0) {
                    PRTLoader.NewParticle<PRT_CultistEmber>(pos, vel, Color.Lerp(core, edge, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.8f, 1.4f))?.Configure(Main.rand.Next(20, 34), 0.12f);
                }
                else if (element == 1) {
                    PRTLoader.NewParticle<PRT_CultistFrostMote>(pos, vel, Color.Lerp(core, edge, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.7f, 1.2f))?.Configure(Main.rand.Next(22, 36));
                }
                else {
                    PRTLoader.NewParticle<PRT_Spark>(pos, vel * 1.4f, Color.Lerp(core, edge, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.7f, 1.1f))?.Configure(true, Main.rand.Next(10, 18));
                }
            }
            Lighting.AddLight(pos, core.ToVector3() * 0.8f * strength);
            if (playSound) {
                SoundEngine.PlaySound(SoundID.Item118 with { Volume = 0.5f * MathHelper.Clamp(strength, 0.3f, 1f), Pitch = 0.1f }, pos);
            }
        }
        #endregion

        #region 通用
        /// <summary>屏幕震动，方向可选，距离衰减</summary>
        public static void Shake(Vector2 pos, float strength, int frames, Vector2? direction = null) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 dir = direction.HasValue && direction.Value != Vector2.Zero
                ? direction.Value.SafeNormalize(Vector2.UnitY) : Main.rand.NextVector2Unit();
            PunchCameraModifier modifier = new PunchCameraModifier(pos, dir, strength, 7f, frames, 2300f, "CultistMotion");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>屏内判定，含边距</summary>
        public static bool OnScreen(Vector2 worldPos, float margin = 300f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }
        #endregion
    }
}
