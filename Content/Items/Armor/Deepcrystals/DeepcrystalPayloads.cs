using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Kinematics;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Armor.Deepcrystals
{
    /// <summary>
    /// 射手引爆:玩家位置锚定的高压水柱,定角贯穿。画法复用海虾 SeaShrimpJet 的 TechJet,
    /// 宽度生命周期展开→满宽→塌缩,展开 ≥60% 才开伤害窗,判定芯 0.62× 可见宽。
    /// ai[0]=柱向角,ai[1]=湿身旗标(加宽)
    /// </summary>
    internal class DeepcrystalJetBeam : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int ExpandFrames = 8;
        private const int FireFrames = 24;
        private const int CollapseFrames = 8;
        private const int TotalLife = ExpandFrames + FireFrames + CollapseFrames;
        private const float MaxLength = 900f;
        private const float BaseWidth = 66f;
        private const float CoreFrac = 0.62f;

        private float Angle => Projectile.ai[0];
        private bool Wet => Projectile.ai[1] > 0.5f;
        private float JetWidth => BaseWidth * (Wet ? 1.15f : 1f);
        private int Age => (int)Projectile.localAI[0];

        private float Width01 {
            get {
                int age = Age;
                if (age < ExpandFrames) {
                    float t = age / (float)ExpandFrames;
                    return 1f - (1f - t) * (1f - t);
                }
                int fromEnd = TotalLife - age;
                if (fromEnd < CollapseFrames) {
                    float t = fromEnd / (float)CollapseFrames;
                    return t * t;
                }
                return 1f;
            }
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1100;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = TotalLife + 4;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool ShouldUpdatePosition() => false;

        private float BeamLength(Vector2 dir, out bool hit, out Vector2 hitPoint) {
            hit = ShrimpTerrain.RaycastSurface(Projectile.Center, dir, MaxLength, out hitPoint);
            return Vector2.Distance(Projectile.Center, hitPoint);
        }

        public override void AI() {
            Projectile.localAI[0]++;
            if (Age >= TotalLife) {
                Projectile.Kill();
                return;
            }
            Vector2 dir = Angle.ToRotationVector2();
            float len = BeamLength(dir, out bool hit, out Vector2 hitPoint);
            float w01 = Width01;
            Lighting.AddLight(Projectile.Center + dir * len * 0.5f, 0.12f * w01, 0.26f * w01, 0.46f * w01);

            if (Age == 2) {
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.65f, Pitch = 0.1f }, Projectile.Center);
            }
            if (Main.dedServ || w01 < 0.25f) {
                return;
            }
            //口部回溅
            if (Main.GameUpdateCount % 3 == 0) {
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center + dir * 14f,
                    -dir * Main.rand.NextFloat(1.5f, 3.5f) + Main.rand.NextVector2Circular(1.2f, 1.2f),
                    Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(12, 1.6f);
            }
            //落点冲刷
            if (hit && Main.GameUpdateCount % 4 == 0) {
                EverdeepVFX.SplashBurst(hitPoint, dir * 9f, 0.85f);
            }
            //沿柱飞沫
            if (Main.GameUpdateCount % 4 == 0) {
                float at = Main.rand.NextFloat(0.2f, 0.9f);
                EverdeepVFX.ShedDroplet(Projectile.Center + dir * (len * at)
                    + dir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-JetWidth * 0.5f, JetWidth * 0.5f),
                    dir * 3f + Main.rand.NextVector2Circular(1f, 1f), 0.8f);
            }
        }

        public override bool? CanDamage() => Width01 >= 0.6f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = Angle.ToRotationVector2();
            float len = BeamLength(dir, out _, out _);
            float coreWidth = JetWidth * CoreFrac * Width01;
            float _ignore = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                Projectile.Center, Projectile.Center + dir * len, coreWidth, ref _ignore);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //断流:柱身化滴
            Vector2 dir = Angle.ToRotationVector2();
            for (int i = 0; i < 8; i++) {
                EverdeepVFX.ShedDroplet(Projectile.Center + dir * (MaxLength * 0.35f * (i / 8f)),
                    dir * Main.rand.NextFloat(2f, 5f) + new Vector2(0f, Main.rand.NextFloat(0.5f, 1.5f)), 0.9f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float w01 = Width01;
            if (w01 <= 0.02f) {
                return false;
            }
            Vector2 dir = Angle.ToRotationVector2();
            float len = BeamLength(dir, out bool hit, out _);

            float quadLen = len + (hit ? 44f : 130f);
            float sag = 10f * MathHelper.Clamp(len / MaxLength, 0f, 1f);
            float sagLocal = sag * (MathF.Cos(Angle) >= 0f ? 1f : -1f);
            float quadH = JetWidth * 2.6f + MathF.Abs(sagLocal) * 2f;

            Effect fx = EffectLoader.SeaShrimpJet?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (pixel == null) {
                return false;
            }
            if (fx == null || noise == null) {
                DrawFallback(dir, len, w01);
                return false;
            }

            fx.CurrentTechnique = fx.Techniques["TechJet"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.47f);
            fx.Parameters["fadeAlpha"]?.SetValue(1f);
            fx.Parameters["uQuadLenPx"]?.SetValue(quadLen);
            fx.Parameters["uQuadHPx"]?.SetValue(quadH);
            fx.Parameters["uLenPx"]?.SetValue(len);
            fx.Parameters["uWidthPx"]?.SetValue(JetWidth * w01);
            fx.Parameters["uSagPx"]?.SetValue(sagLocal);
            fx.Parameters["uImpact"]?.SetValue(hit ? 1f : 0f);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();

            Vector2 origin = new(0f, 0.5f);
            Rectangle src = new(0, 0, 1, 1);
            sb.Draw(pixel, Projectile.Center - dir * 8f - Main.screenPosition, src, Color.White,
                Angle, origin, new Vector2(quadLen, quadH), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺失回退:分段暗鞘+亮芯,两端正弦包络收口</summary>
        private void DrawFallback(Vector2 dir, float len, float w01) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return;
            }
            Rectangle src = new(0, 0, 1, 1);
            const int Segs = 12;
            for (int i = 0; i < Segs; i++) {
                float t0 = i / (float)Segs;
                float segLen = len / Segs;
                float endEnv = MathF.Sin(MathF.Min(t0 * 3f, MathF.Min((1f - t0) * 3f, 1f)) * MathHelper.PiOver2);
                Vector2 pos = Projectile.Center + dir * (len * t0) - Main.screenPosition;
                float w = JetWidth * w01 * endEnv;
                Main.spriteBatch.Draw(pixel, pos, src, SeaShrimpVFX.Deep * (0.75f * endEnv), Angle,
                    new Vector2(0f, 0.5f), new Vector2(segLen + 1f, w), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(pixel, pos, src, SeaShrimpVFX.Glow * (0.5f * endEnv), Angle,
                    new Vector2(0f, 0.5f), new Vector2(segLen + 1f, w * 0.34f), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 法师引爆:制导水球。前段缓转向锁定点,后段重力下坠成弧;
    /// 画法整段搬海虾压缩水弹(暗流管拖尾 TechCurrent + 递缩鬼影 + 原版水矢本体)。
    /// ai[0]/ai[1]=锁定点坐标
    /// </summary>
    internal class DeepcrystalWaterShot : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.WaterBolt}";

        private const int TrailLen = 10;
        private const int SteerFrames = 30;

        private Vector2 AimPoint => new(Projectile.ai[0], Projectile.ai[1]);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLen;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 180;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            Projectile.rotation = Projectile.velocity.ToRotation();
            float age = Projectile.localAI[0];

            if (age < SteerFrames) {
                //制导段:保速缓转向锁定点 + 轻微鱼摆尾
                float speed = Projectile.velocity.Length();
                Vector2 want = (AimPoint - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Vector2 cur = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 dir = Vector2.Lerp(cur, want, 0.09f).SafeNormalize(Vector2.UnitX);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                Projectile.velocity = dir * speed
                    + perp * (MathF.Sin(age * 0.4f + Projectile.identity) * 0.35f);
            }
            else {
                //坠落段:重力成弧
                Projectile.velocity.Y += 0.2f;
                if (Projectile.velocity.Y > 15f) {
                    Projectile.velocity.Y = 15f;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.06f, 0.16f, 0.32f);

            if (!Main.dedServ) {
                if (Main.GameUpdateCount % 2 == 0) {
                    PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.Center,
                        -Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                        Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                        Main.rand.NextFloat(0.2f, 0.34f))?.Configure(10, 1.5f);
                }
                if (Main.rand.NextBool(9)) {
                    EverdeepVFX.ShedDroplet(Projectile.Center,
                        -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.6f, 0.6f), 0.8f);
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 180);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            EverdeepVFX.SplashBurst(Projectile.Center, Projectile.velocity, 1f);
            for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                PRTLoader.NewParticle<PRT_AbyssGlob>(Projectile.oldPos[i] + Projectile.Size * 0.5f,
                    Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(Main.rand.Next(10, 18), 1.2f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center,
                    Main.rand.NextVector2Circular(3f, 3f) - Projectile.velocity * 0.1f,
                    SeaShrimpVFX.Glow, Main.rand.NextFloat(0.6f, 0.9f))?.Configure(10);
            }
        }

        void IPrimitiveDrawable.DrawPrimitives() {
            Vector2[] path = new Vector2[TrailLen];
            int count = 0;
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                path[count++] = Projectile.oldPos[i] + Projectile.Size * 0.5f;
            }
            if (count < 2) {
                return;
            }
            path[count - 1] = Projectile.Center;
            float lifeFade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0.15f, 1f);
            AbyssrendFX.DrawPathStrip(path, count, i => {
                float t = i / (float)Math.Max(count - 1, 1);
                return MathHelper.Lerp(4.5f, 10f, t) * lifeFade;
            }, lifeFade);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                float t = i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Color col = Color.Lerp(SeaShrimpVFX.Body, SeaShrimpVFX.Glow, 1f - t) * (0.4f * (1f - t));
                Main.spriteBatch.Draw(tex, pos, null, col, Projectile.oldRot[i],
                    origin, MathHelper.Lerp(0.9f, 0.5f, t), SpriteEffects.None, 0f);
            }

            Vector2 center = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, center, null, lightColor, Projectile.rotation,
                origin, 1f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, center, null,
                new Color(200, 235, 255, 90) * 0.7f, Projectile.rotation,
                origin, 0.62f, SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>
    /// 召唤引爆:追踪气泡。缓加速索敌,触敌破膜 8 帧(破膜期无伤害)并附渊压;
    /// 泡体接入海虾水膜批绘(<see cref="SeaShrimpBubbleRender"/>)。ai[0]=湿身旗标(加大)
    /// </summary>
    internal class DeepcrystalSeekBubble : ModProjectile, ISeaShrimpBubbleBody
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int BurstFrames = 8;
        private const int InflateFrames = 8;
        private const float SeekRange = 700f;

        private bool Wet => Projectile.ai[0] > 0.5f;
        private float Radius => Wet ? 16f : 13f;
        private int Age => (int)Projectile.localAI[0];
        private int BurstAge => (int)Projectile.localAI[1];
        private bool Bursting => Projectile.localAI[1] > 0;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 300;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            SeaShrimpBubbleRender.PresenceStamp.Stamp();

            if (Bursting) {
                Projectile.velocity *= 0.3f;
                Projectile.localAI[1]++;
                if (BurstAge > BurstFrames) {
                    Projectile.Kill();
                }
                return;
            }

            //索敌:缓加速追踪 + 水流摆
            NPC target = Projectile.Center.FindClosestNPC(SeekRange);
            float speed = MathHelper.Clamp(2.5f + Age * 0.1f, 2.5f, 7.5f);
            if (target != null) {
                Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * speed;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.08f);
            }
            else {
                Projectile.velocity *= 0.97f;
            }
            Vector2 perp = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Projectile.velocity += perp
                * (MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + Projectile.identity * 0.9f) * 0.12f);
            Lighting.AddLight(Projectile.Center, 0.05f, 0.12f, 0.24f);

            if (!Main.dedServ && Main.rand.NextBool(12)) {
                EverdeepVFX.ShedDroplet(Projectile.Center, -Projectile.velocity * 0.1f, 0.6f);
            }
            //寿尽先破膜
            if (Projectile.timeLeft <= BurstFrames + 2) {
                StartBurst();
            }
        }

        private void StartBurst() {
            if (Bursting) {
                return;
            }
            Projectile.localAI[1] = 1f;
            SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(0.8f, 2.4f)
                    - Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f);
                EverdeepVFX.ShedDroplet(Projectile.Center
                    + Main.rand.NextVector2Circular(Radius * 0.5f, Radius * 0.5f), vel, 0.8f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_AbyssSpark>(Projectile.Center,
                    Main.rand.NextVector2Circular(2.5f, 2.5f),
                    SeaShrimpVFX.Glow, Main.rand.NextFloat(0.5f, 0.8f))?.Configure(10);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Wet, 180);
            target.AddBuff(ModContent.BuffType<AbyssalPressure>(), 120);
            StartBurst();
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_GhostRainMist>(Projectile.Center,
                new Vector2(0f, -0.2f), SeaShrimpVFX.Body * 0.45f, Main.rand.NextFloat(0.35f, 0.5f))
                ?.Configure(Main.rand.Next(26, 40));
        }

        /// <summary>伤害窗=完整膜:成形前与破膜期不咬人</summary>
        public override bool? CanDamage() => Age > 6 && !Bursting ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            return Vector2.Distance(nearest, Projectile.Center) <= Radius;
        }

        bool ISeaShrimpBubbleBody.GetBubbleBody(out SeaShrimpBubbleBodyParams body) {
            body = new SeaShrimpBubbleBodyParams {
                Center = Projectile.Center,
                Radius = Radius * MathHelper.Clamp(Age / (float)InflateFrames, 0.25f, 1f),
                Wobble = 0.5f + MathHelper.Clamp(Projectile.velocity.Length() / 7.5f, 0f, 1f) * 0.3f,
                Arm = 0f,
                Burst = Bursting ? BurstAge / (float)BurstFrames : 0f,
                Fade = MathHelper.Clamp(Age / 5f, 0f, 1f),
                Seed = Projectile.identity,
            };
            return true;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (SeaShrimpVFX.BubblePathReady) {
                //泡体由统一批绘层接管
                return false;
            }
            //着色器缺失回退:软光点
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || Bursting) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color col = new(SeaShrimpVFX.Film.R, SeaShrimpVFX.Film.G, SeaShrimpVFX.Film.B, 0);
            Main.spriteBatch.Draw(glow, pos, null, col * 0.6f, 0f, glow.Size() * 0.5f,
                Radius * 2.2f / glow.Width, SpriteEffects.None, 0f);
            return false;
        }
    }
}
