using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.NeutronWands
{
    /// <summary>
    /// 脉冲星：抛出后复合制动锚定，自旋逐步爬升，磁极双束扫掠战场。
    /// 右键磁制动会拖慢自旋并给壳层充压，松手星震。
    /// </summary>
    internal class NeutronPulsar : ModProjectile, IPrimitiveDrawable, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const int MaxLife = 660;
        /// <summary>制动段长度，之后锚定不再移动</summary>
        internal const int BrakeFrames = 26;
        private const int SpinUpFrames = 120;
        internal const int GlitchFrames = 150;
        private const float BeamLength = 560f;
        private const float BeamHitRadius = 17f;
        private const float BodyQuad = 260f;
        /// <summary>每帧速度衰减率</summary>
        private const float BrakeDecay = 0.885f;
        /// <summary>
        /// 制动段总行程系数：AI 先衰减再位移，故为 k*(1-k^n)/(1-k)。
        /// 初速乘它即落点距离，供播星反解初速。
        /// </summary>
        internal const float TravelFactor = 7.375f;

        //系列配色，取自 NeutronGravityWell 的核心/吸积/外缘三色
        private static readonly Vector3 ColHot = new(0.78f, 0.84f, 1f);
        private static readonly Vector3 ColMain = new(0.54f, 0.31f, 1f);
        private static readonly Vector3 ColBeam = new(0.47f, 0.71f, 1f);
        private static readonly Vector3 ColDeep = new(0.12f, 0.10f, 0.50f);

        internal static readonly Color ParticleViolet = new(138, 80, 255);
        internal static readonly Color ParticleBlue = new(120, 180, 255);
        internal static readonly Color ParticleHot = new(199, 215, 255);

        /// <summary>出生磁轴相位，随生成包同步</summary>
        public ref float MagPhase => ref Projectile.ai[0];

        private float spinPhase;
        private float quake;
        private bool quakeDriven;
        private int glitchTimer;
        private int anchorFlash;
        /// <summary>被超编挤掉的星，超频窗口烧完即散</summary>
        private bool forcedOut;

        private int Age => MaxLife - Projectile.timeLeft;
        private bool Anchored => Age >= BrakeFrames;
        private float Seed => Projectile.identity * 0.173f % 1f;
        private bool Glitching => glitchTimer > 0;
        private float Glitch01 => Glitching ? glitchTimer / (float)GlitchFrames : 0f;
        /// <summary>超频中的星不再吃制动，也不参与超编淘汰</summary>
        public bool CanBrake => !Glitching && !forcedOut;

        /// <summary>本帧自旋角速度，制动压低、星震超频</summary>
        private float SpinRate {
            get {
                if (Glitching) {
                    return 0.32f;
                }
                if (!Anchored) {
                    return 0.03f;
                }
                float t = MathHelper.Clamp((Age - BrakeFrames) / (float)SpinUpFrames, 0f, 1f);
                return MathHelper.Lerp(0.03f, 0.135f, t * t) * (1f - quake * 0.85f);
            }
        }

        /// <summary>磁轴与自旋轴错开，故光束扫掠而非静止</summary>
        private float MagAxis => spinPhase + MagPhase;
        /// <summary>灯塔包络，每转两次打峰</summary>
        private float BeamPhase => MathF.Pow(Math.Abs(MathF.Cos(spinPhase)), 5f);
        /// <summary>制动时光束缩短，星震后暴涨</summary>
        private float BeamReach => (1f - quake * 0.55f) * (Glitching ? 1.55f : 1f);
        private float BeamHalfWidth => 30f * (1f - quake * 0.2f) * (Glitching ? 2.2f : 1f);
        private float FadeIn => MathHelper.Clamp(Age / 8f, 0f, 1f);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;

        public override void SetDefaults() {
            //大盒子只为让 Colliding 有机会跑到，实判在重写里
            Projectile.width = Projectile.height = 1240;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = MaxLife;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.ArmorPenetration = 80;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => !Anchored;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(glitchTimer);
            writer.Write(quake);
            writer.Write(spinPhase);
            writer.Write(forcedOut);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            glitchTimer = reader.ReadInt32();
            quake = reader.ReadSingle();
            spinPhase = reader.ReadSingle();
            forcedOut = reader.ReadBoolean();
        }

        /// <summary>手持体每帧灌入制动量</summary>
        public void DriveQuake(float value) {
            if (!CanBrake) {
                return;
            }
            quake = value;
            quakeDriven = true;
        }

        /// <summary>星震：壳层崩裂、磁重联、随后进入超频窗口</summary>
        public void TriggerQuake(float power, bool forced = false) {
            if (Glitching) {
                return;
            }

            forcedOut |= forced;
            power = MathHelper.Clamp(power, 0f, 1f);
            glitchTimer = (int)(GlitchFrames * (0.55f + power * 0.45f));
            quake = 0f;

            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.9f, Pitch = -0.45f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.7f, Pitch = 0.35f + power * 0.3f }, Projectile.Center);

            if (Projectile.IsOwnedByLocalPlayer()) {
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero
                    , ModContent.ProjectileType<NeutronStarquake>()
                    , (int)(Projectile.damage * (1.4f + power * 1.1f)), Projectile.knockBack * 2f
                    , Projectile.owner, power);
                Main.LocalPlayer.CWR().GetScreenShake(4f + power * 4f);
                Projectile.netUpdate = true;
            }

            SpawnQuakeParticles(power);
        }

        public override void AI() {
            if (!Anchored) {
                Projectile.velocity *= BrakeDecay;
                ShedFlightTrail();
            }
            else if (Age == BrakeFrames) {
                OnAnchor();
            }

            spinPhase += SpinRate;
            if (spinPhase > MathHelper.TwoPi * 64f) {
                spinPhase -= MathHelper.TwoPi * 64f;
            }

            if (glitchTimer > 0) {
                glitchTimer--;
                //被挤掉的星把最后一段超频烧完就散，各端由同步过的标记自行判定
                if (glitchTimer == 0 && forcedOut) {
                    Projectile.Kill();
                    return;
                }
            }
            if (anchorFlash > 0) {
                anchorFlash--;
            }

            //手持体未灌值就自然泄压
            if (!quakeDriven && quake > 0f) {
                quake = MathHelper.Lerp(quake, 0f, 0.09f);
                if (quake < 0.004f) {
                    quake = 0f;
                }
            }
            quakeDriven = false;

            if (Anchored) {
                DragNearbyEnemies();
            }

            EmitParticles();

            float glow = 0.55f + BeamPhase * 0.5f + Glitch01 * 0.8f;
            Lighting.AddLight(Projectile.Center, ColMain * glow);
        }

        private void OnAnchor() {
            Projectile.velocity = Vector2.Zero;
            anchorFlash = 10;
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = 0.35f }, Projectile.Center);

            if (VaultUtils.isServer) {
                return;
            }
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, ParticleBlue, 0.2f)
                ?.Configure(0.2f, 1.5f, 22);
            for (int i = 0; i < 14; i++) {
                float ang = MathHelper.TwoPi * i / 14f;
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center, ang.ToRotationVector2() * Main.rand.NextFloat(3f, 8f)
                    , Color.Lerp(ParticleViolet, ParticleHot, Main.rand.NextFloat()), Main.rand.NextFloat(0.5f, 0.9f))
                    ?.Configure(false, Main.rand.Next(12, 20));
            }
        }

        /// <summary>制动段掉渣，速率随速度衰减</summary>
        private void ShedFlightTrail() {
            if (VaultUtils.isServer) {
                return;
            }
            float speed = Projectile.velocity.Length();
            int count = (int)MathHelper.Clamp(speed * 0.25f, 1f, 5f);
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + Main.rand.NextVector2Circular(9f, 9f)
                    , -Projectile.velocity * Main.rand.NextFloat(0.12f, 0.3f) + Main.rand.NextVector2Circular(1.6f, 1.6f)
                    , Color.Lerp(ParticleViolet, ParticleBlue, Main.rand.NextFloat()), Main.rand.NextFloat(0.35f, 0.7f))
                    ?.Configure(false, Main.rand.Next(9, 16));
            }
        }

        /// <summary>锚定后的弱引力，只拽得动非 boss</summary>
        private void DragNearbyEnemies() {
            float radius = 220f + Glitch01 * 120f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile) || npc.boss || npc.knockBackResist <= 0f) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist > radius || dist < 24f) {
                    continue;
                }
                float factor = 1f - dist / radius;
                npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero)
                    * 2.6f * factor * factor * npc.knockBackResist;
            }
        }

        private void EmitParticles() {
            if (VaultUtils.isServer || !Anchored) {
                return;
            }

            //吸积：外围物质沿开普勒轨螺旋落向壳面
            if (Projectile.timeLeft % 6 == 0) {
                PRTLoader.NewParticle<PRT_GravityVortex>(Projectile.Center, Vector2.Zero
                    , Color.Lerp(ParticleViolet, ParticleBlue, Main.rand.NextFloat()), Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), Main.rand.NextFloat(70f, 140f), Main.rand.Next(34, 52));
            }

            //磁极出流：沿磁轴甩出的等离子团
            if (Projectile.timeLeft % 3 == 0) {
                Vector2 axis = MagAxis.ToRotationVector2();
                int sign = Main.rand.NextBool() ? 1 : -1;
                Vector2 spawn = Projectile.Center + axis * sign * Main.rand.NextFloat(26f, 46f);
                PRTLoader.NewParticle<PRT_Spark>(spawn
                    , axis * sign * Main.rand.NextFloat(5f, 12f) * (1f + Glitch01) + Main.rand.NextVector2Circular(1.4f, 1.4f)
                    , Color.Lerp(ParticleBlue, ParticleHot, Main.rand.NextFloat(0.5f)), Main.rand.NextFloat(0.5f, 0.95f))
                    ?.Configure(false, Main.rand.Next(9, 16));
            }

            //制动期壳层泄压，裂缝喷屑
            if (quake > 0.25f && Projectile.timeLeft % 2 == 0) {
                Vector2 edge = Main.rand.NextVector2CircularEdge(1f, 1f);
                PRTLoader.NewParticle<PRT_SpaceFracture>(Projectile.Center + edge * Main.rand.NextFloat(18f, 30f)
                    , edge * Main.rand.NextFloat(1.5f, 4.5f) * quake
                    , Color.Lerp(ParticleViolet, ParticleHot, quake), Main.rand.NextFloat(0.3f, 0.6f))
                    ?.Configure(Main.rand.Next(12, 22), Main.rand.NextFloat(-0.4f, 0.4f));
            }
        }

        private void SpawnQuakeParticles(float power) {
            if (VaultUtils.isServer) {
                return;
            }
            int count = (int)(18 + power * 22);
            for (int i = 0; i < count; i++) {
                float ang = MathHelper.TwoPi * i / count;
                PRTLoader.NewParticle<PRT_SpaceFracture>(Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(8f, 26f)
                    , ang.ToRotationVector2() * Main.rand.NextFloat(6f, 17f) * (0.7f + power)
                    , Color.Lerp(ParticleHot, ParticleViolet, Main.rand.NextFloat()), Main.rand.NextFloat(0.45f, 1f))
                    ?.Configure(Main.rand.Next(20, 36), Main.rand.NextFloat(-0.5f, 0.5f));
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (FadeIn < 0.2f) {
                return false;
            }

            bool core = VaultUtils.CircleIntersectsRectangle(Projectile.Center, 26f, targetHitbox);
            if (core || !Anchored) {
                return core;
            }

            float len = BeamLength * BeamReach;
            float rad = BeamHitRadius * (Glitching ? 2.2f : 1f);
            Vector2 axis = MagAxis.ToRotationVector2();
            float point = 0f;
            for (int s = -1; s <= 1; s += 2) {
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , Projectile.Center, Projectile.Center + axis * (len * s), rad, ref point)) {
                    return true;
                }
            }
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (Glitching) {
                modifiers.SourceDamage *= 1.6f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<VoidErosion>(), 1200);

            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(target.Center + Main.rand.NextVector2Circular(12f, 12f)
                    , Main.rand.NextVector2Circular(6f, 6f)
                    , Color.Lerp(ParticleBlue, ParticleHot, Main.rand.NextFloat(0.6f)), Main.rand.NextFloat(0.5f, 1f))
                    ?.Configure(false, Main.rand.Next(10, 17));
            }
        }

        /// <summary>寿终不是凭空消失：壳层蒸发的余韵比本体活得久</summary>
        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.5f, Pitch = -0.3f }, Projectile.Center);
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, ParticleViolet, 0.35f)
                ?.Configure(0.35f, 2.1f, 34);

            for (int i = 0; i < 20; i++) {
                float ang = MathHelper.TwoPi * i / 20f;
                PRTLoader.NewParticle<PRT_SpaceFracture>(Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(6f, 20f)
                    , ang.ToRotationVector2() * Main.rand.NextFloat(2f, 6f)
                    , Color.Lerp(ParticleViolet, ParticleBlue, Main.rand.NextFloat()) * 0.9f, Main.rand.NextFloat(0.35f, 0.7f))
                    ?.Configure(Main.rand.Next(26, 46), Main.rand.NextFloat(-0.35f, 0.35f));
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_GravityVortex>(Projectile.Center, Vector2.Zero
                    , ParticleBlue, Main.rand.NextFloat(0.3f, 0.55f))
                    ?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), Main.rand.NextFloat(30f, 90f), Main.rand.Next(30, 50));
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public bool CanDrawCustom() => false;
        public void DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>本体透镜；星震另有 ShockwaveRing 由 NeutronStarquake 出</summary>
        public void Warp() {
            float intensity = (0.16f + Glitch01 * 0.3f + quake * 0.22f) * FadeIn;
            if (intensity < 0.01f) {
                return;
            }
            float size = 210f + Glitch01 * 130f;
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size, intensity, 1f, 0f, "GravitationalLens");
        }

        public void DrawPrimitives() {
            if (VaultUtils.isServer || FadeIn <= 0.01f) {
                return;
            }
            DrawBeams();
            DrawBody();
        }

        private void DrawBeams() {
            if (!Anchored) {
                return;
            }

            Effect effect = EffectLoader.NeutronPulseBeam?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            float len = BeamLength * BeamReach;
            float halfWidth = BeamHalfWidth;
            if (len < 20f) {
                return;
            }

            Vector2 axis = MagAxis.ToRotationVector2();
            Vector2 perp = axis.RotatedBy(MathHelper.PiOver2);
            //根部埋进壳里，避免露出接缝
            float rootBack = 14f;

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.Additive;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uFade"]?.SetValue(FadeIn * (0.85f + Glitch01 * 0.55f));
            effect.Parameters["uPhase"]?.SetValue(BeamPhase);
            effect.Parameters["uGlitch"]?.SetValue(Glitch01);
            //锥形只由着色器负责，顶点保持等宽画布，否则两次收缩会把光束勒成细线
            effect.Parameters["uSpread"]?.SetValue(0.92f);
            effect.Parameters["uColHot"]?.SetValue(ColHot);
            effect.Parameters["uColBeam"]?.SetValue(ColBeam);
            effect.Parameters["uColMain"]?.SetValue(ColMain);
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            for (int s = -1; s <= 1; s += 2) {
                Vector2 dir = axis * s;
                Vector2 root = Projectile.Center - dir * rootBack;
                Vector2 tip = Projectile.Center + dir * len;

                verts[0] = new VertexPositionColorTexture((root + perp * halfWidth).ToVector3(), Color.White, new Vector2(0f, 0f));
                verts[1] = new VertexPositionColorTexture((root - perp * halfWidth).ToVector3(), Color.White, new Vector2(0f, 1f));
                verts[2] = new VertexPositionColorTexture((tip + perp * halfWidth).ToVector3(), Color.White, new Vector2(1f, 0f));
                verts[3] = new VertexPositionColorTexture((tip - perp * halfWidth).ToVector3(), Color.White, new Vector2(1f, 1f));

                foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
                }
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        private void DrawBody() {
            Effect effect = EffectLoader.NeutronPulsar?.Value;
            Texture2D cells = CWRAsset.Extra_193?.Value;
            Texture2D quad = VaultAsset.placeholder2?.Value;
            if (effect == null || cells == null || quad == null) {
                return;
            }

            float side = BodyQuad * (1f + Glitch01 * 0.18f);
            float squash = 1f + MathHelper.Clamp(Projectile.velocity.Length() / 30f, 0f, 0.55f);

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSpin"]?.SetValue(spinPhase);
            effect.Parameters["uSpinRate"]?.SetValue(MathHelper.Clamp(SpinRate / 0.32f, 0f, 1f));
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uFade"]?.SetValue(FadeIn);
            effect.Parameters["uRadius"]?.SetValue(0.085f * (1f + Glitch01 * 0.16f + anchorFlash / 26f));
            effect.Parameters["uQuake"]?.SetValue(quake);
            effect.Parameters["uGlitch"]?.SetValue(Glitch01);
            effect.Parameters["uMagAngle"]?.SetValue(MagAxis);
            effect.Parameters["uSquash"]?.SetValue(squash);
            effect.Parameters["uMotAngle"]?.SetValue(Projectile.velocity.ToRotation());
            effect.Parameters["uColHot"]?.SetValue(ColHot);
            effect.Parameters["uColMain"]?.SetValue(ColMain);
            effect.Parameters["uColBeam"]?.SetValue(ColBeam);
            effect.Parameters["uColDeep"]?.SetValue(ColDeep);
            effect.Parameters["uCellTex"]?.SetValue(cells);

            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = quad.Size() * 0.5f;
            Vector2 scale = new(side / quad.Width, side / quad.Height);

            //实心壳走预乘 AlphaBlend，磁层走加色
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique = effect.Techniques["Crust"];
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(quad, drawPos, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            sb.End();

            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique = effect.Techniques["Field"];
            effect.CurrentTechnique.Passes[0].Apply();
            sb.Draw(quad, drawPos, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            sb.End();
        }
    }
}
