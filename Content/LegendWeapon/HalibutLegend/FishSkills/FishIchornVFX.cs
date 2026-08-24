using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>灵流蚀甲域内 shader 资源（域内加载器，不经 EffectLoader）</summary>
    internal class FishIchornAssets
    {
        /// <summary>金色灵液液柱条带</summary>
        [VaultLoaden(CWRConstant.Effects)]
        public static Effect FishIchornJet { get; private set; }
    }

    /// <summary>灵流蚀甲</summary>
    internal static class FishIchornVFX
    {
        /// <summary>暗金基底（外缘、干涸渍）</summary>
        public static readonly Color IchorDark = new(86, 56, 8);
        /// <summary>深金（过渡、沉液）</summary>
        public static readonly Color IchorDeep = new(150, 102, 16);
        /// <summary>高饱和金黄（主色）</summary>
        public static readonly Color IchorGold = new(234, 176, 36);
        /// <summary>亮芯，仅限液锋与湿面高光的极小面积</summary>
        public static readonly Color IchorBright = new(255, 230, 118);

        /// <summary>确定性哈希 0..1，绘制路径专用（不吃 Main.rand，帧间稳定）</summary>
        public static float Hash(int a, int b) {
            float v = MathF.Sin(a * 12.9898f + b * 78.233f) * 43758.5453f;
            return v - MathF.Floor(v);
        }


        /// <summary>灵液液柱条带</summary>
        public static void DrawJetTrail(Projectile projectile, ref Trail trail
            , TrailThicknessCalculator widthFunc, TrailColorEvaluator colorFunc, float fade) {
            Effect fx = FishIchornAssets.FishIchornJet;
            if (fx == null || projectile.oldPos == null || projectile.oldPos.Length == 0) {
                return;
            }
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(projectile.whoAmI * 0.731f % 3.71f);
            fx.Parameters["uFade"]?.SetValue(fade);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColDark"]?.SetValue(IchorDark.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(IchorDeep.ToVector3());
            fx.Parameters["uColGold"]?.SetValue(IchorGold.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(IchorBright.ToVector3());

            Vector2[] positions = new Vector2[projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                if (projectile.oldPos[i] == Vector2.Zero) {
                    projectile.oldPos[i] = projectile.position;
                }
                positions[i] = projectile.oldPos[i] + projectile.Size * 0.5f;
            }
            trail ??= new Trail(positions, widthFunc, colorFunc);
            trail.TrailPositions = positions;
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
            trail.DrawTrail(fx);
        }


        /// <summary>出膛喷吐</summary>
        public static void MuzzleSpray(Vector2 pos, Vector2 dir) {
            if (Main.dedServ) {
                return;
            }
            dir = dir.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 7; i++) {
                Vector2 vel = dir.RotatedByRandom(0.28f) * Main.rand.NextFloat(5f, 13f);
                Color col = Main.rand.NextBool(3) ? IchorDeep : IchorGold;
                PRTLoader.NewParticle<PRT_FishIchornDroplet>(pos + Main.rand.NextVector2Circular(4f, 4f)
                    , vel, col, Main.rand.NextFloat(0.5f, 0.85f))?.Configure(Main.rand.Next(16, 26));
            }
            //微射线滴
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_FishIchornDroplet>(pos, dir.RotatedByRandom(0.1f) * Main.rand.NextFloat(16f, 22f)
                    , IchorBright, Main.rand.NextFloat(0.4f, 0.6f))?.Configure(Main.rand.Next(5, 9), 0.1f, 0.88f);
            }
            PRTLoader.NewParticle<PRT_DWave>(pos + dir * 8f, Vector2.Zero, IchorDeep, 0.08f)
                ?.Configure(new Vector2(0.55f, 1f), dir.ToRotation(), 0.26f, 9);
        }

        /// <summary>命中迸溅</summary>
        public static void SplashBurst(Vector2 pos, Vector2 impactVel, bool onTile) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float speed = impactVel.Length();
            float ke = MathHelper.Clamp(speed / 24f, 0.3f, 1f);
            float mainAngle = normal.ToRotation();

            //半球迸溅
            int count = (int)(8 + 6 * ke);
            for (int i = 0; i < count; i++) {
                float spread = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                float speedRatio = 1f - MathF.Abs(spread) / MathHelper.PiOver2;
                Vector2 vel = (mainAngle + spread).ToRotationVector2()
                    * Main.rand.NextFloat(2.5f, 9f) * (0.35f + 0.65f * speedRatio) * (0.5f + ke);
                Color col = Main.rand.NextBool(3) ? IchorDeep : IchorGold;
                PRTLoader.NewParticle<PRT_FishIchornDroplet>(pos + Main.rand.NextVector2Circular(6f, 6f)
                    , vel, col, Main.rand.NextFloat(0.5f, 0.95f))?.Configure(Main.rand.Next(22, 40));
            }
            //沉重液团，更大更慢，坠得更急
            for (int i = 0; i < 3; i++) {
                Vector2 vel = normal.RotatedByRandom(0.7f) * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_FishIchornDroplet>(pos, vel, IchorDeep
                    , Main.rand.NextFloat(1.05f, 1.5f))?.Configure(Main.rand.Next(30, 48), 0.4f);
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, IchorDeep, 0.1f)
                ?.Configure(new Vector2(0.7f, 1f), mainAngle, 0.3f + 0.22f * ke, 10);
            //原版灵液尘只做底噪填充
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Ichor
                    , normal.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.5f, 4f), 100, default, Main.rand.NextFloat(1f, 1.6f));
                d.noGravity = Main.rand.NextBool();
            }
            if (onTile) {
                PRTLoader.NewParticle<PRT_FishIchornSmear>(pos + normal * 2f, Vector2.Zero, IchorGold
                    , Main.rand.NextFloat(0.9f, 1.25f))?.Configure(Main.rand.Next(100, 140));
            }
            //只有全速射流的撞击才配一记克制的定向震
            if (speed > 14f && CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    pos, impactVel.SafeNormalize(Vector2.UnitY), 2f, 7f, 6, 520f, "FishIchorn"));
            }
        }
    }

    /// <summary>灵液液滴</summary>
    internal class PRT_FishIchornDroplet : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;

        private Color initialColor;
        private float gravity;
        private float drag;

        public PRT_FishIchornDroplet Configure(int lifetime, float gravityPerFrame = 0.34f, float dragMul = 0.986f) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            drag = dragMul;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = 0f;
            drag = 1f;
        }

        public override void AI() {
            Velocity.X *= drag;
            Velocity.Y += gravity;
            if (Velocity.Y > 15f) {
                Velocity.Y = 15f;
            }

            float t = LifetimeCompletion;
            Scale *= 0.986f;
            //坠落中先保持鲜亮后干涸转暗，透明度尾段陡降
            Color = Color.Lerp(initialColor, FishIchornVFX.IchorDark, MathF.Pow(t, 1.6f) * 0.75f);
            Opacity = 1f - MathF.Pow(t, 3f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            //随速度纵向拉伸
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.9f);
            Vector2 scale = new Vector2(0.36f * (1f - stretch * 0.4f), 0.6f * (1f + stretch * 1.8f)) * Scale;

            Color body = Color * Opacity;
            Color rim = Color.Lerp(Color, FishIchornVFX.IchorDark, 0.55f) * Opacity;
            //暗金压边略宽一圈，给液滴体积感
            spriteBatch.Draw(tex, pos, null, rim, Rotation, origin, scale * new Vector2(1.35f, 1.06f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, Rotation, origin, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);

            //新鲜期湿面反光
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 2.2f, 0f, 1f);
            if (fresh > 0.05f) {
                Color glint = FishIchornVFX.IchorBright with { A = 0 };
                spriteBatch.Draw(tex, pos, null, glint * (0.45f * fresh * Opacity), Rotation, origin
                    , scale * new Vector2(0.2f, 0.55f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>金渍残留 decal</summary>
    internal class PRT_FishIchornSmear : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 200;

        private int seed;
        private Color initialColor;

        public PRT_FishIchornSmear Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            seed = 0;
            initialColor = default;
        }

        public override void SetProperty() {
            seed = Main.rand.Next(100000);
            Velocity = Vector2.Zero;
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //贴上即定型，尾段缓慢干涸淡出
            Opacity = MathHelper.Clamp(Time / 4f, 0f, 1f) * (1f - MathF.Pow(t, 2.4f));
            //渍体越新滴得越勤，挂壁滴淌
            if (t < 0.7f && Main.rand.NextBool(26)) {
                float dx = (FishIchornVFX.Hash(seed, 20) - 0.5f) * 12f;
                PRTLoader.NewParticle<PRT_FishIchornDroplet>(Position + new Vector2(dx, 6f)
                    , new Vector2(0f, Main.rand.NextFloat(0.4f, 1.2f)), FishIchornVFX.IchorDeep
                    , Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(18, 30), 0.3f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 basePos = Position - Main.screenPosition;
            float t = LifetimeCompletion;
            //鲜金 → 干涸暗金的色程；渍面随时间失去光泽
            Color wet = Color.Lerp(initialColor, FishIchornVFX.IchorDark, 0.3f + 0.65f * t);

            //三团渍斑
            for (int i = 0; i < 3; i++) {
                Vector2 off = new((FishIchornVFX.Hash(seed, i) - 0.5f) * 16f, (FishIchornVFX.Hash(seed, i + 3) - 0.5f) * 10f);
                float rot = (FishIchornVFX.Hash(seed, i + 6) - 0.5f) * 0.9f;
                float blobScale = (0.42f + FishIchornVFX.Hash(seed, i + 9) * 0.3f) * Scale;
                //滴淌，渍斑沿重力方向缓慢拉长
                float run = 1f + t * (0.7f + FishIchornVFX.Hash(seed, i + 12) * 0.9f);
                Vector2 blobSize = new Vector2(blobScale * 0.66f, blobScale * 0.52f * run);
                //暗金蚀底压一圈
                spriteBatch.Draw(tex, basePos + off, null, FishIchornVFX.IchorDark * (0.6f * Opacity), rot, origin
                    , blobSize * new Vector2(1.3f, 1.08f), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, basePos + off, null, wet * (0.85f * Opacity), rot, origin
                    , blobSize, SpriteEffects.None, 0f);
            }

            //中央滴淌线
            float runnel = 0.35f + t * 1.4f;
            spriteBatch.Draw(tex, basePos + new Vector2(0f, runnel * 9f), null, FishIchornVFX.IchorDeep * (0.7f * Opacity)
                , 0f, origin, new Vector2(0.1f, runnel) * Scale, SpriteEffects.None, 0f);

            //新鲜期湿面反光
            float sheen = MathF.Max(0f, 1f - t * 2.8f);
            if (sheen > 0.05f) {
                Color glint = FishIchornVFX.IchorBright with { A = 0 };
                spriteBatch.Draw(tex, basePos, null, glint * (0.35f * sheen * Opacity), 0.3f, origin
                    , new Vector2(0.22f, 0.3f) * Scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>蚀甲可视化</summary>
    internal class FishIchornErosion : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>蚀纹剩余展示时长，命中时刷新为 debuff 时长</summary>
        private int tagTime;
        /// <summary>侵蚀加深度 0..1，持续命中与存续中缓慢生长</summary>
        private float grow;

        public void Tag(int duration) {
            tagTime = Math.Max(tagTime, duration);
            grow = MathF.Min(1f, grow + 0.2f);
        }

        public override void PostAI(NPC npc) {
            if (tagTime <= 0) {
                return;
            }
            if (--tagTime <= 0) {
                grow = 0f;
                return;
            }
            grow = MathF.Min(1f, grow + 1f / 240f);
            if (Main.dedServ || !npc.HasBuff(BuffID.Ichor)) {
                return;
            }
            //蚀甲滴金
            if (Main.rand.NextBool(22)) {
                Vector2 pos = npc.Center + new Vector2(Main.rand.NextFloat(-0.4f, 0.4f) * npc.width
                    , Main.rand.NextFloat(-0.2f, 0.45f) * npc.height);
                PRTLoader.NewParticle<PRT_FishIchornDroplet>(pos, npc.velocity * 0.3f + new Vector2(0f, 0.6f)
                    , FishIchornVFX.IchorDeep, Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(20, 32), 0.3f);
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (tagTime <= 0 || npc.IsABestiaryIconDummy || !npc.HasBuff(BuffID.Ichor)) {
                return;
            }
            Texture2D streak = CWRAsset.Extra_98?.Value;
            if (streak == null) {
                return;
            }
            Vector2 origin = streak.Size() * 0.5f;
            //尾段整体淡出，蚀得越深越醒目
            float fade = MathHelper.Clamp(tagTime / 40f, 0f, 1f) * (0.55f + 0.45f * grow);
            float bodyScale = MathHelper.Clamp(MathF.Max(npc.width, npc.height) / 70f, 0.55f, 1.5f);

            for (int i = 0; i < 3; i++) {
                Vector2 pos = npc.Center + new Vector2(
                    (FishIchornVFX.Hash(npc.whoAmI, i) - 0.5f) * npc.width * 0.7f,
                    (FishIchornVFX.Hash(npc.whoAmI, i + 3) - 0.5f) * npc.height * 0.7f) - screenPos;
                //蚀纹大体顺重力走向，微量偏摆
                float rot = (FishIchornVFX.Hash(npc.whoAmI, i + 6) - 0.5f) * 0.9f;
                //相位错开的搏动
                float pulse = 0.72f + 0.28f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f + i * 2.4f + npc.whoAmI);
                Vector2 veinScale = new Vector2(0.16f, 0.5f + 0.45f * grow) * bodyScale;

                //暗金蚀底
                spriteBatch.Draw(streak, pos, null, FishIchornVFX.IchorDark * (0.62f * fade), rot, origin
                    , veinScale * new Vector2(2.1f, 1.12f), SpriteEffects.None, 0f);
                //金色灵液芯（A=0 加色）随搏动明灭
                Color core = FishIchornVFX.IchorGold with { A = 0 };
                spriteBatch.Draw(streak, pos, null, core * (0.8f * pulse * fade), rot, origin
                    , veinScale, SpriteEffects.None, 0f);
                spriteBatch.Draw(streak, pos, null, (FishIchornVFX.IchorBright with { A = 0 }) * (0.35f * pulse * fade)
                    , rot, origin, veinScale * new Vector2(0.45f, 0.8f), SpriteEffects.None, 0f);
            }
        }
    }
}
