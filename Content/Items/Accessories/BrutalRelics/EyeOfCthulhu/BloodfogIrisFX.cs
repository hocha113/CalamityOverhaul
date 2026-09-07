using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.EyeOfCthulhu
{
    /// <summary>
    /// 血雾之瞳演出库。材质只有一种：高速中的液态血，按"团块 → 拉丝 → 血珠"三段破碎级联组织，
    /// 全部元素离散、各有寿命尺寸朝向；没有连续条带、没有规整弧面、没有雾粒、没有加色光环。<br/>
    /// 与克眼本体的 EocMotion 分家：Boss 侧仍带雾，饰品侧一颗雾粒都不生成
    /// </summary>
    internal static class BloodfogIrisFX
    {
        /// <summary>血珠锥：dir 为主方向，spread 半角 rad，速度按指数减速由 PRT 自带 drag 承担</summary>
        public static void Spray(Vector2 pos, Vector2 dir, int count, float spread,
            float speedMin, float speedMax, float scaleMin, float scaleMax, int lifeMin, int lifeMax,
            float gravity = 0.32f, float drag = 0.985f, float scatterPx = 6f) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-spread, spread)) * Main.rand.NextFloat(speedMin, speedMax);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(scatterPx, scatterPx), vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()),
                    Main.rand.NextFloat(scaleMin, scaleMax))?.Configure(Main.rand.Next(lifeMin, lifeMax), gravity, drag);
            }
        }

        /// <summary>细血雾：很小的血珠成片、低重力、短命、随风漂——"血雾"就是这个，不是雾贴图</summary>
        public static void FineMist(Vector2 pos, Vector2 dir, int count, float speed, float spread, float scatterPx = 10f) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < count; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-spread, spread)) * Main.rand.NextFloat(0.4f, 1f) * speed;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos + Main.rand.NextVector2Circular(scatterPx, scatterPx), vel,
                    Color.Lerp(EocMotion.VenousDark, EocMotion.Arterial, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(10, 18), 0.1f, 0.955f);
            }
        }

        /// <summary>雾态血滴：身上随机一点滴落一颗，读作"刚由血凝形、还在往下淌"</summary>
        public static void Drip(Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 pos = player.Center + new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-16f, 14f));
            Vector2 vel = new Vector2(player.velocity.X * 0.3f, Main.rand.NextFloat(0.5f, 1.5f));
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel,
                Color.Lerp(EocMotion.VenousDark, EocMotion.Arterial, Main.rand.NextFloat()), Main.rand.NextFloat(0.7f, 1.1f))?
                .Configure(Main.rand.Next(18, 30), 0.3f, 0.99f);
        }

        /// <summary>血珠向心汇聚：外圈生成、按 frames 帧命中中心</summary>
        public static void Converge(Vector2 center, float radius, int count, int frames = 16) {
            if (VaultUtils.isServer) {
                return;
            }
            frames = Math.Max(frames, 2);
            for (int i = 0; i < count; i++) {
                Vector2 spawnPos = center + Main.rand.NextVector2CircularEdge(radius, radius) * Main.rand.NextFloat(0.7f, 1f);
                Vector2 vel = (center - spawnPos) / frames;
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(spawnPos, vel,
                    Color.Lerp(EocMotion.Arterial, EocMotion.BrightBlood, Main.rand.NextFloat()), Main.rand.NextFloat(0.9f, 1.5f))?
                    .Configure(frames, 0f, 1f);
            }
        }

        /// <summary>速度线：血核身后几条细长暗血丝，3~5 帧即逝，只给速度读数</summary>
        public static void SpeedStreaks(Vector2 pos, Vector2 dir, int count, float spreadPx) {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < count; i++) {
                Vector2 p = pos - dir * Main.rand.NextFloat(10f, 40f) + perp * Main.rand.NextFloat(-spreadPx, spreadPx);
                PRTLoader.NewParticle<PRT_BloodStreak>(p, -dir * Main.rand.NextFloat(1f, 3f),
                    EocMotion.VenousDark, 1f)?.Configure(Main.rand.Next(3, 6), Main.rand.NextFloat(40f, 110f), Main.rand.NextFloat(1.5f, 3f), dir);
            }
        }

        /// <summary>湿爆音（可带震屏），起步/炸开/落地共用</summary>
        public static void WetBurstSound(Vector2 pos, float strength, bool heavy) {
            if (VaultUtils.isServer) {
                return;
            }
            if (heavy) {
                SoundEngine.PlaySound(SoundID.NPCDeath12 with { Volume = 0.85f * MathHelper.Clamp(strength, 0.4f, 1.2f), Pitch = -0.25f }, pos);
            }
            else {
                SoundEngine.PlaySound(SoundID.NPCDeath13 with { Volume = 0.7f * strength, Pitch = -0.35f }, pos);
            }
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.5f * strength, Pitch = -0.1f }, pos);
        }
    }

    /// <summary>速度线粒子：Extra_98 真 alpha 细长暗血丝，朝向锁定、快速淡出</summary>
    internal class PRT_BloodStreak : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private float lengthPx;
        private float widthPx;
        private Color initialColor;

        public PRT_BloodStreak Configure(int lifetime, float lengthPx, float widthPx, Vector2 dir) {
            Lifetime = lifetime;
            this.lengthPx = lengthPx;
            this.widthPx = widthPx;
            initialColor = Color;
            //Extra_98 芯梭竖向，转到运动方向
            Rotation = dir.ToRotation() + MathHelper.PiOver2;
            return this;
        }

        public override void Reset() {
            base.Reset();
            lengthPx = 0f;
            widthPx = 0f;
            initialColor = default;
        }

        public override void AI() {
            Velocity *= 0.9f;
            float t = LifetimeCompletion;
            Color = initialColor * MathF.Pow(1f - t, 1.5f) * 0.8f;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //Extra_98 芯区约 12x42px（72 的 0.17x0.58）
            Vector2 scale = new(widthPx / 12f, lengthPx / 42f);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation, tex.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 液态血 quad 批：把当前实体批切到 Immediate，两支 ps-only 着色器（血团 / 溅片）手动 Apply，
    /// 每 quad 参数打包进顶点色（不逐 quad Apply），组级参数（撕裂/热度/扇角）切组时 Apply 一次。
    /// 调用方须处于 Deferred AlphaBlend 实体批（弹幕 PreDraw），End 后还原
    /// </summary>
    internal static class BloodQuadBatch
    {
        /// <summary>血团着色器可见体半径 = 0.52 quad 半幅，可见尺寸→quad 尺寸的折算</summary>
        public const float BlobQuadScale = 1f / 0.52f;
        /// <summary>溅片 quad 中心 = 源点 + 方向 × 此值 × 半幅，使基部圆弧正落在源点上（与 .fx 内 (-0.6,0)/0.28 对应）</summary>
        public const float SheetBaseOffset = 0.32f;
        /// <summary>顶点色 G 的半幅折算基准，与 .fx 内 *64 对应</summary>
        private const float HalfPxScale = 64f;
        /// <summary>顶点色 B 的长宽比折算基准，与 .fx 内 *8 对应</summary>
        private const float AspectScale = 8f;

        public static bool Begin(SpriteBatch sb, out Effect blob, out Effect sheet) {
            blob = EffectLoader.BRelicIrisBlob?.Value;
            sheet = EffectLoader.BRelicIrisSheet?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (blob == null || sheet == null || noise == null) {
                return false;
            }
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.graphics.GraphicsDevice;
            //噪声显式绑 s1（两支 shader 内均为 register(s1)）
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            float time = Main.GlobalTimeWrappedHourly;
            blob.Parameters["uTime"]?.SetValue(time);
            sheet.Parameters["uTime"]?.SetValue(time);
            return true;
        }

        public static void End(SpriteBatch sb) {
            sb.End();
            Main.graphics.GraphicsDevice.Textures[1] = null;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>切到血团组：撕裂/背风丝根/热度/回流一次上载并 Apply</summary>
        public static void BlobGroup(Effect blob, float tear, float backTear, float heat, float flow) {
            blob.Parameters["uTear"]?.SetValue(tear);
            blob.Parameters["uBackTear"]?.SetValue(backTear);
            blob.Parameters["uHeat"]?.SetValue(heat);
            blob.Parameters["uFlow"]?.SetValue(flow);
            blob.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>画一团：lenPx/widPx 为可见体尺寸，dir 为迎风方向</summary>
        public static void DrawBlob(SpriteBatch sb, Vector2 worldPos, Vector2 dir, float lenPx, float widPx, float seed, float alpha) {
            if (alpha <= 0.01f || widPx < 0.8f || lenPx < 0.8f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float quadLen = lenPx * BlobQuadScale;
            float quadWid = widPx * BlobQuadScale;
            Color packed = new(seed - MathF.Floor(seed), quadWid * 0.5f / HalfPxScale, lenPx / widPx / AspectScale, MathHelper.Clamp(alpha, 0f, 1f));
            sb.Draw(pixel, worldPos - Main.screenPosition, null, packed, dir.ToRotation(),
                pixel.Size() * 0.5f, new Vector2(quadLen / pixel.Width, quadWid / pixel.Height), SpriteEffects.None, 0f);
        }

        /// <summary>切到溅片组：扇角一次上载并 Apply</summary>
        public static void SheetGroup(Effect sheet, float span) {
            sheet.Parameters["uSpan"]?.SetValue(span);
            sheet.CurrentTechnique.Passes[0].Apply();
        }

        /// <summary>画一片：source 为溅射源点（基部圆弧落点），sizePx 为 quad 边长</summary>
        public static void DrawSheet(SpriteBatch sb, Vector2 source, Vector2 dir, float sizePx, float seed, float spread, float tear, float alpha) {
            if (alpha <= 0.01f || sizePx < 4f) {
                return;
            }
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 center = source + dir * (SheetBaseOffset * sizePx * 0.5f);
            Color packed = new(seed - MathF.Floor(seed), MathHelper.Clamp(spread, 0f, 1f), MathHelper.Clamp(tear, 0f, 1f), MathHelper.Clamp(alpha, 0f, 1f));
            sb.Draw(pixel, center - Main.screenPosition, null, packed, dir.ToRotation(),
                pixel.Size() * 0.5f, new Vector2(sizePx / pixel.Width, sizePx / pixel.Height), SpriteEffects.None, 0f);
        }
    }
}
