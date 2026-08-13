using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering
{
    /// <summary>世界吞噬者运动演出：腐化配色、尘爆、酸雾、震屏、地表探测</summary>
    internal static class EowMotionFX
    {
        #region 腐化配色
        /// <summary>酸液主绿</summary>
        internal static readonly Color AcidGreen = new(150, 216, 84);
        /// <summary>酸液高亮(膜面反光)</summary>
        internal static readonly Color AcidBright = new(206, 244, 148);
        /// <summary>酸液沉色</summary>
        internal static readonly Color AcidDeep = new(84, 142, 46);
        /// <summary>腐化紫(甲壳内光)</summary>
        internal static readonly Color CorruptPurple = new(138, 94, 205);
        /// <summary>暗影肉色(阴影底)</summary>
        internal static readonly Color FleshShadow = new(66, 50, 96);
        /// <summary>蚀土棕</summary>
        internal static readonly Color DirtBrown = new(112, 92, 66);
        #endregion

        #region 地表探测与屏幕
        /// <summary>自某点向下探实体地表，找不到回退下方400px</summary>
        public static Vector2 FindGroundBelow(Vector2 from) {
            int tileX = (int)(from.X / 16f);
            int tileY = Math.Max((int)(from.Y / 16f), 10);
            for (int i = 0; i < 80; i++) {
                int y = tileY + i;
                if (y >= Main.maxTilesY - 10) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                    return new Vector2(from.X, y * 16f);
                }
            }
            return from + new Vector2(0, 400f);
        }

        /// <summary>是否屏内(含边距)</summary>
        public static bool OnScreen(Vector2 worldPos, float margin = 280f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        /// <summary>相机冲击，受震动设置约束，服务端跳过</summary>
        public static void CameraPunch(Vector2 pos, float strength, int frames,
            string uniqueId = "EowMotion", Vector2? direction = null) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 dir = direction.HasValue
                ? direction.Value.SafeNormalize(Vector2.UnitY)
                : Main.rand.NextVector2Unit();
            PunchCameraModifier modifier = new PunchCameraModifier(pos, dir,
                strength, 8f, frames, 2600f, uniqueId);
            Main.instance.CameraModifiers.Add(modifier);
        }
        #endregion

        #region 蚀土尘爆
        /// <summary>入土/出土尘爆：土屑喷泉+腐化雾+闷响；power≈0.6~2</summary>
        public static void SpawnDirtBurst(Vector2 pos, float power, bool withSound = true) {
            if (VaultUtils.isServer || !OnScreen(pos)) {
                return;
            }

            int dirtCount = (int)(22 * power);
            for (int i = 0; i < dirtCount; i++) {
                Dust dust = Dust.NewDustDirect(pos + new Vector2(Main.rand.NextFloat(-52f, 52f), -8f),
                    6, 6, DustID.Dirt, 0, 0, 90, default, Main.rand.NextFloat(1.3f, 2.3f));
                dust.velocity = new Vector2(Main.rand.NextFloat(-3.2f, 3.2f), -Main.rand.NextFloat(3f, 9.5f) * power);
            }
            int rotCount = (int)(8 * power);
            for (int i = 0; i < rotCount; i++) {
                Dust dust = Dust.NewDustDirect(pos + new Vector2(Main.rand.NextFloat(-40f, 40f), -6f),
                    4, 4, DustID.CorruptGibs, 0, 0, 120, default, Main.rand.NextFloat(1.0f, 1.7f));
                dust.velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -Main.rand.NextFloat(2f, 6f) * power);
                dust.noGravity = Main.rand.NextBool();
            }
            //扬起的尘幕
            int smokeCount = (int)(4 * power);
            for (int i = 0; i < smokeCount; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(pos + Main.rand.NextVector2Circular(36f, 10f),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1f, 3f) * power),
                    Color.Lerp(DirtBrown, FleshShadow, Main.rand.NextFloat(0.55f)),
                    Main.rand.NextFloat(0.8f, 1.4f) * power)
                    .Configure(Main.rand.Next(40, 70), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f));
            }

            if (withSound) {
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.9f, Pitch = -0.3f, MaxInstances = 4 }, pos);
            }
        }

        /// <summary>大破土冲击：尘爆+酸沫+土块抛射+重低音；power≈1~2.4</summary>
        public static void SpawnBreachBlast(Vector2 pos, float power, Vector2? mainDir = null) {
            if (VaultUtils.isServer) {
                return;
            }
            SpawnDirtBurst(pos, power * 1.3f, withSound: false);

            if (OnScreen(pos)) {
                Vector2 axis = (mainDir ?? -Vector2.UnitY).SafeNormalize(-Vector2.UnitY);
                //酸沫飞溅锥
                int splashCount = (int)(16 * power);
                for (int i = 0; i < splashCount; i++) {
                    Vector2 vel = axis.RotatedBy(Main.rand.NextFloat(-0.95f, 0.95f))
                        * Main.rand.NextFloat(4f, 13f) * power;
                    PRTLoader.NewParticle<PRT_AcidSplash>(pos + Main.rand.NextVector2Circular(26f, 12f), vel,
                        Color.White, Main.rand.NextFloat(0.5f, 1.0f)).Configure(Main.rand.Next(24, 44));
                }
                //腐化雾团
                for (int i = 0; i < (int)(3 * power); i++) {
                    PRTLoader.NewParticle<PRT_ToxicMist>(pos + Main.rand.NextVector2Circular(40f, 16f),
                        axis * Main.rand.NextFloat(1f, 2.5f) + Main.rand.NextVector2Circular(1.2f, 1.2f),
                        Color.White, Main.rand.NextFloat(0.9f, 1.5f) * power)
                        .Configure(Main.rand.Next(45, 80), Main.rand.NextFloat(0.35f, 0.75f));
                }
                Lighting.AddLight(pos, AcidGreen.ToVector3() * 0.9f * power);
            }

            SoundEngine.PlaySound(SoundID.WormDig with { Volume = 1.1f, Pitch = -0.5f }, pos);
            SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 0.75f, Pitch = -0.55f, MaxInstances = 3 }, pos);
        }
        #endregion

        #region 酸液与腐体
        /// <summary>酸液爆沫：飞溅+雾+光；用于命中/撕裂/喷吐</summary>
        public static void SpawnAcidBurst(Vector2 pos, float power, Vector2? dir = null) {
            if (VaultUtils.isServer || !OnScreen(pos)) {
                return;
            }
            Vector2 axis = (dir ?? Vector2.Zero);
            int count = (int)(9 * power);
            for (int i = 0; i < count; i++) {
                Vector2 vel = axis * Main.rand.NextFloat(0.4f, 1f)
                    + Main.rand.NextVector2Circular(3.4f, 3.4f) * power;
                PRTLoader.NewParticle<PRT_AcidSplash>(pos, vel, Color.White,
                    Main.rand.NextFloat(0.45f, 0.9f) * power).Configure(Main.rand.Next(18, 34));
            }
            if (Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_ToxicMist>(pos, Main.rand.NextVector2Circular(1.4f, 1.4f),
                    Color.White, Main.rand.NextFloat(0.6f, 1.1f) * power)
                    .Configure(Main.rand.Next(35, 60), Main.rand.NextFloat(0.4f, 0.7f));
            }
            Lighting.AddLight(pos, AcidGreen.ToVector3() * 0.55f * power);
        }

        /// <summary>体节撕裂创口：迸酸+腐雾+闷裂声(分裂/蜕皮用)</summary>
        public static void SpawnRipBurst(Vector2 pos, Vector2 alongDir, float power) {
            if (VaultUtils.isServer) {
                return;
            }
            if (OnScreen(pos)) {
                Vector2 lateral = alongDir.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                for (int side = -1; side <= 1; side += 2) {
                    for (int i = 0; i < (int)(7 * power); i++) {
                        Vector2 vel = lateral * side * Main.rand.NextFloat(2.5f, 8f)
                            + alongDir * Main.rand.NextFloat(-2f, 2f);
                        PRTLoader.NewParticle<PRT_AcidSplash>(pos, vel, Color.White,
                            Main.rand.NextFloat(0.5f, 1.05f)).Configure(Main.rand.Next(22, 40));
                    }
                }
                PRTLoader.NewParticle<PRT_ToxicMist>(pos, Main.rand.NextVector2Circular(1f, 1f),
                    Color.White, Main.rand.NextFloat(0.9f, 1.3f) * power)
                    .Configure(Main.rand.Next(40, 66), 0.6f);
                Lighting.AddLight(pos, AcidGreen.ToVector3() * power);
            }
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.85f, Pitch = -0.35f, MaxInstances = 4 }, pos);
        }

        /// <summary>体节高速侧漏酸沫(调用方控频，内部屏外剔除)</summary>
        public static void SpawnSegmentSpeedSpray(NPC segment, float strength = 1f) {
            if (VaultUtils.isServer || !OnScreen(segment.Center)) {
                return;
            }
            Vector2 lateral = segment.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float side = Main.rand.NextBool() ? 1f : -1f;
            Vector2 vel = lateral * side * Main.rand.NextFloat(1.5f, 4.5f) * strength - segment.velocity * 0.04f;
            PRTLoader.NewParticle<PRT_AcidSplash>(
                segment.Center + Main.rand.NextVector2Circular(segment.width * 0.4f, segment.height * 0.4f),
                vel, Color.White, Main.rand.NextFloat(0.35f, 0.7f) * strength).Configure(Main.rand.Next(14, 24));
        }

        /// <summary>蜕壳：一节的旧甲弹飞(腐化雾+甲壳浮渣+暗屑)</summary>
        public static void SpawnMoltHusk(NPC segment, float power = 1f) {
            if (VaultUtils.isServer || !OnScreen(segment.Center)) {
                return;
            }
            Vector2 lateral = segment.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            for (int side = -1; side <= 1; side += 2) {
                Vector2 vel = lateral * side * Main.rand.NextFloat(2f, 5f) - Vector2.UnitY * Main.rand.NextFloat(0.5f, 2f);
                //旧壳碎片走暗色火花体
                PRTLoader.NewParticle<PRT_Spark>(segment.Center, vel,
                    Color.Lerp(FleshShadow, DirtBrown, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.3f))
                    .Configure(true, Main.rand.Next(20, 36));
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(segment.position, segment.width, segment.height,
                    DustID.CorruptGibs, 0, 0, 100, default, Main.rand.NextFloat(1.1f, 1.9f));
                dust.velocity = Main.rand.NextVector2Circular(3f, 3f) * power;
            }
            PRTLoader.NewParticle<PRT_ToxicMist>(segment.Center, -Vector2.UnitY * 0.8f, Color.White,
                Main.rand.NextFloat(0.7f, 1.1f)).Configure(Main.rand.Next(30, 50), 0.55f);
        }
        #endregion

        #region 声音节拍
        /// <summary>低吼(远近皆闻的压迫感)</summary>
        public static void PlayRoar(Vector2 pos, float pitch = -0.5f, float volume = 1f) {
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = pitch, Volume = volume }, pos);
        }

        /// <summary>湿滑吐息(唾液起手)</summary>
        public static void PlaySpitCue(Vector2 pos, float pitch = 0f) {
            SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.6f, Pitch = 0.25f + pitch, MaxInstances = 5 }, pos);
            SoundEngine.PlaySound(SoundID.Item17 with { Volume = 0.8f, Pitch = -0.2f + pitch, MaxInstances = 5 }, pos);
        }
        #endregion
    }
}
