using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering
{
    /// <summary>月总本地表现小工具：镜头冲击、星尘爆、地表探测</summary>
    internal static class MLordScreenFX
    {
        /// <summary>方向性镜头冲击，本地，吃震屏配置</summary>
        public static void Punch(Vector2 pos, float strength, int frames, Vector2? direction = null) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            Vector2 dir = direction.HasValue
                ? direction.Value.SafeNormalize(Vector2.UnitY)
                : Main.rand.NextVector2Unit();
            PunchCameraModifier modifier = new(pos, dir, strength, 8f, frames, 3000f, "MLordPunch");
            Main.instance.CameraModifiers.Add(modifier);
        }

        /// <summary>星尘爆：星芒粒 + 光尘 + 空间裂纹，天体材质三件套</summary>
        public static void StarBurst(Vector2 pos, float scale, int starCount = 14) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < starCount; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 9f) * scale;
                Color color = Color.Lerp(MLordDirector.Phantasmal, MLordDirector.MoonWhite, Main.rand.NextFloat(0.5f));
                PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, vel, color,
                    Main.rand.NextFloat(0.6f, 1.15f) * scale)?.Configure(true, Main.rand.Next(20, 34));
            }
            for (int i = 0; i < 4; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 7f) * scale;
                PRTLoader.NewParticle<PRT_SpaceFracture>(pos, vel, MLordDirector.DeepViolet,
                    Main.rand.NextFloat(0.8f, 1.3f) * scale)?.Configure(Main.rand.Next(16, 26), Main.rand.NextFloat(-0.06f, 0.06f));
            }
            PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero, MLordDirector.Phantasmal, 1.5f * scale)
                ?.Configure(18, 0.9f);
        }

        /// <summary>向心汇聚星流（蓄力语法：外圈拉入 + 切向涡旋）</summary>
        public static void ConvergeStreak(Vector2 center, float radius, float chargeRatio) {
            if (VaultUtils.isServer) {
                return;
            }
            //末四分之一静默，尖啸前的吸气
            if (chargeRatio > 0.72f || !Main.rand.NextBool(2)) {
                return;
            }
            Vector2 offset = Main.rand.NextVector2Unit() * Main.rand.NextFloat(radius * 0.4f, radius);
            Vector2 pos = center + offset;
            Vector2 pull = (center - pos) * 0.085f;
            Vector2 swirl = pull.RotatedBy(MathHelper.PiOver2) * 0.55f;
            Color color = Color.Lerp(MLordDirector.DeepViolet, MLordDirector.Phantasmal, Main.rand.NextFloat());
            PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, pull + swirl, color,
                Main.rand.NextFloat(0.4f, 0.85f))?.Configure(false, Main.rand.Next(14, 24));
        }

        /// <summary>下探实体地表，找不到回退下方 420px</summary>
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
            return from + new Vector2(0, 420f);
        }
    }
}
