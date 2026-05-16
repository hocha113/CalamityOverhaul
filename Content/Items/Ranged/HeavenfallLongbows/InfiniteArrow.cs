using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.HeavenfallLongbows
{
    internal class InfiniteArrow : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;

        //历史轨迹缓冲: 头部=位置0, 越靠尾端索引越大
        private const int TrailCacheLen = 36;
        //每隔多少子帧推一次历史点 (MaxUpdates=3, 子帧太密集会过短)
        private const int HistorySpacing = 2;

        private Vector2[] trailHistory;
        private int trailHistoryCount;
        private int historyPushTimer;
        private Vector2[] trailPositions;
        private int currentValidCount;

        private Trail trail;

        /// <summary>每个箭矢的色相偏移, 避免一波箭同色</summary>
        private float HueOffset => Projectile.identity * 0.1379f;

        private Color ChromaColor => VaultUtils.MultiStepColorLerp(
            (Projectile.ai[0] * 0.022f + HueOffset) % 1f, HeavenfallLongbow.rainbowColors);

        public override void SetDefaults() {
            Projectile.height = 54;
            Projectile.width = 54;
            Projectile.DamageType = EndlessDamageClass.Instance;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 100;
            Projectile.MaxUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

            Color light = ChromaColor;
            Lighting.AddLight(Projectile.Center, light.ToVector3() * 1.4f);

            //轻微向心加速 (保留原行为)
            Projectile.velocity += (Projectile.rotation + MathHelper.PiOver2).ToRotationVector2() * 0.1f;

            if (Projectile.ai[0] == 0) {
                Projectile.ai[1] = Main.rand.Next(30);
            }

            //发射初期附着玩家速度
            if (Projectile.ai[0] < 3) {
                Projectile.position += Main.player[Projectile.owner].velocity;
            }

            //历史轨迹推入 (头部最新)
            historyPushTimer++;
            if (historyPushTimer >= HistorySpacing) {
                historyPushTimer = 0;
                PushHistory(Projectile.Center);
            }

            //粒子: 节流到每 2 帧 1 个棱镜, 每 8 帧 1 段极光
            if (!VaultUtils.isServer && Projectile.ai[0] > 0) {
                if (Projectile.ai[0] % 2 == 0) {
                    Vector2 prismVel = Projectile.velocity.RotatedByRandom(0.6f) * Main.rand.NextFloat(0.05f, 0.25f);
                    float prismScale = Main.rand.NextFloat(0.75f, 1.15f);
                    Color prismCol = VaultUtils.MultiStepColorLerp(
                        (Projectile.ai[1] * 0.025f + HueOffset) % 1f, HeavenfallLongbow.rainbowColors);
                    PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                        Projectile.Center, prismVel, prismCol, prismScale,
                        Main.rand.Next(22, 34), Main.rand.NextFloat(2.5f, 5f),
                        Main.rand.NextBool(3)));
                }

                if (Projectile.ai[0] % 8 == 4) {
                    Vector2 auroraVel = -Projectile.velocity.SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.8f, 2.2f);
                    PRTLoader.AddParticle(new PRT_HeavenfallAurora(
                        Projectile.Center, auroraVel,
                        Main.rand.NextFloat(70f, 110f), Main.rand.NextFloat(14f, 22f),
                        Main.rand.Next(26, 38),
                        huePhase: HueOffset + Main.rand.NextFloat(0.4f),
                        hueSpeed: 0.022f,
                        driftScale: 0.85f));
                }
            }

            Projectile.ai[0]++;
            Projectile.ai[1]++;
        }

        private void PushHistory(Vector2 newPos) {
            //简单 ring shift: 整体向后挪一格, 头插入 newPos
            if (trailHistory == null) {
                trailHistory = new Vector2[TrailCacheLen];
                for (int i = 0; i < TrailCacheLen; i++) {
                    trailHistory[i] = newPos;
                }
            }
            for (int i = TrailCacheLen - 1; i > 0; i--) {
                trailHistory[i] = trailHistory[i - 1];
            }
            trailHistory[0] = newPos;
            if (trailHistoryCount < TrailCacheLen) {
                trailHistoryCount++;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                int lightningDamage = (int)(Projectile.damage * 1.3f);
                Vector2 ownerPos = Main.player[Projectile.owner].Center;
                Vector2 spanPos = ownerPos + ownerPos.To(target.Center).UnitVector().RotatedBy((120 + Main.rand.Next(120)) * CWRUtils.atoR) * Main.rand.Next(909, 1045);
                Vector2 vr = (target.Center - spanPos + target.velocity * 7.5f).SafeNormalize(Vector2.UnitY) * 17f;
                int lightning = Projectile.NewProjectile(Projectile.GetSource_FromThis(), spanPos, vr, ModContent.ProjectileType<HeavenRainbowImpact>(), lightningDamage, 0f, Projectile.owner);
                if (Main.projectile.IndexInRange(lightning)) {
                    Main.projectile[lightning].ai[0] = vr.ToRotation();
                    Main.projectile[lightning].ai[1] = Main.rand.Next(100);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                Color baseCol = ChromaColor;
                //环状棱镜爆发 (8 个)
                for (int i = 0; i < 8; i++) {
                    float ang = MathHelper.TwoPi * i / 8f + Main.rand.NextFloat(-0.1f, 0.1f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 4.5f);
                    Color col = VaultUtils.MultiStepColorLerp(
                        (i / 8f + HueOffset) % 1f, HeavenfallLongbow.rainbowColors);
                    PRTLoader.AddParticle(new PRT_HeavenfallPrism(
                        Projectile.Center, vel, col,
                        Main.rand.NextFloat(1.0f, 1.6f), Main.rand.Next(28, 42),
                        Main.rand.NextFloat(4f, 7f), shortStretch: true));
                }
                //极光收束环
                for (int i = 0; i < 4; i++) {
                    float ang = MathHelper.TwoPi * i / 4f + Main.rand.NextFloat(0.1f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(1.8f, 3.5f);
                    PRTLoader.AddParticle(new PRT_HeavenfallAurora(
                        Projectile.Center, vel,
                        Main.rand.NextFloat(110f, 160f), Main.rand.NextFloat(20f, 28f),
                        Main.rand.Next(28, 40),
                        huePhase: HueOffset + i * 0.25f, hueSpeed: 0.025f, driftScale: 1.1f));
                }
                _ = baseCol; //占位防警告 (baseCol 当前未用于直绘, 留作后续扩展锚点)
            }
            Projectile.Explode(spanSound: false);
        }

        //═════════════ Trail Width/Color ═════════════
        public float WidthFunc(float progress) {
            if (trailHistory == null) {
                return 0f;
            }
            float validRatio = MathF.Max((float)currentValidCount / TrailCacheLen, 0.1f);
            float clipped = MathHelper.Clamp(progress / validRatio, 0f, 1f);
            //头部圆滑上升, 尾端平方衰减
            float noseRise = MathF.Sin(MathF.Min(clipped / 0.08f, 1f) * MathHelper.PiOver2);
            float tailTaper = 1f - MathF.Pow(clipped, 1.8f);
            return MathF.Max(noseRise * tailTaper, 0f) * (30f * Projectile.scale);
        }

        public Color ColorFunc(Vector2 _) => Color.White * Projectile.Opacity;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (trailHistory == null || trailHistoryCount < 3) {
                return;
            }

            Effect shader = EffectLoader.HeavenfallPrismTrail?.Value;
            if (shader == null) {
                return;
            }
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (noise == null) {
                return;
            }

            //构建当前帧拖尾点 (头插入实时 Center, 后接历史点)
            if (trailPositions == null) {
                trailPositions = new Vector2[TrailCacheLen];
            }
            trailPositions[0] = Projectile.Center;
            for (int i = 1; i < TrailCacheLen; i++) {
                int histIdx = Math.Min(i - 1, trailHistoryCount - 1);
                trailPositions[i] = trailHistory[Math.Max(histIdx, 0)];
            }
            currentValidCount = Math.Min(trailHistoryCount + 1, TrailCacheLen);

            trail ??= new Trail(trailPositions, WidthFunc, ColorFunc);
            trail.TrailPositions = trailPositions;

            //寿命淡出
            float lifeFade = MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);

            shader.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.045f);
            shader.Parameters["fadeAlpha"]?.SetValue(Projectile.Opacity * lifeFade);
            shader.Parameters["coreIntensity"]?.SetValue(0.9f);
            shader.Parameters["dispersion"]?.SetValue(0.06f);
            shader.Parameters["flowSpeed"]?.SetValue(0.7f);
            shader.Parameters["hueOffset"]?.SetValue(HueOffset);
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            shader.CurrentTechnique = shader.Techniques["Trail"];

            GraphicsDevice device = Main.graphics.GraphicsDevice;
            BlendState old = device.BlendState;
            device.BlendState = BlendState.Additive;
            trail.DrawTrail(shader);
            device.BlendState = old;
        }
    }
}
