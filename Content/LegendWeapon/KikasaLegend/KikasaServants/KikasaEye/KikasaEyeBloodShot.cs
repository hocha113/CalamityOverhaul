using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills;
using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaEye
{
    /// <summary>
    /// 鬼奴克眼的血痰：一口有体积的粘稠血团，不是光条。
    /// 头部三层液团（暗血压边→血红主体→血沫亮芯湿反光）带表面张力抖动，
    /// 身后拖一条会珠化断裂的粘血线（复用灵液液柱条带 shader，换血色板），
    /// 飞行中失稳甩珠、吃重力走抛物线；命中/贴壁半球迸溅+沉重血团，
    /// 壁面留会往下滴淌的血渍；落空坠回血湖时被湖收走
    /// </summary>
    internal class KikasaEyeBloodShot : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>出膛后多少帧开始吃重力：痰是抛出去的，不是射出去的</summary>
        private const int GravityDelay = 8;

        private ref float Life => ref Projectile.ai[0];

        private Trail trail;
        //贴壁演出已放，OnKill 不再补迸溅
        private bool burstDone;
        //被湖收走：谢幕换成涟漪，不走迸溅
        private bool lakeSwallowed;

        //==================== 血色板（随观看域鬼雨异化冷化，与湖系同族）====================

        internal static Color BloodDark => KikasaDomain.CoolTint(new(64, 12, 14), new(38, 48, 52));
        internal static Color BloodDeep => KikasaDomain.CoolTint(new(140, 32, 30), new(84, 104, 110));
        internal static Color BloodMain => KikasaDomain.CoolTint(new(237, 77, 69), new(126, 158, 164));
        internal static Color BloodBright => KikasaDomain.CoolTint(new(246, 133, 112), new(176, 200, 204));

        /// <summary>连续量抖动的确定性相位（9.1：绘制路径不掷 Main.rand）</summary>
        private float Seed => Projectile.identity * 0.7391f % 3.71f;

        /// <summary>出生 4 帧淡入，避免第一帧硬弹出</summary>
        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;

            //抛物线：短暂平直后被重量拽下去；粘性阻力让水平段也在缓慢泄劲
            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.22f, 16f);
            }
            Projectile.velocity *= 0.995f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //表面张力失稳：从团身后侧撕下小血珠，横向微散
            if (!Main.dedServ && Life % 3 == 0) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Vector2 spawnPos = Projectile.Center - dir * Main.rand.NextFloat(6f, 16f);
                Vector2 dropVel = Projectile.velocity * Main.rand.NextFloat(0.2f, 0.45f)
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1.2f, 1.2f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(spawnPos, dropVel,
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(16, 28));
            }

            float glow = 0.45f * VisualFade;
            Lighting.AddLight(Projectile.Center, 0.5f * glow, 0.12f * glow, 0.11f * glow);

            //落空坠回血湖：湖收回自己的血，不迸溅
            Player owner = Main.player[Projectile.owner];
            if (owner?.active == true
                && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                && domain.AnyActive && domain.RiseT > 0.5f
                && Projectile.Center.Y >= domain.LakeWorldY + 4f) {
                lakeSwallowed = true;
                if (!Main.dedServ && KikasaDomain.Viewed == domain) {
                    KikasaDomainDeco.RippleAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 0.75f);
                    KikasaDomainDeco.SplashAt(new Vector2(Projectile.Center.X, domain.LakeWorldY), 4);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.4f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                Projectile.Kill();
            }
        }

        //==================== 命中与谢幕 ====================

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //贴壁：迸溅 + 血渍 decal，渍会挂壁滴淌
            burstDone = true;
            SplashBurst(Projectile.Center, oldVelocity, onTile: true);
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || lakeSwallowed) {
                return;
            }
            if (!burstDone) {
                //命中 NPC / 超时坠灭共用（penetrate=1，Kill 各端都跑，队友也看得见）
                SplashBurst(Projectile.Center, Projectile.velocity, onTile: false);
            }
            //血线失压散珠：拖尾旧位上留几粒回落的残珠
            Vector2[] oldPos = Projectile.oldPos;
            if (oldPos == null) {
                return;
            }
            for (int i = 2; i < oldPos.Length; i += 4) {
                if (oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 pos = oldPos[i] + Projectile.Size * 0.5f;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(4f, 4f),
                    Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.7f, 0.7f),
                    Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.3f, 0.55f))?.Configure(Main.rand.Next(16, 26));
            }
        }

        /// <summary>命中迸溅：半球血珠扇 + 沉重血团 + 扩散环 + 原版血尘垫底；贴壁再留渍</summary>
        internal static void SplashBurst(Vector2 pos, Vector2 impactVel, bool onTile) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            float ke = MathHelper.Clamp(impactVel.Length() / 20f, 0.3f, 1f);
            float mainAngle = normal.ToRotation();

            //半球迸溅：越贴法线越快
            int count = (int)(6 + 5 * ke);
            for (int i = 0; i < count; i++) {
                float spread = Main.rand.NextFloat(-MathHelper.PiOver2, MathHelper.PiOver2);
                float speedRatio = 1f - MathF.Abs(spread) / MathHelper.PiOver2;
                Vector2 vel = (mainAngle + spread).ToRotationVector2()
                    * Main.rand.NextFloat(2f, 7.5f) * (0.35f + 0.65f * speedRatio) * (0.5f + ke);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos + Main.rand.NextVector2Circular(5f, 5f),
                    vel, Main.rand.NextBool(3) ? BloodDeep : BloodMain,
                    Main.rand.NextFloat(0.4f, 0.8f))?.Configure(Main.rand.Next(20, 34));
            }
            //沉重血团：更大更慢，坠得更急
            for (int i = 0; i < 2; i++) {
                Vector2 vel = normal.RotatedByRandom(0.7f) * Main.rand.NextFloat(1.6f, 4f);
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(pos, vel, BloodDeep,
                    Main.rand.NextFloat(0.9f, 1.25f))?.Configure(Main.rand.Next(26, 42), 0.4f);
            }
            PRTLoader.NewParticle<PRT_DWave>(pos, Vector2.Zero, BloodDeep, 0.08f)
                ?.Configure(new Vector2(0.7f, 1f), mainAngle, 0.24f + 0.18f * ke, 9);
            //原版血尘只做底噪
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(pos, DustID.Blood,
                    normal.RotatedByRandom(0.9f) * Main.rand.NextFloat(1.2f, 3.5f), 100, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = Main.rand.NextBool();
            }
            if (onTile) {
                PRTLoader.NewParticle<PRT_KikasaBloodSmear>(pos + normal * 2f, Vector2.Zero, BloodMain,
                    Main.rand.NextFloat(0.85f, 1.15f))?.Configure(Main.rand.Next(90, 130));
            }

            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.4f, Pitch = -0.1f, MaxInstances = 3 }, pos);
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.4f, Pitch = -0.25f, MaxInstances = 3 }, pos);
        }

        //==================== 绘制 ====================

        public float GetWidthFunc(float completionRatio)
            => MathHelper.Lerp(8.5f, 1.2f, completionRatio) * VisualFade; //0=团后颈最宽，尾端收成丝

        public Color GetColorFunc(Vector2 coord) => Color.White;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !Projectile.active || VisualFade <= 0.01f) {
                return;
            }
            DrawSlimeTrail();

            //液团头部画在条带之上
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            DrawGlobHead(sb);
            sb.End();
        }

        /// <summary>粘血线条带：借灵液液柱 shader（四色全参数化），换血色板；尾段自带珠化断裂</summary>
        private void DrawSlimeTrail() {
            Effect fx = FishIchornAssets.FishIchornJet;
            if (fx == null || Projectile.oldPos == null || Projectile.oldPos.Length == 0) {
                return;
            }
            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uFade"]?.SetValue(VisualFade * 0.9f);
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (noise != null) {
                fx.Parameters["uNoiseTex"]?.SetValue(noise);
            }
            fx.Parameters["uColDark"]?.SetValue(BloodDark.ToVector3());
            fx.Parameters["uColDeep"]?.SetValue(BloodDeep.ToVector3());
            fx.Parameters["uColGold"]?.SetValue(BloodMain.ToVector3());
            fx.Parameters["uColBright"]?.SetValue(BloodBright.ToVector3());

            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    Projectile.oldPos[i] = Projectile.position;
                }
                positions[i] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            trail ??= new Trail(positions, GetWidthFunc, GetColorFunc);
            trail.TrailPositions = positions;
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
            trail.DrawTrail(fx);
        }

        /// <summary>液团头部：暗血压边→血红主体→血沫亮芯，表面张力抖动 + 速度拉伸</summary>
        private void DrawGlobHead(SpriteBatch sb) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center + Projectile.velocity * 0.35f - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            float rotation = Projectile.rotation;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.032f, 0.15f, 0.8f);

            //表面张力抖动：宽窄反相呼吸，痰在飞行里晃
            float wob = MathF.Sin(Life * 0.55f + Seed * 6f) * 0.12f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);

            //暗血压边
            sb.Draw(tex, pos, null, BloodDark * (0.85f * fade), rotation, origin,
                new Vector2(0.52f, 0.56f + stretch * 0.85f) * jiggle, SpriteEffects.None, 0f);
            //血红主体
            sb.Draw(tex, pos, null, BloodMain * fade, rotation, origin,
                new Vector2(0.4f, 0.46f + stretch * 0.75f) * jiggle, SpriteEffects.None, 0f);
            //血沫亮芯：极小面积加色湿反光
            Color core = BloodBright with { A = 0 };
            sb.Draw(tex, pos, null, core * (0.6f * fade), rotation, origin,
                new Vector2(0.14f, 0.24f + stretch * 0.3f) * jiggle, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>血珠：暗缘压边给体积、新鲜期湿反光、坠落中渐干转暗（血色版灵液滴语法）</summary>
    internal class PRT_KikasaBloodGlob : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 500;

        private Color initialColor;
        private float gravity;
        private float drag;

        public PRT_KikasaBloodGlob Configure(int lifetime, float gravityPerFrame = 0.34f, float dragMul = 0.986f) {
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

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 24;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            Velocity.X *= drag;
            Velocity.Y = MathF.Min(Velocity.Y + gravity, 15f);

            float t = LifetimeCompletion;
            Scale *= 0.986f;
            //先鲜亮后凝暗，透明度尾段陡降
            Color = Color.Lerp(initialColor, KikasaEyeBloodShot.BloodDark, MathF.Pow(t, 1.6f) * 0.75f);
            Opacity = 1f - MathF.Pow(t, 3f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.05f, 0f, 0.9f);
            Vector2 scale = new Vector2(0.36f * (1f - stretch * 0.4f), 0.6f * (1f + stretch * 1.8f)) * Scale;

            Color body = Color * Opacity;
            Color rim = Color.Lerp(Color, KikasaEyeBloodShot.BloodDark, 0.55f) * Opacity;
            //暗血压边略宽一圈，给血珠体积感
            spriteBatch.Draw(tex, pos, null, rim, Rotation, origin, scale * new Vector2(1.35f, 1.06f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, body, Rotation, origin, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);

            //新鲜期湿面反光
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 2.2f, 0f, 1f);
            if (fresh > 0.05f) {
                Color glint = KikasaEyeBloodShot.BloodBright with { A = 0 };
                spriteBatch.Draw(tex, pos, null, glint * (0.4f * fresh * Opacity), Rotation, origin,
                    scale * new Vector2(0.2f, 0.55f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }

    /// <summary>血渍 decal：三团渍斑贴壁定型、沿重力拉长滴淌，新鲜期反光、尾段干涸淡出</summary>
    internal class PRT_KikasaBloodSmear : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 120;

        private int seed;
        private Color initialColor;

        public PRT_KikasaBloodSmear Configure(int lifetime) {
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
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            seed = Main.rand.Next(100000);
            Velocity = Vector2.Zero;
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            //贴上即定型，尾段干涸淡出
            Opacity = MathHelper.Clamp(Time / 4f, 0f, 1f) * (1f - MathF.Pow(t, 2.4f));
            //渍越新滴得越勤，挂壁滴淌
            if (t < 0.7f && Main.rand.NextBool(26)) {
                float dx = (FishIchornVFX.Hash(seed, 20) - 0.5f) * 12f;
                PRTLoader.NewParticle<PRT_KikasaBloodGlob>(Position + new Vector2(dx, 6f),
                    new Vector2(0f, Main.rand.NextFloat(0.4f, 1.1f)), KikasaEyeBloodShot.BloodDeep,
                    Main.rand.NextFloat(0.28f, 0.45f))?.Configure(Main.rand.Next(16, 28), 0.3f);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 basePos = Position - Main.screenPosition;
            float t = LifetimeCompletion;
            //鲜血 → 凝暗的色程
            Color wet = Color.Lerp(initialColor, KikasaEyeBloodShot.BloodDark, 0.3f + 0.65f * t);

            //三团渍斑，滴淌方向沿重力缓慢拉长
            for (int i = 0; i < 3; i++) {
                Vector2 off = new((FishIchornVFX.Hash(seed, i) - 0.5f) * 14f, (FishIchornVFX.Hash(seed, i + 3) - 0.5f) * 9f);
                float rot = (FishIchornVFX.Hash(seed, i + 6) - 0.5f) * 0.9f;
                float blobScale = (0.4f + FishIchornVFX.Hash(seed, i + 9) * 0.28f) * Scale;
                float run = 1f + t * (0.7f + FishIchornVFX.Hash(seed, i + 12) * 0.9f);
                Vector2 blobSize = new(blobScale * 0.64f, blobScale * 0.5f * run);
                spriteBatch.Draw(tex, basePos + off, null, KikasaEyeBloodShot.BloodDark * (0.6f * Opacity), rot, origin,
                    blobSize * new Vector2(1.3f, 1.08f), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, basePos + off, null, wet * (0.85f * Opacity), rot, origin,
                    blobSize, SpriteEffects.None, 0f);
            }

            //中央滴淌线
            float runnel = 0.35f + t * 1.3f;
            spriteBatch.Draw(tex, basePos + new Vector2(0f, runnel * 8f), null, KikasaEyeBloodShot.BloodDeep * (0.65f * Opacity),
                0f, origin, new Vector2(0.1f, runnel) * Scale, SpriteEffects.None, 0f);

            //新鲜期湿面反光
            float sheen = MathF.Max(0f, 1f - t * 2.8f);
            if (sheen > 0.05f) {
                Color glint = KikasaEyeBloodShot.BloodBright with { A = 0 };
                spriteBatch.Draw(tex, basePos, null, glint * (0.3f * sheen * Opacity), 0.3f, origin,
                    new Vector2(0.2f, 0.28f) * Scale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
