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

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaCultist
{
    /// <summary>
    /// 鬼奴邪教徒的血雷缓行球：一颗低速漂行的紫电血核，
    /// 周期性向近旁最近的敌人劈出链状电弧（ThunderTrail 双层，预警帧短、放电窗有伤害），
    /// 电弧穿过湖面时在交点炸小水花。到寿自爆一记小电爆。
    /// 放电拍点 Life 本地确定性推进；弧目标各端就近自选（位置输入一致，伤害仅 owner 端结算）
    /// </summary>
    internal class KikasaCultistThunderOrb : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        internal const float DriftSpeed = 4.2f;

        private const int TotalLife = 190;
        private const int WarmupFrames = 26;
        private const int DischargeGap = 36;
        private const int ArcLife = 10;
        private const float ArcRange = 340f;

        private ref float Life => ref Projectile.localAI[0];

        //弧状态：本地表现 + owner 端伤害窗共用
        private int arcTimer;
        private int arcTargetIdx = -1;
        private Vector2 lastArcPos;
        private bool splashedThisArc;

        private ThunderTrail mainTrail;
        private ThunderTrail coreTrail;

        private float Seed => Projectile.identity * 0.7391f % 4.3f;

        private float VisualFade => MathHelper.Clamp(Life / 5f, 0f, 1f)
            * MathHelper.Clamp((TotalLife - Life) / 10f, 0f, 1f);

        public override void SetStaticDefaults()
            => ProjectileID.Sets.DrawScreenCheckFluff[Projectile.type] = 520;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife + 20;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 26;
        }

        /// <summary>球体常燃可触，电弧只在放电窗结算</summary>
        public override bool? CanDamage() => null;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (projHitbox.Intersects(targetHitbox)) {
                return true;
            }
            if (arcTimer > 0) {
                float _ = 0f;
                return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    Projectile.Center, lastArcPos, 30f, ref _);
            }
            return false;
        }

        public override void AI() {
            Life++;

            //缓行：极低转率追向最近敌人，加一缕确定性纵向漂浮——雷云在游
            int nearest = FindNearest(ArcRange * 2.2f);
            if (nearest >= 0) {
                float wantAngle = (Main.npc[nearest].Center - Projectile.Center).ToRotation();
                float newAngle = Projectile.velocity.ToRotation().AngleTowards(wantAngle, 0.012f);
                Projectile.velocity = newAngle.ToRotationVector2() * DriftSpeed;
            }
            Projectile.velocity.Y += MathF.Sin(Life * 0.06f + Seed) * 0.02f;
            Projectile.rotation += 0.02f;

            //周期放电：预热后每 36 帧劈一道（确定性拍点，各端同拍；目标就近自选）
            if (Life >= WarmupFrames && (int)Life % DischargeGap == 0) {
                int target = FindNearest(ArcRange);
                if (target >= 0) {
                    arcTimer = ArcLife;
                    arcTargetIdx = target;
                    lastArcPos = Main.npc[target].Center;
                    splashedThisArc = false;
                    SoundEngine.PlaySound(SoundID.Item93 with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.3f, Pitch = -0.4f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            if (arcTimer > 0) {
                arcTimer--;
                //弧内目标还活着就跟着劈，死了钉在最后位置
                if (arcTargetIdx >= 0 && Main.npc[arcTargetIdx]?.active == true) {
                    lastArcPos = Main.npc[arcTargetIdx].Center;
                }
                UpdateArcSplash();
                //放电时球体收缩回弹在绘制层读出；这里补沿弧火花
                if (!Main.dedServ && arcTimer % 3 == 1) {
                    Vector2 sparkPos = Vector2.Lerp(Projectile.Center, lastArcPos, Main.rand.NextFloat());
                    PRTLoader.NewParticle<PRT_Spark>(sparkPos, Main.rand.NextVector2Circular(2.5f, 2.5f),
                        Color.Lerp(KikasaCultistServant.ThunderTint, KikasaCultistServant.RuneCore, Main.rand.NextFloat(0.5f)),
                        Main.rand.NextFloat(0.6f, 1f))?.Configure(false, Main.rand.Next(8, 14));
                }
            }

            //常燃细火花与光照
            if (!Main.dedServ && (int)Life % 5 == 2) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Circular(1.2f, 1.2f),
                    KikasaCultistServant.ThunderTint * 0.8f,
                    Main.rand.NextFloat(0.5f, 0.85f))?.Configure(false, Main.rand.Next(7, 12));
            }
            float glow = VisualFade;
            Lighting.AddLight(Projectile.Center, 0.32f * glow, 0.22f * glow, 0.5f * glow);

            //到寿自爆小电爆（各端同帧规则）
            if (Life >= TotalLife) {
                Projectile.Kill();
            }
        }

        /// <summary>电弧过水线：交点炸一次小水花（帧内一次预算，viewed 门控）</summary>
        private void UpdateArcSplash() {
            if (splashedThisArc || Main.dedServ) {
                return;
            }
            Player owner = Main.player[Projectile.owner];
            if (owner?.active != true || !owner.TryGetModPlayer(out KikasaDomainPlayer domain)
                || KikasaDomain.Viewed != domain || !domain.AnyActive || domain.RiseT < 0.5f) {
                return;
            }
            float lakeY = domain.LakeWorldY;
            float y0 = Projectile.Center.Y, y1 = lastArcPos.Y;
            bool crossing = (y0 - lakeY) * (y1 - lakeY) < 0f;
            bool grazing = MathF.Abs(y1 - lakeY) < 40f;
            if (!crossing && !grazing) {
                return;
            }
            splashedThisArc = true;
            float t = crossing ? (lakeY - y0) / (y1 - y0) : 1f;
            float x = MathHelper.Lerp(Projectile.Center.X, lastArcPos.X, MathHelper.Clamp(t, 0f, 1f));
            Vector2 hit = new(x, lakeY);
            KikasaDomainDeco.SplashAt(hit, 4);
            KikasaDomainDeco.RippleAt(hit, 0.6f);
        }

        private int FindNearest(float range) {
            int best = -1;
            float bestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        //==================== 命中与谢幕 ====================

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    target.Center + Main.rand.NextVector2Circular(14f, 14f),
                    Main.rand.NextVector2Circular(3f, 3f),
                    KikasaCultistServant.ThunderTint, Main.rand.NextFloat(0.7f, 1.1f))
                    ?.Configure(false, Main.rand.Next(9, 15));
            }
            SoundEngine.PlaySound(SoundID.NPCHit53 with { Volume = 0.4f, Pitch = 0.1f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Item94 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f),
                    Color.Lerp(KikasaCultistServant.ThunderTint, KikasaCultistServant.RuneCore, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.7f, 1.2f))?.Configure(false, Main.rand.Next(10, 18));
            }
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                KikasaCultistServant.ThunderTint, 0.07f)
                ?.Configure(new Vector2(1f, 1f), 0f, 0.22f, 8);
        }

        //==================== 绘制 ====================

        /// <summary>重建电弧折线：两端锚定、中段确定性正弦摆 + ThunderTrail 自身抖动</summary>
        private void BuildArcPath() {
            const int pointCount = 12;
            Vector2[] points = new Vector2[pointCount];
            Vector2 start = Projectile.Center;
            Vector2 end = lastArcPos;
            Vector2 dir = end - start;
            Vector2 perp = dir.SafeNormalize(Vector2.UnitY).RotatedBy(MathHelper.PiOver2);
            float waveSeed = Main.GlobalTimeWrappedHourly * 11f + Seed;
            float power = arcTimer / (float)ArcLife;
            for (int i = 0; i < pointCount; i++) {
                float t = i / (float)(pointCount - 1);
                float envelope = MathF.Sin(t * MathHelper.Pi);
                float wave = MathF.Sin(waveSeed + t * 9f) * 13f * envelope * power;
                points[i] = start + dir * t + perp * wave;
            }

            Texture2D thunderTex = CWRAsset.ThunderTrail?.Value;
            if (mainTrail == null && thunderTex != null) {
                mainTrail = new ThunderTrail(CWRAsset.ThunderTrail, GetMainWidth, GetMainColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 3,
                };
                mainTrail.SetRange((0, 8));
                mainTrail.SetExpandWidth(5);
                coreTrail = new ThunderTrail(CWRAsset.ThunderTrail, GetCoreWidth, GetCoreColor, GetArcAlpha) {
                    CanDraw = true,
                    UseNonOrAdd = true,
                    PartitionPointCount = 2,
                };
                coreTrail.SetRange((0, 4));
                coreTrail.SetExpandWidth(3);
            }
            if (mainTrail == null) {
                return;
            }
            mainTrail.BasePositions = points;
            coreTrail.BasePositions = points;
            if (arcTimer % 3 == 0) {
                mainTrail.RandomThunder();
                coreTrail.RandomThunder();
            }
        }

        private float GetMainWidth(float factor) => 13f + 6f * MathF.Sin(factor * MathHelper.Pi);
        private float GetCoreWidth(float factor) => 5f + 3f * MathF.Sin(factor * MathHelper.Pi);
        private Color GetMainColor(float factor) => KikasaCultistServant.ThunderTint;
        private Color GetCoreColor(float factor) => KikasaCultistServant.RuneCore;
        private float GetArcAlpha(float factor) => arcTimer / (float)ArcLife;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return false;
            }
            float fade = VisualFade;
            if (fade <= 0.02f) {
                return false;
            }

            //电弧：ThunderTrail 直接走图元批
            if (arcTimer > 0) {
                BuildArcPath();
                mainTrail?.DrawThunder(Main.instance.GraphicsDevice);
                coreTrail?.DrawThunder(Main.instance.GraphicsDevice);
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 gOrigin = glow.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //放电收缩：劈出的一瞬球体缩四成再弹回
            float discharge = arcTimer > 0 ? 1f - 0.4f * MathF.Sin(arcTimer / (float)ArcLife * MathHelper.Pi) : 1f;
            float wob = 1f + 0.08f * MathF.Sin(Life * 0.4f + Seed * 3f);
            float r = 13f * discharge * wob;

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            //三层雷核：深血晕→紫电体→白芯，外加两条确定性抖动的短弧须
            sb.Draw(glow, pos, null, KikasaCultistServant.BloodDeep * (0.5f * fade), 0f, gOrigin,
                new Vector2(r * 2.6f * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, KikasaCultistServant.ThunderTint * (0.85f * fade), 0f, gOrigin,
                new Vector2(r * 1.5f * 2f / glow.Width), SpriteEffects.None, 0f);
            sb.Draw(glow, pos, null, KikasaCultistServant.RuneCore * (0.55f * fade), 0f, gOrigin,
                new Vector2(r * 0.6f * 2f / glow.Width), SpriteEffects.None, 0f);
            int jitterSeed = (int)(Main.GlobalTimeWrappedHourly * 14f);
            for (int i = 0; i < 2; i++) {
                float h = KikasaCultistRunes.Hash01(jitterSeed * 2.3f + i * 7.7f + Seed);
                float ang = h * MathHelper.TwoPi + Projectile.rotation;
                Vector2 tip = ang.ToRotationVector2() * (r + 7f + h * 9f);
                sb.Draw(glow, pos + tip * 0.5f, null, KikasaCultistServant.ThunderTint * (0.45f * fade),
                    ang, gOrigin,
                    new Vector2(tip.Length() * 1.05f * 2f / glow.Width, 2.4f * 2f / glow.Height), SpriteEffects.None, 0f);
            }

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
