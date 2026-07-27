using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend
{
    /// <summary>
    /// 海洋洪流的客户端表现入口。弹幕只提交初始条件，后续生命周期全部由 PRT 接管
    /// </summary>
    internal static class OceanCurrentVFX
    {
        internal static readonly Color DeepOcean = new(10, 42, 72);
        internal static readonly Color ShallowOcean = new(32, 126, 184);
        internal static readonly Color WaterBright = new(92, 202, 236);
        internal static readonly Color OceanFoam = new(210, 240, 248);
        internal static readonly Color Seaweed = new(42, 142, 126);

        public static void EmitStream(Vector2 center, Vector2 streamVelocity, int time, float wavePhase) {
            if (Main.dedServ) {
                return;
            }

            float speed = streamVelocity.Length();
            Vector2 direction = streamVelocity.SafeNormalize(Vector2.UnitX);
            Vector2 tangent = direction.RotatedBy(MathHelper.PiOver2);

            if (time % 2 == 0) {
                float capillaryWave = MathF.Sin(wavePhase + time * 0.31f) * Main.rand.NextFloat(0.35f, 1.1f);
                Vector2 position = center
                    - direction * Main.rand.NextFloat(1f, 16f)
                    + tangent * Main.rand.NextFloat(-10f, 10f);
                Vector2 velocity = streamVelocity * Main.rand.NextFloat(0.58f, 0.94f)
                    + tangent * (capillaryWave + Main.rand.NextFloat(-0.75f, 0.75f))
                    - Vector2.UnitY * Main.rand.NextFloat(0f, 0.45f);
                Color color = Color.Lerp(DeepOcean, ShallowOcean, Main.rand.NextFloat(0.2f, 0.9f));

                PRTLoader.NewParticle<PRT_OceanCurrentDroplet>(position, velocity, color
                    , Main.rand.NextFloat(0.13f, 0.24f))
                    ?.Configure(Main.rand.Next(34, 58), gravityPerFrame: 0.20f, dragMultiplier: 0.986f
                        , turbulence: Main.rand.NextFloat(0.015f, 0.045f), canSplit: speed > 7f && Main.rand.NextBool(3));
            }

            if (time % 4 == 0) {
                Vector2 position = center
                    - direction * Main.rand.NextFloat(4f, 20f)
                    + tangent * Main.rand.NextFloat(-11f, 11f);
                Vector2 velocity = -streamVelocity * Main.rand.NextFloat(0.025f, 0.075f)
                    + tangent * Main.rand.NextFloat(-0.45f, 0.45f)
                    - Vector2.UnitY * Main.rand.NextFloat(0.15f, 0.75f);
                PRTLoader.NewParticle<PRT_OceanCurrentFoam>(position, velocity, OceanFoam
                    , Main.rand.NextFloat(0.055f, 0.105f))
                    ?.Configure(Main.rand.Next(34, 58), Main.rand.NextFloat(0.025f, 0.055f));
            }

            if (time % 8 == 0) {
                Vector2 wakePosition = center - direction * Main.rand.NextFloat(10f, 22f);
                PRTLoader.NewParticle<PRT_OceanCurrentWake>(wakePosition, streamVelocity * 0.08f
                    , ShallowOcean, 0.055f)
                    ?.Configure(direction, new Vector2(1f, 0.42f), 0.24f, Main.rand.Next(10, 15));
            }

            if (time % 28 == 0) {
                bool fish = Main.rand.NextBool();
                Vector2 position = center + Main.rand.NextVector2Circular(18f, 16f);
                Vector2 velocity = streamVelocity * Main.rand.NextFloat(0.24f, 0.48f)
                    + Main.rand.NextVector2Circular(0.7f, 0.5f);
                Color color = fish ? WaterBright : Seaweed;
                PRTLoader.NewParticle<PRT_OceanCurrentMarineMote>(position, velocity, color
                    , Main.rand.NextFloat(0.13f, 0.23f))
                    ?.Configure(fish, Main.rand.Next(48, 82));
            }
        }

        public static void EmitSpent(Vector2 center, Vector2 velocity, int time) {
            if (Main.dedServ || time % 5 != 0) {
                return;
            }

            Vector2 drift = velocity * 0.16f + Main.rand.NextVector2Circular(0.8f, 0.55f);
            PRTLoader.NewParticle<PRT_OceanCurrentFoam>(center + Main.rand.NextVector2Circular(10f, 8f)
                , drift - Vector2.UnitY * 0.25f, OceanFoam, Main.rand.NextFloat(0.05f, 0.09f))
                ?.Configure(Main.rand.Next(24, 40), 0.04f);
        }

        public static void SplashBurst(Vector2 position, Vector2 impactVelocity, float scale = 1f) {
            if (Main.dedServ) {
                return;
            }

            float impactSpeed = MathHelper.Clamp(impactVelocity.Length(), 2f, 30f);
            float energy = MathHelper.Clamp(impactSpeed / 16f, 0.45f, 1.65f) * scale;
            Vector2 rebound = -impactVelocity.SafeNormalize(-Vector2.UnitY);
            rebound = (rebound - Vector2.UnitY * 0.24f).SafeNormalize(-Vector2.UnitY);
            Vector2 tangent = rebound.RotatedBy(MathHelper.PiOver2);

            PRTLoader.NewParticle<PRT_OceanCurrentWake>(position, Vector2.Zero, WaterBright, 0.07f * scale)
                ?.Configure(tangent, new Vector2(1f, 0.48f), 0.48f * energy, Main.rand.Next(12, 17));

            int dropletCount = Math.Clamp((int)(10f + impactSpeed * 0.7f * scale), 10, 30);
            for (int i = 0; i < dropletCount; i++) {
                float lateral = Main.rand.NextFloat(-0.95f, 0.95f);
                Vector2 sprayDirection = (rebound * Main.rand.NextFloat(0.35f, 1f)
                    + tangent * lateral
                    - Vector2.UnitY * Main.rand.NextFloat(0.05f, 0.4f))
                    .SafeNormalize(rebound);
                float speed = Main.rand.NextFloat(2.2f, 6.5f + energy * 4.2f)
                    * MathHelper.Lerp(0.65f, 1f, 1f - Math.Abs(lateral));
                Color color = Color.Lerp(DeepOcean, WaterBright, Main.rand.NextFloat(0.2f, 0.8f));

                PRTLoader.NewParticle<PRT_OceanCurrentDroplet>(
                    position + Main.rand.NextVector2Circular(7f, 6f), sprayDirection * speed, color
                    , Main.rand.NextFloat(0.14f, 0.28f) * MathHelper.Clamp(scale, 0.65f, 1.25f))
                    ?.Configure(Main.rand.Next(38, 68), gravityPerFrame: 0.27f
                        , dragMultiplier: 0.984f, turbulence: Main.rand.NextFloat(0.01f, 0.035f)
                        , canSplit: speed > 5.5f && Main.rand.NextBool(2));
            }

            int foamCount = Math.Clamp((int)(4f + energy * 5f), 4, 12);
            for (int i = 0; i < foamCount; i++) {
                Vector2 velocity = rebound.RotatedByRandom(1.05f) * Main.rand.NextFloat(0.8f, 3.8f) * energy
                    - Vector2.UnitY * Main.rand.NextFloat(0.2f, 1.2f);
                PRTLoader.NewParticle<PRT_OceanCurrentFoam>(
                    position + Main.rand.NextVector2Circular(9f, 7f), velocity, OceanFoam
                    , Main.rand.NextFloat(0.06f, 0.13f) * MathHelper.Clamp(scale, 0.7f, 1.2f))
                    ?.Configure(Main.rand.Next(30, 54), Main.rand.NextFloat(0.025f, 0.06f));
            }
        }
    }

    /// <summary>
    /// 池化水滴。采用弹道、连续图格碰撞和表面薄膜三阶段近似，避免粒子间 O(n²) 邻域求解
    /// </summary>
    internal class PRT_OceanCurrentDroplet : BasePRT
    {
        private enum DropletPhase : byte
        {
            Flying,
            SurfaceFilm
        }

        public override string Texture => CWRConstant.Masking + "Spray";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 1200;

        private DropletPhase phase;
        private Color baseColor;
        private float initialScale;
        private float gravity;
        private float drag;
        private float turbulence;
        private float noisePhase;
        private int frameIndex;
        private bool canSplit;
        private int ignoreCollisionUntil;
        private int filmStartedAt;
        private int filmLifetime;
        private float filmSpeed;
        private Vector2 surfaceNormal;

        public PRT_OceanCurrentDroplet Configure(int lifetime, float gravityPerFrame = 0.24f
            , float dragMultiplier = 0.985f, float turbulence = 0.025f, bool canSplit = true) {
            Lifetime = lifetime;
            baseColor = Color;
            initialScale = Scale;
            gravity = gravityPerFrame;
            drag = dragMultiplier;
            this.turbulence = turbulence;
            this.canSplit = canSplit;

            float speed = Velocity.Length();
            frameIndex = speed < 2.5f
                ? Main.rand.Next(0, 3)
                : speed < 6f
                    ? Main.rand.Next(3, 6)
                    : Main.rand.Next(6, 9);
            return this;
        }

        public override void Reset() {
            base.Reset();
            phase = DropletPhase.Flying;
            baseColor = default;
            initialScale = 0f;
            gravity = 0f;
            drag = 0f;
            turbulence = 0f;
            noisePhase = 0f;
            frameIndex = 0;
            canSplit = false;
            ignoreCollisionUntil = 0;
            filmStartedAt = 0;
            filmLifetime = 0;
            filmSpeed = 0f;
            surfaceNormal = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 1f;
            noisePhase = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override bool ShouldUpdatePosition() => phase == DropletPhase.Flying;

        public override void AI() {
            if (phase == DropletPhase.SurfaceFilm) {
                SurfaceFilmAI();
                return;
            }

            Vector2 stepVelocity = Velocity;
            if (Time >= ignoreCollisionUntil && TryResolveSurface(stepVelocity, out Vector2 normal)) {
                EnterSurfaceFilm(normal, stepVelocity);
                return;
            }

            Vector2 direction = Velocity.SafeNormalize(Vector2.UnitX);
            Vector2 tangent = direction.RotatedBy(MathHelper.PiOver2);
            Velocity += tangent * (MathF.Sin(noisePhase + Time * 0.47f) * turbulence);
            Velocity.X *= drag;
            Velocity.Y = MathF.Min(Velocity.Y + gravity, 15f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            float life = MathHelper.Clamp(LifetimeCompletion, 0f, 1f);
            Scale = initialScale * MathHelper.Lerp(1f, 0.72f, life * life);
            Opacity = MathF.Min(Time / 3f, 1f) * SmoothStep01((1f - life) / 0.24f);
            Color = Color.Lerp(baseColor, DeepTint(baseColor), life * 0.38f);
        }

        private bool TryResolveSurface(Vector2 stepVelocity, out Vector2 normal) {
            normal = Vector2.Zero;
            if (stepVelocity.LengthSquared() < 0.04f) {
                return false;
            }

            const int hitSize = 4;
            Vector2 half = new(hitSize * 0.5f);
            Vector2 previous = Position - stepVelocity;
            Vector2 allowed = Collision.TileCollision(previous - half, stepVelocity
                , hitSize, hitSize, fallThrough: false, fall2: false, gravDir: 1);

            bool collided = Vector2.DistanceSquared(allowed, stepVelocity) > 0.01f;
            if (!collided && Collision.SolidCollision(Position - half, hitSize, hitSize)) {
                allowed = Vector2.Zero;
                collided = true;
            }
            if (!collided) {
                return false;
            }

            if (Math.Abs(allowed.X - stepVelocity.X) > 0.05f) {
                normal.X = -Math.Sign(stepVelocity.X);
            }
            if (Math.Abs(allowed.Y - stepVelocity.Y) > 0.05f) {
                normal.Y = -Math.Sign(stepVelocity.Y);
            }
            if (normal == Vector2.Zero) {
                normal = -stepVelocity.SafeNormalize(-Vector2.UnitY);
            }
            normal.Normalize();
            Position = previous + allowed + normal * 0.8f;
            return true;
        }

        private void EnterSurfaceFilm(Vector2 normal, Vector2 impactVelocity) {
            surfaceNormal = normal;
            Vector2 tangent = surfaceNormal.RotatedBy(MathHelper.PiOver2);
            filmSpeed = MathHelper.Clamp(Vector2.Dot(impactVelocity, tangent) * 0.16f, -1.6f, 1.6f);
            filmStartedAt = Time;
            filmLifetime = Main.rand.Next(18, 31);

            if (canSplit && impactVelocity.LengthSquared() > 20f) {
                SpawnSecondarySplash(impactVelocity);
            }

            phase = DropletPhase.SurfaceFilm;
            Velocity = Vector2.Zero;
            Lifetime = Math.Max(Lifetime, Time + filmLifetime);
            Rotation = tangent.ToRotation() + MathHelper.PiOver2;
            Scale = initialScale;
            Opacity = 1f;
        }

        private void SurfaceFilmAI() {
            int held = Time - filmStartedAt;
            float progress = MathHelper.Clamp(held / (float)Math.Max(1, filmLifetime), 0f, 1f);
            Vector2 tangent = surfaceNormal.RotatedBy(MathHelper.PiOver2);

            filmSpeed += Vector2.Dot(Vector2.UnitY * gravity, tangent) * 0.18f;
            filmSpeed *= 0.88f;
            Position += tangent * filmSpeed;

            Opacity = 1f - SmoothStep01((progress - 0.35f) / 0.65f);
            Color = Color.Lerp(baseColor, DeepTint(baseColor), progress * 0.45f);

            bool floorFilm = surfaceNormal.Y < -0.65f;
            if (!floorFilm && held >= 9) {
                ReleaseRunoff(tangent);
            }
        }

        private void ReleaseRunoff(Vector2 tangent) {
            phase = DropletPhase.Flying;
            ignoreCollisionUntil = Time + 4;
            canSplit = false;
            Position += surfaceNormal * 1.5f;
            Velocity = tangent * filmSpeed * 0.45f
                + Vector2.UnitY * Main.rand.NextFloat(0.75f, 1.45f)
                + surfaceNormal * 0.25f;
            initialScale *= 0.68f;
            Scale = initialScale;
            Lifetime = Math.Max(Lifetime, Time + Main.rand.Next(18, 30));
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        private void SpawnSecondarySplash(Vector2 impactVelocity) {
            Vector2 tangent = surfaceNormal.RotatedBy(MathHelper.PiOver2);
            float impact = MathHelper.Clamp(impactVelocity.Length(), 3f, 14f);
            int count = impact > 8f ? 2 : 1;

            for (int i = 0; i < count; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 velocity = surfaceNormal * Main.rand.NextFloat(0.35f, 0.75f) * impact
                    + tangent * side * Main.rand.NextFloat(0.15f, 0.5f) * impact;
                velocity.Y -= Main.rand.NextFloat(0.15f, 0.75f);
                PRTLoader.NewParticle<PRT_OceanCurrentDroplet>(Position + surfaceNormal * 1.5f
                    , velocity, baseColor, initialScale * Main.rand.NextFloat(0.38f, 0.58f))
                    ?.Configure(Main.rand.Next(18, 30), gravity * 1.08f, 0.978f
                        , turbulence * 0.6f, canSplit: false);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            int frameWidth = texture.Width / 3;
            int frameHeight = texture.Height / 3;
            int index = frameIndex % 9;
            Rectangle frame = new(index % 3 * frameWidth, index / 3 * frameHeight, frameWidth, frameHeight);
            Vector2 origin = frame.Size() * 0.5f;
            Vector2 drawPosition = Position - Main.screenPosition;

            if (phase == DropletPhase.SurfaceFilm) {
                float progress = MathHelper.Clamp((Time - filmStartedAt) / (float)Math.Max(1, filmLifetime), 0f, 1f);
                Vector2 filmScale = new Vector2(0.28f, MathHelper.Lerp(0.72f, 1.35f, MathF.Sqrt(progress)))
                    * Scale;
                spriteBatch.Draw(texture, drawPosition, frame, Color * Opacity, Rotation
                    , origin, filmScale, SpriteEffects.None, 0f);
                return false;
            }

            float speed = Velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.075f, 0f, 1.25f);
            Vector2 bodyScale = new Vector2(0.64f - stretch * 0.17f, 0.82f + stretch * 1.35f) * Scale;
            spriteBatch.Draw(texture, drawPosition, frame, Color * Opacity, Rotation
                , origin, bodyScale, SpriteEffects.None, 0f);

            Texture2D highlight = CWRAsset.Extra_98?.Value;
            if (highlight != null && speed > 2f) {
                Color glint = OceanCurrentVFX.WaterBright with { A = 0 };
                Vector2 glintScale = new(0.12f * Scale, MathHelper.Clamp(speed * 0.012f, 0.08f, 0.28f) * Scale);
                spriteBatch.Draw(highlight, drawPosition, null, glint * (Opacity * 0.42f), Rotation
                    , highlight.Size() * 0.5f, glintScale, SpriteEffects.None, 0f);
            }
            return false;
        }

        private static Color DeepTint(Color color) => Color.Lerp(color, OceanCurrentVFX.DeepOcean, 0.62f);

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }

    /// <summary>上浮并在寿命末段鼓胀破裂的海水泡沫</summary>
    internal class PRT_OceanCurrentFoam : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle6";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 500;

        private Color baseColor;
        private float initialScale;
        private float rise;
        private float wobblePhase;
        private float wobbleSpeed;

        public PRT_OceanCurrentFoam Configure(int lifetime, float risePerFrame = 0.04f) {
            Lifetime = lifetime;
            baseColor = Color;
            initialScale = Scale;
            rise = risePerFrame;
            return this;
        }

        public override void Reset() {
            base.Reset();
            baseColor = default;
            initialScale = 0f;
            rise = 0f;
            wobblePhase = 0f;
            wobbleSpeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            wobblePhase = Main.rand.NextFloat(MathHelper.TwoPi);
            wobbleSpeed = Main.rand.NextFloat(0.14f, 0.24f);
        }

        public override void AI() {
            wobblePhase += wobbleSpeed;
            Velocity.X = Velocity.X * 0.95f + MathF.Sin(wobblePhase) * 0.035f;
            Velocity.Y = MathF.Max(Velocity.Y - rise, -1.45f);

            float life = MathHelper.Clamp(LifetimeCompletion, 0f, 1f);
            float pop = SmoothStep01((life - 0.78f) / 0.22f);
            Scale = initialScale * MathHelper.Lerp(0.82f, 1.5f, pop);
            Opacity = MathF.Min(life / 0.1f, 1f) * (1f - pop);
            Color = Color.Lerp(baseColor, OceanCurrentVFX.WaterBright, life * 0.22f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 position = Position - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;
            Color rim = Color with { A = 0 };

            spriteBatch.Draw(texture, position, null, rim * (0.62f * Opacity), 0f
                , origin, Scale, SpriteEffects.None, 0f);
            Vector2 highlightOffset = new(-texture.Width, -texture.Height);
            highlightOffset *= Scale * 0.12f;
            spriteBatch.Draw(texture, position + highlightOffset, null
                , OceanCurrentVFX.OceanFoam with { A = 0 } * (0.5f * Opacity), 0f
                , origin, Scale * 0.22f, SpriteEffects.None, 0f);
            return false;
        }

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }

    /// <summary>短命水压环，承担流动间隙和撞击轮廓，不使用非池化的通用波环。</summary>
    internal class PRT_OceanCurrentWake : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "DiffusionCircle4";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 300;

        private Color baseColor;
        private float initialScale;
        private float finalScale;
        private Vector2 squish;

        public PRT_OceanCurrentWake Configure(Vector2 direction, Vector2 squish, float finalScale, int lifetime) {
            Rotation = direction.ToRotation();
            this.squish = squish;
            this.finalScale = finalScale;
            initialScale = Scale;
            baseColor = Color;
            Lifetime = lifetime;
            return this;
        }

        public override void Reset() {
            base.Reset();
            baseColor = default;
            initialScale = 0f;
            finalScale = 0f;
            squish = default;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            float life = MathHelper.Clamp(LifetimeCompletion, 0f, 1f);
            float eased = 1f - MathF.Pow(1f - life, 2f);
            Scale = MathHelper.Lerp(initialScale, finalScale, eased);
            Velocity *= 0.9f;
            Opacity = (1f - life) * MathF.Min(life * 6f, 1f);
            Color = Color.Lerp(baseColor, OceanCurrentVFX.DeepOcean, life * 0.35f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            spriteBatch.Draw(texture, Position - Main.screenPosition, null, Color * Opacity
                , Rotation, texture.Size() * 0.5f, Scale * squish, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>低频海洋生物剪影；只作为洪流内部的深度点缀</summary>
    internal class PRT_OceanCurrentMarineMote : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 160;

        private bool fish;
        private Color baseColor;
        private float initialScale;
        private float swimPhase;
        private float swimSpeed;

        public PRT_OceanCurrentMarineMote Configure(bool isFish, int lifetime) {
            fish = isFish;
            Lifetime = lifetime;
            baseColor = Color;
            initialScale = Scale;
            return this;
        }

        public override void Reset() {
            base.Reset();
            fish = false;
            baseColor = default;
            initialScale = 0f;
            swimPhase = 0f;
            swimSpeed = 0f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            swimPhase = Main.rand.NextFloat(MathHelper.TwoPi);
            swimSpeed = Main.rand.NextFloat(0.12f, 0.2f);
        }

        public override void AI() {
            swimPhase += swimSpeed;
            Vector2 direction = Velocity.SafeNormalize(Vector2.UnitX);
            Vector2 tangent = direction.RotatedBy(MathHelper.PiOver2);

            if (fish) {
                Velocity *= 0.975f;
                Position += tangent * MathF.Sin(swimPhase) * 0.42f;
                Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            }
            else {
                Velocity *= 0.95f;
                Velocity.Y += 0.025f;
                Rotation = MathF.Sin(swimPhase) * 0.38f;
            }

            float life = MathHelper.Clamp(LifetimeCompletion, 0f, 1f);
            Opacity = MathF.Min(life / 0.12f, 1f) * SmoothStep01((1f - life) / 0.28f) * 0.72f;
            Scale = initialScale * MathHelper.Lerp(1f, 0.76f, life);
            Color = Color.Lerp(baseColor, OceanCurrentVFX.DeepOcean, life * 0.55f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D texture = PRTLoader.PRT_IDToTexture[ID];
            Vector2 direction = Velocity.SafeNormalize(Vector2.UnitX);
            Vector2 position = Position - Main.screenPosition;
            Vector2 origin = texture.Size() * 0.5f;

            if (fish) {
                Vector2 bodyScale = new(0.34f, 0.78f);
                spriteBatch.Draw(texture, position, null, Color * Opacity, Rotation
                    , origin, bodyScale * Scale, SpriteEffects.None, 0f);
                spriteBatch.Draw(texture, position - direction * (texture.Height * Scale * 0.2f), null
                    , OceanCurrentVFX.DeepOcean * (Opacity * 0.75f), Rotation + MathF.Sin(swimPhase) * 0.28f
                    , origin, new Vector2(0.24f, 0.34f) * Scale, SpriteEffects.None, 0f);
            }
            else {
                for (int i = 0; i < 2; i++) {
                    Vector2 offset = Vector2.UnitY * i * texture.Height * Scale * 0.16f;
                    spriteBatch.Draw(texture, position + offset, null, Color * (Opacity * (1f - i * 0.22f))
                        , Rotation + i * 0.16f, origin, new Vector2(0.22f, 0.62f) * Scale
                        , SpriteEffects.None, 0f);
                }
            }
            return false;
        }

        private static float SmoothStep01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
    }
}