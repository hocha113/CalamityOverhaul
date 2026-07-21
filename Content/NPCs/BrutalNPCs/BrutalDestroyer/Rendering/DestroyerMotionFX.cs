using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDestroyer.Rendering
{
    /// <summary>运动演出，光带/爆发/火花/冲击/震屏</summary>
    internal static class DestroyerMotionFX
    {
        //固定点数的缓存Trail（Trail的点数与宽度/颜色委托在构造时绑定，且持有GPU缓冲，须复用）
        private const int TrailPointCount = 26;
        private static Trail headTrail;
        private static readonly Vector2[] trailPositions = new Vector2[TrailPointCount];
        private static float trailWidth;
        private static float trailAlpha;

        internal static readonly Color HotOrange = new(255, 130, 40);
        internal static readonly Color HotRed = new(255, 70, 30);
        internal static readonly Color WhiteHot = new(255, 230, 180);

        #region 高速光带拖尾

        /// <summary>头oldPos加法光带，强度过阈才绘</summary>
        public static void DrawHeadTrail(NPC npc, float intensity) {
            if (intensity <= 0.05f || EffectLoader.GradientTrail?.Value == null) {
                return;
            }

            //轨迹点，[^1]头现位 [0]尾；不足用最旧点垫
            Span<Vector2> gathered = stackalloc Vector2[TrailPointCount];
            int count = 0;
            gathered[count++] = npc.Center;
            for (int i = 0; i < npc.oldPos.Length && count < TrailPointCount; i++) {
                if (npc.oldPos[i] == Vector2.Zero) {
                    break;
                }
                Vector2 pos = npc.oldPos[i] + npc.Size / 2f;
                //滤瞬移超长段
                if (Vector2.DistanceSquared(pos, gathered[count - 1]) > 400f * 400f) {
                    break;
                }
                gathered[count++] = pos;
            }
            if (count < 4) {
                return;
            }

            //[0]最新，倒序写入trail末
            Vector2 oldest = gathered[count - 1];
            int pad = TrailPointCount - count;
            for (int i = 0; i < pad; i++) {
                trailPositions[i] = oldest;
            }
            for (int i = 0; i < count; i++) {
                trailPositions[pad + i] = gathered[count - 1 - i];
            }

            trailWidth = 42f * intensity;
            trailAlpha = 0.85f * intensity;

            //闭包读取静态字段，Trail只构造一次
            headTrail ??= new Trail(new Vector2[TrailPointCount],
                f => trailWidth * (0.2f + f * 0.8f),
                texCoord => Color.Lerp(HotRed, HotOrange, texCoord.X) * (trailAlpha * (0.25f + texCoord.X * 0.75f)));
            headTrail.TrailPositions = trailPositions;

            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * -0.06f);
            effect.Parameters["uTimeG"]?.SetValue(Main.GlobalTimeWrappedHourly * -0.2f);
            effect.Parameters["udissolveS"]?.SetValue(1f);
            effect.Parameters["uBaseImage"]?.SetValue(VaultAsset.placeholder2.Value);
            effect.Parameters["uFlow"]?.SetValue(VaultAsset.placeholder2.Value);
            effect.Parameters["uGradient"]?.SetValue(CWRAsset.BloodRed_Bar.Value);
            effect.Parameters["uDissolve"]?.SetValue(VaultAsset.placeholder2.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            for (int i = 0; i < 3; i++) {
                headTrail.DrawTrail(effect);
            }
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
        }

        internal static void Unload() {
            headTrail?.Dispose();
            headTrail = null;
        }

        #endregion

        #region 热浪尾流扭曲

        /// <summary>Warp热浪尾流quad，HeatWakeProj.Warp调</summary>
        public static void DrawHeatWakeWarp(Vector2 worldCenter, float lengthPx, float widthPx,
            float rotation, float intensity, float progress) {
            if (EffectLoader.DestroyerHeatWake?.Value == null) {
                return;
            }

            Effect effect = EffectLoader.DestroyerHeatWake.Value;
            effect.Parameters["uTime"]?.SetValue((float)Main.GameUpdateCount * 0.05f);
            effect.Parameters["uIntensity"]?.SetValue(intensity);
            effect.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(progress, 0f, 1f));
            effect.Parameters["uRotation"]?.SetValue(rotation);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            //占位拉伸为旋转quad
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(lengthPx / pixel.Width, widthPx / pixel.Height);
            sb.Draw(pixel, worldCenter - Main.screenPosition, null, Color.White,
                rotation, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            //还原Warp采集批
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        #endregion

        #region 冲刺与刹车

        /// <summary>冲刺爆发，音爆见Shockwave</summary>
        public static void SpawnDashBurst(Vector2 pos, Vector2 dir) {
            if (VaultUtils.isServer) {
                return;
            }

            PRTLoader.NewParticle<PRT_Light>(pos, dir * 9f, HotOrange, 1.6f)
                .Configure(22, 1f, 2.2f, 4f);
            PRTLoader.NewParticle<PRT_StarPulseRing>(pos, dir * 2f, HotRed, 0.1f)
                .Configure(0.1f, 1.5f, 24);

            for (int i = 0; i < 14; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.65f, 0.65f)) * Main.rand.NextFloat(6f, 17f);
                PRTLoader.NewParticle<PRT_Spark>(pos, vel,
                    Color.Lerp(HotOrange, WhiteHot, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.0f, 1.7f)).Configure(true, Main.rand.Next(16, 28));
            }

            Lighting.AddLight(pos, HotOrange.ToVector3() * 2.4f);
        }

        /// <summary>硬刹逆速火花+烟</summary>
        public static void SpawnBrakeSparks(NPC npc) {
            if (VaultUtils.isServer || npc.velocity.Length() < 4f) {
                return;
            }

            Vector2 back = -npc.velocity.SafeNormalize(Vector2.Zero);
            for (int i = 0; i < 3; i++) {
                Vector2 vel = back.RotatedBy(Main.rand.NextFloat(-0.8f, 0.8f)) * Main.rand.NextFloat(3f, 9f);
                PRTLoader.NewParticle<PRT_Spark>(npc.Center + Main.rand.NextVector2Circular(22f, 22f), vel,
                    new Color(255, 200, 110), Main.rand.NextFloat(0.7f, 1.2f)).Configure(true, Main.rand.Next(12, 20));
            }
            if (Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_Smoke>(npc.Center, back * 2f - Vector2.UnitY,
                    new Color(60, 56, 54), Main.rand.NextFloat(0.5f, 0.8f))
                    .Configure(Main.rand.Next(30, 45), 0.5f, Main.rand.NextFloat(-0.04f, 0.04f));
            }
        }

        /// <summary>体节高速火花/扬尘；调用方控频率，内部屏外剔除</summary>
        public static void SpawnSegmentSpeedSparks(NPC segment, float strength = 1f) {
            if (VaultUtils.isServer || !OnScreen(segment.Center)) {
                return;
            }

            Vector2 lateral = segment.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
            float side = Main.rand.NextBool() ? 1f : -1f;
            Vector2 vel = lateral * side * Main.rand.NextFloat(2f, 6f) * strength - segment.velocity * 0.05f;
            PRTLoader.NewParticle<PRT_Spark>(
                segment.Center + Main.rand.NextVector2Circular(segment.width * 0.4f, segment.height * 0.4f),
                vel, Color.Lerp(HotOrange, WhiteHot, Main.rand.NextFloat(0.5f)),
                Main.rand.NextFloat(0.6f, 1.1f) * strength).Configure(true, Main.rand.Next(10, 18));
        }

        #endregion

        #region 冲击与震动

        /// <summary>大地冲击，光+碎屑+烟+音</summary>
        public static void SpawnImpactBlast(Vector2 pos, float power) {
            if (VaultUtils.isServer) {
                return;
            }

            Color warm = Color.Lerp(new Color(255, 150, 50), new Color(255, 85, 35), Main.rand.NextFloat());
            PRTLoader.NewParticle<PRT_MechExplosion>(pos, Vector2.Zero, warm, 2.2f * power)
                .Configure(34, warm);

            //碎屑喷泉
            int sparkCount = (int)(26 * power);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = (-Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f))
                    * Main.rand.NextFloat(5f, 16f) * power;
                PRTLoader.NewParticle<PRT_Spark>(pos + Main.rand.NextVector2Circular(30f, 12f), vel,
                    Color.Lerp(HotOrange, WhiteHot, Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.0f, 1.9f)).Configure(true, Main.rand.Next(22, 40));
            }

            int emberCount = (int)(10 * power);
            for (int i = 0; i < emberCount; i++) {
                Vector2 vel = (-Vector2.UnitY).RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f)) * Main.rand.NextFloat(3f, 8f);
                PRTLoader.NewParticle<PRT_LavaFire>(pos + Main.rand.NextVector2Circular(26f, 10f), vel,
                    Color.White, Main.rand.NextFloat(0.7f, 1.3f)).SetLifetime(24, 50);
            }

            int smokeCount = (int)(8 * power);
            for (int i = 0; i < smokeCount; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3f, 1.5f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f);
                PRTLoader.NewParticle<PRT_Smoke>(pos + Main.rand.NextVector2Circular(40f, 14f), vel,
                    Color.Lerp(new Color(70, 64, 60), new Color(28, 24, 24), Main.rand.NextFloat()),
                    Main.rand.NextFloat(1.0f, 1.7f) * power)
                    .Configure(Main.rand.Next(50, 85), 0.75f, Main.rand.NextFloat(-0.05f, 0.05f));
            }

            Lighting.AddLight(pos, warm.ToVector3() * 3f * power);
            SoundEngine.PlaySound(SoundID.DD2_KoboldExplosion with { Volume = 1.05f, Pitch = -0.35f }, pos);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.9f, Pitch = -0.5f }, pos);
        }

        /// <summary>相机冲击，受震动设置，服务端跳过</summary>
        public static void CameraPunch(Vector2 pos, float strength, int frames,
            string uniqueId = "DestroyerMotion", Vector2? direction = null) {
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

        /// <summary>下探实体地表，找不到回退下方400px</summary>
        public static Vector2 FindGroundBelow(Vector2 from) {
            int tileX = (int)(from.X / 16f);
            int tileY = Math.Max((int)(from.Y / 16f), 10);
            for (int i = 0; i < 70; i++) {
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

        #endregion
    }
}
