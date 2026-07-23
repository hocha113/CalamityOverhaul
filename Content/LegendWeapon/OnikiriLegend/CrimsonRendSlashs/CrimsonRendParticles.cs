using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs
{
    /// <summary>
    /// 命中材质分流,金属弹射钢屑/白热,血肉重力血珠+可贴块血渍<br/>
    /// 挥空刀光呼吸粒子不走此处
    /// </summary>
    internal static class CrimsonRendHitVFX
    {
        public static readonly Color Blood = new(156, 22, 28);
        public static readonly Color BloodDeep = new(96, 12, 18);
        public static readonly Color Arterial = new(188, 32, 40);
        public static readonly Color WoundHot = new(210, 70, 58);

        /// <summary>每拍首次命中爆点,金属火花 vs 血肉四溅</summary>
        public static void SpawnImpactBurst(Vector2 pos, Vector2 aimDir, float power, float sizeMul, bool steel) {
            if (Main.dedServ) {
                return;
            }
            if (steel) {
                SpawnSteelBurst(pos, aimDir, power, sizeMul);
            }
            else {
                SpawnFleshBurst(pos, aimDir, power, sizeMul);
            }
        }

        /// <summary>同拍后续命中轻量跟刀</summary>
        public static void SpawnHitTick(Vector2 pos, Vector2 aimDir, float sizeMul, bool steel) {
            if (Main.dedServ) {
                return;
            }
            if (steel) {
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = aimDir.RotatedByRandom(0.65) * Main.rand.NextFloat(4f, 12f) * sizeMul;
                    PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(pos, vel, new Color(255, 96, 60)
                        , Main.rand.NextFloat(0.4f, 0.8f) * sizeMul)
                        ?.Configure(Main.rand.Next(18, 32), gravity: true, maxBounces: 2);
                }
            }
            else {
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = aimDir.RotatedByRandom(0.75) * Main.rand.NextFloat(4.5f, 11f) * sizeMul;
                    vel.Y -= Main.rand.NextFloat(0.4f, 1.8f);
                    Color c = Main.rand.NextBool(3) ? Arterial : Blood;
                    //跟刀多数可贴,加重落地
                    if (!Main.rand.NextBool(3)) {
                        PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, vel, c
                            , Main.rand.NextFloat(0.95f, 1.55f) * sizeMul)
                            ?.Configure(Main.rand.Next(36, 56), 0.42f, 0.99f, stuckLifetime: Main.rand.Next(36, 56));
                    }
                    else {
                        PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, c
                            , Main.rand.NextFloat(0.95f, 1.55f) * sizeMul)
                            ?.Configure(Main.rand.Next(20, 34), 0.30f);
                    }
                }
                //慢重余韵,全部可贴
                for (int i = 0; i < 2; i++) {
                    Vector2 vel = aimDir.RotatedByRandom(1.1) * Main.rand.NextFloat(1.2f, 3.5f) * sizeMul;
                    PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, vel, BloodDeep
                        , Main.rand.NextFloat(1.1f, 1.7f) * sizeMul)
                        ?.Configure(Main.rand.Next(44, 68), 0.48f, 0.985f, stuckLifetime: Main.rand.Next(42, 64));
                }
            }
        }

        private static void SpawnSteelBurst(Vector2 pos, Vector2 aimDir, float power, float sizeMul) {
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos, Vector2.Zero
                , new Color(255, 225, 205), (0.75f + power * 0.8f) * sizeMul);
            int satellites = 1 + (int)(power * 2f);
            for (int i = 0; i < satellites; i++) {
                Vector2 off = Main.rand.NextVector2Circular(24f, 24f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonHitFlash>(pos + off, off * 0.05f
                    , new Color(255, 140, 110), Main.rand.NextFloat(0.5f, 0.75f) * sizeMul);
            }

            //可弹射钢屑(主视觉)+少量无碰撞飞星垫密度
            int mainSparks = 8 + (int)(power * 14f);
            for (int i = 0; i < mainSparks; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.78) * Main.rand.NextFloat(5f, 12f + power * 10f) * sizeMul;
                Color c = Main.rand.NextBool(3) ? new Color(255, 236, 210) : new Color(255, 92, 58);
                float sc = Main.rand.NextFloat(0.45f, 0.7f + power * 0.4f) * sizeMul;
                int life = Main.rand.Next(20, 34 + (int)(power * 12f));
                if (!Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(pos, vel, c, sc)
                        ?.Configure(life, gravity: true, maxBounces: Main.rand.Next(1, 3));
                }
                else {
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, c, sc)
                        ?.Configure(life, affectedByGravity: true);
                }
            }
            int backSparks = 2 + (int)(power * 5f);
            for (int i = 0; i < backSparks; i++) {
                Vector2 vel = (-aimDir).RotatedByRandom(1.1) * Main.rand.NextFloat(3f, 8f) * sizeMul;
                //背向轻屑也可弹一次
                if (Main.rand.NextBool()) {
                    PRTLoader.NewParticle<PRT_CrimsonSteelSpark>(pos, vel, new Color(255, 70, 46)
                        , Main.rand.NextFloat(0.35f, 0.6f) * sizeMul)
                        ?.Configure(Main.rand.Next(16, 26), gravity: true, maxBounces: 1);
                }
                else {
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 70, 46)
                        , Main.rand.NextFloat(0.35f, 0.6f) * sizeMul)
                        ?.Configure(Main.rand.Next(16, 26), affectedByGravity: false);
                }
            }
        }

        private static void SpawnFleshBurst(Vector2 pos, Vector2 aimDir, float power, float sizeMul) {
            //伤口暗红雾(体积垫底,不发光)
            for (int i = 0; i < 2 + (int)(power * 2f); i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.6f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos + Main.rand.NextVector2Circular(8f, 6f) * sizeMul
                    , vel, Color.White, Main.rand.NextFloat(0.08f, 0.14f) * sizeMul)
                    ?.Configure(Main.rand.Next(22, 36), Blood, BloodDeep, 0.01f);
            }

            //动脉喷溅;约 2/3 可贴块,加重+延寿方便落地
            int mainDrops = 10 + (int)(power * 16f);
            for (int i = 0; i < mainDrops; i++) {
                Vector2 vel = aimDir.RotatedByRandom(0.82) * Main.rand.NextFloat(6f, 13f + power * 10f) * sizeMul;
                vel.Y -= Main.rand.NextFloat(0.8f, 2.8f);
                Color c = Main.rand.NextBool(4) ? Arterial : (Main.rand.NextBool() ? Blood : WoundHot);
                float sc = Main.rand.NextFloat(1.0f, 1.75f + power * 0.35f) * sizeMul;
                if (!Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, vel, c, sc)
                        ?.Configure(Main.rand.Next(40, 62 + (int)(power * 12f)), 0.42f, 0.99f
                            , stuckLifetime: Main.rand.Next(38, 58));
                }
                else {
                    int life = Main.rand.Next(22, 36 + (int)(power * 10f));
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, c, sc)
                        ?.Configure(life, 0.30f);
                }
            }

            //慢重血珠,全部可贴(主血渍贡献)
            int slowDrops = 3 + (int)(power * 5f);
            for (int i = 0; i < slowDrops; i++) {
                Vector2 vel = aimDir.RotatedByRandom(1.15) * Main.rand.NextFloat(1.4f, 4.2f) * sizeMul;
                PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, vel, BloodDeep
                    , Main.rand.NextFloat(1.2f, 1.9f) * sizeMul)
                    ?.Configure(Main.rand.Next(48, 72), 0.50f, 0.985f, stuckLifetime: Main.rand.Next(44, 68));
            }

            //背向溅出,多数可贴
            int backDrops = 2 + (int)(power * 4f);
            for (int i = 0; i < backDrops; i++) {
                Vector2 vel = (-aimDir).RotatedByRandom(1.0) * Main.rand.NextFloat(2.5f, 7f) * sizeMul;
                vel.Y -= Main.rand.NextFloat(0.3f, 1.5f);
                float sc = Main.rand.NextFloat(0.9f, 1.4f) * sizeMul;
                if (!Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_CrimsonBloodStain>(pos, vel, Blood, sc)
                        ?.Configure(Main.rand.Next(36, 56), 0.40f, 0.99f, stuckLifetime: Main.rand.Next(36, 54));
                }
                else {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos, vel, Blood, sc)
                        ?.Configure(Main.rand.Next(20, 34), 0.28f);
                }
            }
        }
    }

    /// <summary>
    /// 绯红血珠:飞行同刻心者液滴,触实心块压扁贴附,短暂下垂后淡出<br/>
    /// 仅 Crimson 血肉命中使用,不改全局 PRT_HeartcarverDroplet
    /// </summary>
    internal class PRT_CrimsonBloodStain : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 140;

        private enum Phase : byte { Flying, Stuck }

        private Phase phase;
        private Color initialColor;
        private float gravity;
        private float drag;
        private int stickLife;
        private int stuckAt;
        private Vector2 stuckNormal;
        private float impactSpeed;
        private float splatMul;
        private float sag;
        private float stuckScale;

        public PRT_CrimsonBloodStain Configure(int flyLifetime, float gravityPerFrame = 0.32f
            , float dragMul = 0.985f, int stuckLifetime = 48) {
            Lifetime = flyLifetime;
            initialColor = Color;
            gravity = gravityPerFrame;
            drag = dragMul;
            stickLife = Math.Max(18, stuckLifetime);
            return this;
        }

        public override void Reset() {
            base.Reset();
            phase = Phase.Flying;
            initialColor = default;
            gravity = 0f;
            drag = 1f;
            stickLife = 0;
            stuckAt = 0;
            stuckNormal = default;
            impactSpeed = 0f;
            splatMul = 1f;
            sag = 0f;
            stuckScale = 1f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            Opacity = 1f;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(26, 40);
            }
            if (gravity <= 0f) {
                gravity = 0.32f;
            }
            if (drag <= 0f || drag > 1f) {
                drag = 0.985f;
            }
            if (stickLife <= 0) {
                stickLife = Main.rand.Next(40, 58);
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override bool ShouldUpdatePosition() => phase == Phase.Flying;

        public override void AI() {
            if (phase == Phase.Stuck) {
                StuckAI();
                return;
            }

            Velocity.X *= drag;
            Velocity.Y += gravity;
            if (Velocity.Y > 14f) {
                Velocity.Y = 14f;
            }

            //空中轻淡,给撞墙留色量
            float flyT = LifetimeCompletion;
            Scale *= 0.988f;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(flyT, 3.2f) * 0.4f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;

            TryEnterStuck();
        }

        private void TryEnterStuck() {
            const int hitW = 2;
            const int hitH = 2;
            Vector2 half = new(hitW * 0.5f, hitH * 0.5f);
            //下落才认平台/桌面等 SolidTop:可上穿、落下滞留
            bool landTops = Velocity.Y > 0.15f;

            Vector2 prev = Position - Velocity;
            bool inSolid = CrimsonHitSurface.Hits(Position - half, hitW, hitH, landTops);

            //前瞻半步:高速时提前咬住表面
            if (!inSolid) {
                Vector2 ahead = Position + Velocity * 0.55f;
                bool aheadLand = Velocity.Y + Velocity.Y * 0.55f > 0.15f || landTops;
                if (CrimsonHitSurface.Hits(ahead - half, hitW, hitH, aheadLand)) {
                    Position = ahead;
                    inSolid = true;
                    landTops = aheadLand;
                }
            }

            //下落近距吸附:下方 12px 内有实心/平台则拉贴
            Vector2 snapNormal = -Vector2.UnitY;
            if (!inSolid && Velocity.Y > 0.5f) {
                for (float d = 1f; d <= 12f; d += 1f) {
                    Vector2 probe = Position + new Vector2(0f, d);
                    if (CrimsonHitSurface.Hits(probe - half, hitW, hitH, allowPlatforms: true)) {
                        Position = probe;
                        inSolid = true;
                        landTops = true;
                        snapNormal = -Vector2.UnitY;
                        break;
                    }
                }
            }

            //侧向近距吸附(仅全实心墙,平台不可侧贴)
            if (!inSolid && Math.Abs(Velocity.X) > 0.6f) {
                float side = Math.Sign(Velocity.X);
                for (float d = 1f; d <= 8f; d += 1f) {
                    Vector2 probe = Position + new Vector2(side * d, 0f);
                    if (CrimsonHitSurface.Hits(probe - half, hitW, hitH, allowPlatforms: false)) {
                        Position = probe;
                        inSolid = true;
                        landTops = false;
                        snapNormal = new Vector2(-side, 0f);
                        break;
                    }
                }
            }

            if (!inSolid) {
                return;
            }

            Vector2 n = Vector2.Zero;
            if (CrimsonHitSurface.Hits(prev + new Vector2(Velocity.X, 0f) - half, hitW, hitH, allowPlatforms: false)) {
                n.X = -Math.Sign(Velocity.X == 0f ? snapNormal.X : Velocity.X);
            }
            if (CrimsonHitSurface.Hits(prev + new Vector2(0f, Velocity.Y) - half, hitW, hitH, landTops)) {
                n.Y = -Math.Sign(Velocity.Y == 0f ? 1f : Velocity.Y);
            }
            if (n == Vector2.Zero) {
                n = snapNormal;
            }

            stuckNormal = n.SafeNormalize(-Vector2.UnitY);
            impactSpeed = MathF.Max(Velocity.Length(), 1f);

            //从实体内部沿外法线推出到刚好离开,贴死表面(勿回退到空中 prev)
            for (int i = 0; i < 32 && CrimsonHitSurface.Hits(Position - half, hitW, hitH, landTops); i++) {
                Position += stuckNormal * 0.5f;
            }
            //若已在空气中(近距吸附过头),沿 -normal 拉回贴面
            if (!CrimsonHitSurface.Hits(Position - half, hitW, hitH, landTops)) {
                for (int i = 0; i < 16; i++) {
                    Vector2 next = Position - stuckNormal * 0.5f;
                    if (CrimsonHitSurface.Hits(next - half, hitW, hitH, landTops)) {
                        break;
                    }
                    Position = next;
                }
                Position += stuckNormal * 0.35f;
            }

            phase = Phase.Stuck;
            Velocity = Vector2.Zero;
            stuckAt = Time;
            Lifetime = Time + stickLife;
            stuckScale = Scale * 0.72f;
            splatMul = MathHelper.Clamp(0.55f + impactSpeed * 0.045f, 0.55f, 1.15f)
                * Main.rand.NextFloat(0.85f, 1.05f);
            Rotation = stuckNormal.ToRotation() + MathHelper.PiOver2;
            Color = initialColor;
            Opacity = 1f;

            if (impactSpeed > 5.5f && Main.rand.NextBool(2)) {
                SpawnImpactSplash();
            }
        }

        private void StuckAI() {
            float held = Time - stuckAt;
            float stuckT = MathHelper.Clamp(held / stickLife, 0f, 1f);
            sag = MathHelper.Clamp(held / 36f, 0f, 0.55f);

            //贴附后轻微铺开
            float spread = 1f + MathF.Sin(MathHelper.Clamp(stuckT * 2.6f, 0f, 1f) * MathHelper.PiOver2) * 0.10f;
            Scale = stuckScale * spread;

            //更快淡出
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(stuckT, 1.15f));
            Opacity = 1f - SmoothStep01((stuckT - 0.28f) / 0.55f);

            //底缘偶发垂滴(寿命短,少滴一次)
            int heldFrames = Time - stuckAt;
            if (heldFrames > 12 && heldFrames % 22 == 0 && Main.rand.NextBool(4) && sag > 0.18f) {
                SpawnDrip();
            }
        }

        private void SpawnImpactSplash() {
            Vector2 tangent = stuckNormal.RotatedBy(MathHelper.PiOver2);
            float force = MathHelper.Clamp(impactSpeed * 0.28f, 1.0f, 3.8f);
            int n = impactSpeed > 10f ? 3 : 2;
            for (int i = 0; i < n; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 v = tangent * side * force * Main.rand.NextFloat(0.35f, 1f)
                    + stuckNormal * force * Main.rand.NextFloat(0.15f, 0.55f);
                v.Y -= Main.rand.NextFloat(0.2f, 0.8f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Position + tangent * Main.rand.NextFloat(-5f, 5f), v
                    , initialColor, Scale * Main.rand.NextFloat(0.45f, 0.75f))
                    ?.Configure(Main.rand.Next(14, 24), 0.28f, 0.985f);
            }
        }

        private void SpawnDrip() {
            Vector2 tangent = stuckNormal.RotatedBy(MathHelper.PiOver2);
            //重力向垂滴:墙面从下缘,地面从中心略偏
            Vector2 dripPos = Position
                + tangent * Main.rand.NextFloat(-5f, 5f)
                + Vector2.UnitY * (2f + sag * 4f)
                - stuckNormal * 1.5f;
            PRTLoader.NewParticle<PRT_HeartcarverDroplet>(dripPos
                , new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(0.35f, 0.85f))
                , initialColor, Scale * Main.rand.NextFloat(0.35f, 0.55f))
                ?.Configure(Main.rand.Next(18, 30), 0.20f, 0.992f);
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            Color light = Lighting.GetColor(Position.ToTileCoordinates());
            Color draw = Color.MultiplyRGB(light) * Opacity;

            if (phase == Phase.Stuck) {
                //更小血渍:切向铺开、法向压扁
                float wide = (0.32f + sag * 0.12f) * splatMul;
                float thin = (0.12f - sag * 0.02f) * splatMul;
                Vector2 body = new Vector2(wide, thin + sag * 0.06f) * Scale;
                Vector2 core = body * new Vector2(0.5f, 0.8f);
                spriteBatch.Draw(tex, pos, null, draw, Rotation, origin, body, SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, pos + stuckNormal.RotatedBy(MathHelper.PiOver2) * (0.8f * sag)
                    , null, draw * 0.7f, Rotation + 0.14f, origin
                    , body * new Vector2(0.58f, 0.85f), SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, pos - stuckNormal * (0.25f + sag * 0.2f), null, draw * 0.5f
                    , Rotation, origin, core, SpriteEffects.None, 0f);
                return false;
            }

            float stretch = MathHelper.Clamp(Velocity.Length() * 0.045f, 0f, 0.85f);
            Vector2 scale = new Vector2(0.34f * (1f - stretch * 0.35f), 0.62f * (1f + stretch * 1.7f)) * Scale;
            spriteBatch.Draw(tex, pos, null, draw, Rotation, origin, scale, SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, draw, Rotation, origin, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>命中粒子共用表面判定:全实心始终挡;SolidTop 仅下落时挡</summary>
    internal static class CrimsonHitSurface
    {
        public static bool Hits(Vector2 topLeft, int width, int height, bool allowPlatforms) {
            return allowPlatforms
                ? Collision.SolidCollision(topLeft, width, height, acceptTopSurfaces: true)
                : Collision.SolidCollision(topLeft, width, height);
        }
    }

    /// <summary>
    /// 金属命中钢屑:加色拉长火花,触块弹射/刮擦(对比血珠贴附)<br/>
    /// 平台仅下落时碰撞;刀光呼吸仍用无碰撞 PRT_CrimsonSpark
    /// </summary>
    internal class PRT_CrimsonSteelSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarGlow01";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 220;

        private Color initialColor;
        private bool useGravity;
        private int maxBounces;
        private int bounceCount;
        private int lastBounceTime;
        private float restitution;

        public PRT_CrimsonSteelSpark Configure(int lifetime, bool gravity = true, int maxBounces = 2
            , float restitution = 0.58f) {
            Lifetime = lifetime;
            initialColor = Color;
            useGravity = gravity;
            this.maxBounces = Math.Clamp(maxBounces, 0, 3);
            this.restitution = MathHelper.Clamp(restitution, 0.25f, 0.85f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            useGravity = false;
            maxBounces = 0;
            bounceCount = 0;
            lastBounceTime = -10;
            restitution = 0.58f;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            Opacity = 1f;
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(20, 34);
            }
            if (initialColor == default) {
                initialColor = Color;
            }
            if (maxBounces <= 0 && bounceCount == 0) {
                maxBounces = 2;
            }
        }

        public override void AI() {
            Scale *= 0.972f;
            Velocity *= 0.975f;
            if (useGravity && Velocity.Y < 16f) {
                Velocity.Y += 0.28f;
            }

            TryRicochet();

            Rotation = Velocity.LengthSquared() > 0.04f
                ? Velocity.ToRotation() + MathHelper.PiOver2
                : Rotation;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(LifetimeCompletion, 2.1f));
            Opacity = MathHelper.Clamp(1f - LifetimeCompletion * 0.15f, 0.55f, 1f)
                * MathHelper.Clamp((1f - LifetimeCompletion) * 3.5f, 0f, 1f);

            if (Scale < 0.035f) {
                active = false;
            }
        }

        private void TryRicochet() {
            if (Time - lastBounceTime < 2) {
                return;
            }
            if (Velocity.LengthSquared() < 0.5f) {
                return;
            }

            const int hitW = 2;
            const int hitH = 2;
            Vector2 half = new(hitW * 0.5f, hitH * 0.5f);
            bool landTops = Velocity.Y > 0.15f;

            bool hit = CrimsonHitSurface.Hits(Position - half, hitW, hitH, landTops);
            if (!hit) {
                Vector2 ahead = Position + Velocity * 0.45f;
                if (CrimsonHitSurface.Hits(ahead - half, hitW, hitH, Velocity.Y > 0.15f)) {
                    Position = ahead;
                    hit = true;
                    landTops = Velocity.Y > 0.15f;
                }
            }
            if (!hit && Velocity.Y > 0.6f) {
                for (float d = 1f; d <= 8f; d += 1f) {
                    Vector2 probe = Position + new Vector2(0f, d);
                    if (CrimsonHitSurface.Hits(probe - half, hitW, hitH, allowPlatforms: true)) {
                        Position = probe;
                        hit = true;
                        landTops = true;
                        break;
                    }
                }
            }
            if (!hit) {
                return;
            }

            Vector2 prev = Position - Velocity;
            Vector2 n = Vector2.Zero;
            if (CrimsonHitSurface.Hits(prev + new Vector2(Velocity.X, 0f) - half, hitW, hitH, allowPlatforms: false)) {
                n.X = -Math.Sign(Velocity.X);
            }
            if (CrimsonHitSurface.Hits(prev + new Vector2(0f, Velocity.Y) - half, hitW, hitH, landTops)) {
                n.Y = -Math.Sign(Velocity.Y == 0f ? 1f : Velocity.Y);
            }
            if (n == Vector2.Zero) {
                n = landTops ? -Vector2.UnitY : new Vector2(-Math.Sign(Velocity.X == 0f ? 1f : Velocity.X), 0f);
            }
            Vector2 normal = n.SafeNormalize(-Vector2.UnitY);

            //推出实体再反射
            for (int i = 0; i < 24 && CrimsonHitSurface.Hits(Position - half, hitW, hitH, landTops); i++) {
                Position += normal * 0.5f;
            }

            Vector2 oldVel = Velocity;
            float impact = oldVel.Length();
            lastBounceTime = Time;
            bounceCount++;

            //能量不足或弹尽:刮擦熄灭(不贴附)
            if (bounceCount > maxBounces || impact < 2.8f) {
                Velocity = Vector2.Reflect(oldVel, normal) * 0.18f;
                Velocity += normal * Main.rand.NextFloat(0.4f, 1.1f);
                Scale *= 0.7f;
                if (Lifetime - Time > 7) {
                    Time = Lifetime - 7;
                }
                SpawnScrapeChips(normal, impact * 0.35f, 1);
                return;
            }

            Velocity = Vector2.Reflect(oldVel, normal) * restitution;
            Velocity = Velocity.RotatedByRandom(0.22f);
            //略抬离表面防贴帧再撞
            Position += normal * 1.2f;
            Scale *= 0.92f;
            SpawnScrapeChips(normal, impact * 0.55f, impact > 8f ? 2 : 1);
        }

        private void SpawnScrapeChips(Vector2 normal, float force, int count) {
            Vector2 tangent = normal.RotatedBy(MathHelper.PiOver2);
            for (int i = 0; i < count; i++) {
                float side = Main.rand.NextBool() ? 1f : -1f;
                Vector2 v = tangent * side * force * Main.rand.NextFloat(0.25f, 0.7f)
                    + normal * force * Main.rand.NextFloat(0.15f, 0.45f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(Position, v
                    , Color.Lerp(initialColor, new Color(255, 240, 210), 0.45f)
                    , Scale * Main.rand.NextFloat(0.35f, 0.55f))
                    ?.Configure(Main.rand.Next(8, 14), affectedByGravity: false);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            Color draw = Color * Opacity;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.18f, 0.85f, 3.0f);
            Vector2 scale = new Vector2(0.38f, stretch) * Scale;
            //热芯 + 拉长条
            spriteBatch.Draw(tex, pos, null, draw, Rotation, origin, scale, SpriteEffects.None, 0);
            spriteBatch.Draw(tex, pos, null, draw * 0.85f, Rotation, origin
                , scale * new Vector2(0.42f, 1f), SpriteEffects.None, 0);
            if (bounceCount > 0 && Time - lastBounceTime < 4) {
                float flash = 1f - (Time - lastBounceTime) / 4f;
                spriteBatch.Draw(tex, pos, null, new Color(255, 245, 220) * (flash * 0.7f * Opacity)
                    , Rotation, origin, scale * (1.15f + flash * 0.25f), SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>刀光燃尽烟,暗红→焦黑 AlphaBlend,外漂放大消散</summary>
    internal class PRT_CrimsonSmoke : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "SmokeSheet01";
        public override bool CanPool => true;

        private float spin;
        private Color hotColor;
        private Color coldColor;

        public PRT_CrimsonSmoke Configure(int lifetime, Color hot, Color cold, float rotSpeed = 0.012f) {
            Lifetime = lifetime;
            hotColor = hot;
            coldColor = cold;
            spin = rotSpeed * (Main.rand.NextBool() ? 1f : -1f);
            return this;
        }

        public override void Reset() {
            base.Reset();
            spin = 0f;
            hotColor = coldColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            ai[0] = Main.rand.Next(4);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            if (Lifetime <= 0) {
                Lifetime = Main.rand.Next(34, 50);
                hotColor = new Color(120, 24, 30);
                coldColor = new Color(30, 14, 24);
            }
        }

        public override void AI() {
            float t = LifetimeCompletion;
            Scale *= 1.008f;
            Rotation += spin;
            Velocity *= 0.94f;
            Velocity.Y -= 0.012f;   //微上浮

            Color = Color.Lerp(hotColor, coldColor, MathF.Min(1f, t * 1.3f));
            //快进快出,峰值压低防烟层吞刀光;末段提前收尾避免白天灰剪影
            Opacity = MathF.Min(t / 0.12f, 1f) * (1f - SmoothStep01((t - 0.42f) / 0.50f)) * 0.42f;
        }

        private static float SmoothStep01(float x) {
            x = MathHelper.Clamp(x, 0f, 1f);
            return x * x * (3f - 2f * x);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int index = (int)ai[0];
            int frameSize = tex.Width / 2;
            Rectangle frame = new(index % 2 * frameSize, index / 2 * frameSize, frameSize, frameSize);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, Scale * 0.5f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>冲击火花,加色四芒星拉长条,抛物+末段重力</summary>
    internal class PRT_CrimsonSpark : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "StarGlow01";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 4000;

        private Color initialColor;
        private bool gravity;

        public PRT_CrimsonSpark Configure(int lifetime, bool affectedByGravity) {
            Lifetime = lifetime;
            initialColor = Color;
            gravity = affectedByGravity;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
            gravity = false;
        }

        public override void SetProperty() => PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;

        public override void AI() {
            Scale *= 0.955f;
            Velocity *= 0.94f;
            if (gravity && Velocity.Length() < 11f) {
                Velocity.X *= 0.96f;
                Velocity.Y += 0.30f;
            }
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
            Color = Color.Lerp(initialColor, Color.Transparent, MathF.Pow(LifetimeCompletion, 2.4f));
            if (Scale < 0.04f) {
                active = false;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            //沿速度拉长成火花条,窄条提亮芯部
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.16f, 0.9f, 2.6f);
            Vector2 scale = new Vector2(0.42f, stretch) * Scale;
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color, Rotation
                , tex.Size() * 0.5f, scale, SpriteEffects.None, 0);
            spriteBatch.Draw(tex, Position - Main.screenPosition, null, Color * 0.8f, Rotation
                , tex.Size() * 0.5f, scale * new Vector2(0.45f, 1f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>命中火花序列帧,2×2 图集单次播放,加色</summary>
    internal class PRT_CrimsonHitFlash : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "HitSparkSheet01";
        public override bool CanPool => true;

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AdditiveBlend;
            if (Lifetime <= 0) {
                Lifetime = 14;
            }
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            Velocity *= 0.9f;
            Opacity = 1f - MathF.Pow(LifetimeCompletion, 3f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            int frameIdx = (int)MathHelper.Clamp(LifetimeCompletion * 4f, 0f, 3f);
            Rectangle frame = new(frameIdx % 2 * 128, frameIdx / 2 * 128, 128, 128);
            spriteBatch.Draw(tex, Position - Main.screenPosition, frame, Color * Opacity, Rotation
                , frame.Size() * 0.5f, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
