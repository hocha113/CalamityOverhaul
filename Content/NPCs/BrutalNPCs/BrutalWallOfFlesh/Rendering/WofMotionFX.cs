using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering
{
    /// <summary>血肉墙运动演出：血珠/碎肉/血雾/震屏/吼叫，全部客户端本地</summary>
    internal static class WofMotionFX
    {
        internal static readonly Color BloodDark = new(96, 14, 18);
        internal static readonly Color BloodMid = new(168, 26, 30);
        internal static readonly Color BloodHot = new(232, 58, 44);

        /// <summary>是否屏内(含边距)</summary>
        public static bool OnScreen(Vector2 worldPos, float margin = 300f) {
            return worldPos.X > Main.screenPosition.X - margin
                && worldPos.X < Main.screenPosition.X + Main.screenWidth + margin
                && worldPos.Y > Main.screenPosition.Y - margin
                && worldPos.Y < Main.screenPosition.Y + Main.screenHeight + margin;
        }

        /// <summary>相机冲击，受震动设置，服务端跳过</summary>
        public static void CameraPunch(Vector2 pos, float strength, int frames,
            string uniqueId = "WofMotion", Vector2? direction = null) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 dir = direction.HasValue
                ? direction.Value.SafeNormalize(Vector2.UnitY)
                : Main.rand.NextVector2Unit();
            PunchCameraModifier modifier = new PunchCameraModifier(pos, dir,
                strength, 8f, frames, 2800f, uniqueId);
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>血浆爆发：血珠+碎肉+血雾</summary>
        public static void SpawnBloodBurst(Vector2 pos, float power, Vector2? baseDir = null) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 dir = baseDir ?? -Vector2.UnitY;

            int drops = (int)(14 * power);
            for (int i = 0; i < drops; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(3f, 11f) * power;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(20f, 20f), vel,
                    Color.Lerp(BloodMid, BloodHot, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.5f))
                    ?.Configure(Main.rand.Next(26, 44), 0.34f);
            }

            int chunks = (int)(4 * power);
            for (int i = 0; i < chunks; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(4f, 9f) * power;
                PRTLoader.NewParticle<PRT_WofGore>(pos, vel, BloodDark,
                    Main.rand.NextFloat(0.22f, 0.5f) * power)?.Configure(Main.rand.Next(50, 90));
            }

            int mists = (int)(3 * power);
            for (int i = 0; i < mists; i++) {
                PRTLoader.NewParticle<PRT_WofBloodMist>(pos + Main.rand.NextVector2Circular(30f, 30f),
                    dir * Main.rand.NextFloat(0.5f, 2f) + Main.rand.NextVector2Circular(1.5f, 1.5f),
                    BloodDark, Main.rand.NextFloat(0.8f, 1.4f) * power)?.Configure(Main.rand.Next(55, 85), 0.55f);
            }

            Lighting.AddLight(pos, BloodHot.ToVector3() * 0.8f * power);
        }

        /// <summary>墙面渗血：沿墙面随机高度甩落血珠与血雾(推进的死线在滴血)</summary>
        public static void SpawnWallSeep(NPC wall, float density) {
            if (VaultUtils.isServer) {
                return;
            }
            float faceX = Core.WofWallField.WallFaceX(wall);
            float top = Core.WofWallField.Top;
            float bottom = Core.WofWallField.Bottom;
            if (bottom - top < 32f) {
                return;
            }

            int count = (int)(2 * density) + (Main.rand.NextFloat() < density % 1f ? 1 : 0);
            for (int i = 0; i < count; i++) {
                Vector2 pos = new Vector2(faceX + Main.rand.NextFloat(-26f, 10f) * wall.direction,
                    Main.rand.NextFloat(top, bottom));
                if (!OnScreen(pos, 80f)) {
                    continue;
                }
                Vector2 vel = new Vector2(wall.direction * Main.rand.NextFloat(1f, 3.4f), Main.rand.NextFloat(-1f, 2f));
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                    Color.Lerp(BloodDark, BloodMid, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.2f))
                    ?.Configure(Main.rand.Next(20, 34), 0.3f);
            }
        }

        /// <summary>吼叫演出：口部环波+血雾+震屏+低吼</summary>
        public static void MouthRoar(NPC wall, float power, bool playSound = true) {
            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(wall.Center, Vector2.Zero, BloodHot, 0.12f * power)
                ?.Configure(0.12f * power, 1.6f * power, 26);
            for (int i = 0; i < (int)(5 * power); i++) {
                PRTLoader.NewParticle<PRT_WofBloodMist>(wall.Center + Main.rand.NextVector2Circular(60f, 60f),
                    new Vector2(wall.direction * Main.rand.NextFloat(1f, 4f), Main.rand.NextFloat(-2f, 2f)),
                    BloodDark, Main.rand.NextFloat(1f, 1.8f) * power)?.Configure(Main.rand.Next(45, 75), 0.6f);
            }
            CameraPunch(wall.Center, 5f * power, 14, "WofRoar");
            if (playSound) {
                SoundEngine.PlaySound(SoundID.NPCDeath10 with { Volume = MathHelper.Clamp(power, 0.4f, 1.2f) }, wall.Center);
            }
        }

        /// <summary>下探实体地表，找不到返回 null</summary>
        public static Vector2? FindGroundBelow(Vector2 from, int maxTiles = 70) {
            int tileX = (int)(from.X / 16f);
            int tileY = Math.Max((int)(from.Y / 16f), 10);
            for (int i = 0; i < maxTiles; i++) {
                int y = tileY + i;
                if (y >= Main.maxTilesY - 10) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                    return new Vector2(from.X, y * 16f);
                }
            }
            return null;
        }

        /// <summary>上探实体顶板，找不到返回 null</summary>
        public static Vector2? FindCeilingAbove(Vector2 from, int maxTiles = 70) {
            int tileX = (int)(from.X / 16f);
            int tileY = Math.Max((int)(from.Y / 16f), 10);
            for (int i = 0; i < maxTiles; i++) {
                int y = tileY - i;
                if (y <= 10) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasUnactuatedTile && Main.tileSolid[tile.TileType]) {
                    return new Vector2(from.X, y * 16f + 16f);
                }
            }
            return null;
        }
    }
}
