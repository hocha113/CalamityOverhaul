using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering
{
    /// <summary>运动演出库：浪花/水雾/破水/震屏/水面探测</summary>
    internal static class FishronMotionFX
    {
        //风暴海色板：深海青绿主导，泡沫冷白，雷光青
        internal static readonly Color DeepSea = new(18, 68, 92);
        internal static readonly Color SeaGreen = new(38, 150, 160);
        internal static readonly Color FoamWhite = new(190, 235, 235);
        internal static readonly Color StormBolt = new(140, 230, 255);

        #region 粒子

        /// <summary>浪花锥：沿方向喷出带重力的水珠</summary>
        public static void SpawnSprayCone(Vector2 pos, Vector2 dir, int count, float speedMin, float speedMax, float spread = 0.7f, float scale = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-spread, spread)) * Main.rand.NextFloat(speedMin, speedMax);
                var p = PRTLoader.NewParticle<PRT_FishronSpray>(pos + Main.rand.NextVector2Circular(14f, 14f), vel,
                    Color.Lerp(SeaGreen, FoamWhite, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.25f) * scale);
                p?.Configure(Main.rand.Next(22, 40), 0.22f);
            }
        }

        /// <summary>水雾团：Fog 贴图慢飘</summary>
        public static void SpawnMist(Vector2 pos, Vector2 vel, float scale, int count = 1) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(pos + Main.rand.NextVector2Circular(26f, 14f),
                    vel + Main.rand.NextVector2Circular(1.2f, 0.8f),
                    Color.Lerp(DeepSea, SeaGreen, Main.rand.NextFloat()) * 0.75f,
                    Main.rand.NextFloat(0.7f, 1.15f) * scale * 0.6f)
                    ?.Configure(Main.rand.Next(38, 64), 0.55f, Main.rand.NextFloat(-0.04f, 0.04f));
            }
        }

        /// <summary>破水/入水大水花：环+喷泉+雾+声</summary>
        public static void SpawnSplashBurst(Vector2 pos, float power, bool playSound = true) {
            if (VaultUtils.isServer) {
                return;
            }
            //正交扩散环
            PRTLoader.NewParticle<PRT_DWave>(pos, -Vector2.UnitY * 1.4f, SeaGreen, 0.24f * power)?
                .Configure(new Vector2(1.5f, 0.5f), 0f, 1.05f * power, 18);
            PRTLoader.NewParticle<PRT_DWave>(pos, -Vector2.UnitY * 0.7f, FoamWhite * 0.8f, 0.14f * power)?
                .Configure(new Vector2(1.2f, 0.65f), 0f, 0.7f * power, 14);

            //喷泉水珠
            int count = (int)(24 * power);
            for (int i = 0; i < count; i++) {
                Vector2 vel = (-Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-1f, 1f)) * Main.rand.NextFloat(5f, 15f) * power;
                PRTLoader.NewParticle<PRT_FishronSpray>(pos + Main.rand.NextVector2Circular(34f, 10f), vel,
                    Color.Lerp(SeaGreen, FoamWhite, Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.6f))
                    ?.Configure(Main.rand.Next(26, 48), 0.3f);
            }

            SpawnMist(pos, -Vector2.UnitY * 1.5f, power, (int)(4 * power));
            Lighting.AddLight(pos, SeaGreen.ToVector3() * 1.4f * power);

            if (playSound) {
                SoundEngine.PlaySound(SoundID.Splash with { Volume = Math.Min(1.1f, 0.65f * power), Pitch = -0.35f, MaxInstances = 3 }, pos);
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = Math.Min(0.9f, 0.4f * power), Pitch = -0.5f, MaxInstances = 3 }, pos);
            }
        }

        /// <summary>冲刺起手爆发：正交水环+后向水珠+闷响</summary>
        public static void SpawnDashBurst(Vector2 pos, Vector2 dir, float strength = 1f) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, dir * 1.6f, SeaGreen, 0.24f * strength)?
                .Configure(new Vector2(1.5f, 0.5f), dir.ToRotation() + MathHelper.PiOver2, 1.1f * strength, 16);
            PRTLoader.NewParticle<PRT_DWave>(pos, dir * 0.8f, FoamWhite * 0.7f, 0.14f * strength)?
                .Configure(new Vector2(1.2f, 0.7f), dir.ToRotation() + MathHelper.PiOver2, 0.72f * strength, 12);

            for (int i = 0; i < 12; i++) {
                Vector2 vel = -dir.RotatedBy(Main.rand.NextFloat(-0.65f, 0.65f)) * Main.rand.NextFloat(4f, 12f) * strength;
                PRTLoader.NewParticle<PRT_FishronSpray>(pos, vel,
                    Color.Lerp(SeaGreen, FoamWhite, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.4f) * strength)
                    ?.Configure(Main.rand.Next(18, 30), 0.24f);
            }

            SoundEngine.PlaySound(SoundID.Item96 with { Volume = 0.8f * strength, Pitch = -0.15f, MaxInstances = 3 }, pos);
            CameraPunch(pos, 5f * strength, 10, "FishronDash", dir);
        }

        /// <summary>刹车水花：逆速小水珠</summary>
        public static void SpawnBrakeSpray(NPC npc) {
            if (VaultUtils.isServer || npc.velocity.Length() < 5f) {
                return;
            }
            Vector2 back = -npc.velocity.SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 3; i++) {
                Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(3f, 8f);
                PRTLoader.NewParticle<PRT_FishronSpray>(npc.Center + Main.rand.NextVector2Circular(30f, 20f), vel,
                    Color.Lerp(SeaGreen, FoamWhite, Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(14, 24), 0.2f);
            }
        }

        /// <summary>蓄力内聚水汽：72% 后硬切静默</summary>
        public static void SpawnChargeGatherFX(Vector2 center, float progress, float radius = 110f) {
            if (VaultUtils.isServer || progress > 0.72f) {
                return;
            }
            Vector2 spawnPos = center + Main.rand.NextVector2CircularEdge(radius, radius) * (1f - progress * 0.4f);
            PRTLoader.NewParticle<PRT_FishronSpray>(spawnPos, (center - spawnPos) * 0.1f,
                Color.Lerp(SeaGreen, FoamWhite, progress), Main.rand.NextFloat(0.7f, 1.2f))
                ?.Configure(14, 0f);
        }

        #endregion

        #region 震屏与探测

        /// <summary>相机冲击，受设置项，服务端跳过</summary>
        public static void CameraPunch(Vector2 pos, float strength, int frames,
            string uniqueId = "FishronMotion", Vector2? direction = null) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 dir = direction.HasValue
                ? direction.Value.SafeNormalize(Vector2.UnitY)
                : Main.rand.NextVector2Unit();
            PunchCameraModifier modifier = new PunchCameraModifier(pos, dir,
                strength, 8f, frames, 2800f, uniqueId);
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>下探水面或地表；找到水返回水面 y，否则实体地表，兜底下方 420px</summary>
        public static Vector2 FindSurfaceBelow(Vector2 from, out bool isWater) {
            isWater = false;
            int tileX = (int)(from.X / 16f);
            int tileY = Math.Max((int)(from.Y / 16f), 10);
            for (int i = 0; i < 80; i++) {
                int y = tileY + i;
                if (y >= Main.maxTilesY - 10) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.LiquidAmount > 100 && tile.LiquidType == LiquidID.Water) {
                    isWater = true;
                    return new Vector2(from.X, y * 16f);
                }
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                    return new Vector2(from.X, y * 16f);
                }
            }
            return from + new Vector2(0, 420f);
        }

        /// <summary>是否屏内(含边距)</summary>
        public static bool OnScreen(Vector2 worldPos, float margin = 300f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        #endregion
    }
}
