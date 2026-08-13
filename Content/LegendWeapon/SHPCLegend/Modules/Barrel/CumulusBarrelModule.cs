using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Cyberspaces;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Barrel
{
    /// <summary>积云枪管，光束留云核，满能降雷</summary>
    internal sealed class CumulusBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(165, 215, 255);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.08f;
            ctx.DamageMul += -0.08f;
            ctx.BeamLifeMul += 0.12f;
            ctx.ManaCostMul += 0.30f;
        }

        //同主云核上限
        private const int MaxConcurrentClouds = 6;
        //同点200px内已有则跳过
        private const float MinSpacing = 200f;
        //单束生成间隔帧
        private const int SpawnInterval = 60;

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % SpawnInterval != 0) return;
            int cloudType = ModContent.ProjectileType<SHPCCumulusNodeProj>();
            //上限+间距节流
            if (SHPCNaturalFx.CountOwned(beam.Projectile.owner, cloudType) >= MaxConcurrentClouds) return;
            Vector2 spawnPos = beam.Projectile.Center + Main.rand.NextVector2Circular(30f, 18f);
            if (SHPCNaturalFx.HasOwnedNear(beam.Projectile.owner, cloudType, spawnPos, MinSpacing)) return;
            int damage = Math.Max(beam.Projectile.damage / 3, 1);
            //凝聚音效移至云核首帧AI，旁观者同闻
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                spawnPos, Main.rand.NextVector2Circular(0.8f, 0.5f),
                cloudType, damage, 0f, beam.Projectile.owner);
        }
    }

    /// <summary>积云核，Fog+充能环，满能主闪+3支线</summary>
    internal sealed class SHPCCumulusNodeProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //Fog 团块数
        private const int BlobCount = 5;
        //充能扫描节流，每8帧
        private const int ChargeScanInterval = 8;

        //云块布局，首帧 seed 后稳定
        private Vector2[] blobOffsets;
        private float[] blobRotations;
        private float[] blobScales;
        private SpriteEffects[] blobMirrors;
        private float seedAngle;
        //PassiveCharge 缓存，按节流重算
        private float cachedChargeRate;

        //凝聚包络帧数
        private const int CondenseFrames = 14;
        private float Condense01 => MathHelper.Clamp((240 - Projectile.timeLeft) / (float)CondenseFrames, 0f, 1f);

        //距离衰减屏震，1300px 归零
        private static void ShakeNear(Vector2 pos, float amount) {
            float k = 1f - MathHelper.Clamp(Main.LocalPlayer.Distance(pos) / 1300f, 0f, 1f);
            SHPCNaturalFx.Shake(amount * k);
        }

        public override void SetDefaults() {
            Projectile.width = 72;
            Projectile.height = 44;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧 seed 云块，凝聚音效各端自播
            if (blobOffsets == null) {
                seedAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                blobOffsets = new Vector2[BlobCount];
                blobRotations = new float[BlobCount];
                blobScales = new float[BlobCount];
                blobMirrors = new SpriteEffects[BlobCount];
                for (int i = 0; i < BlobCount; i++) {
                    float a = seedAngle + i * (MathHelper.TwoPi / BlobCount) + Main.rand.NextFloat(-0.3f, 0.3f);
                    float r = Main.rand.NextFloat(0.55f, 1f);
                    blobOffsets[i] = new Vector2(MathF.Cos(a) * 40f * r, MathF.Sin(a) * 24f * r);
                    blobRotations[i] = Main.rand.NextFloat(MathHelper.TwoPi);
                    blobScales[i] = Main.rand.NextFloat(0.55f, 0.95f);
                    blobMirrors[i] = Main.rand.NextBool() ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                }
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.25f, Pitch = 0.4f }, Projectile.Center);
                }
            }
            Projectile.velocity *= 0.94f;
            //节流重算 PassiveCharge
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % ChargeScanInterval == 0) {
                cachedChargeRate = PassiveCharge();
            }
            Projectile.localAI[0] = MathF.Min(Projectile.localAI[0] + cachedChargeRate, 100f);
            //充能≥80预放电，否则方块粒子
            if (Main.netMode != NetmodeID.Server) {
                if (Projectile.localAI[0] > 80f && Main.GameUpdateCount % 8 == 0) {
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center + Main.rand.NextVector2Circular(36f, 18f), Main.rand.NextVector2Circular(0.6f, 0.6f), new Color(220, 240, 255), Main.rand.NextFloat(0.3f, 0.6f)).Configure(new Color(120, 130, 200), Main.rand.Next(10, 20), Main.rand.NextFloat(-0.2f, 0.2f), 0.7f);
                }
                else if (Main.GameUpdateCount % 12 == 0) {
                    Color color = Projectile.localAI[0] > 70f ? new Color(210, 240, 255) : new Color(150, 190, 220);
                    PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center + Main.rand.NextVector2Circular(44f, 20f), Main.rand.NextVector2Circular(0.5f, 0.5f), color, Main.rand.NextFloat(0.5f, 1.1f)).Configure(new Color(90, 130, 170), Main.rand.Next(18, 34));
                }
            }
            if (Projectile.localAI[0] < 100f) return;
            if (Projectile.owner == Main.myPlayer) {
                ReleaseRain();
            }
            Projectile.Kill();
        }

        private float PassiveCharge() {
            //返回每帧充能速率；扫描间隔只控制邻近弹幕的重算频率
            float charge = 0.08f;
            int beamType = ModContent.ProjectileType<CyberTraceBeamProj>();
            int orbType = ModContent.ProjectileType<CyberChargeOrbProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile other = Main.projectile[i];
                if (!other.active || other.owner != Projectile.owner || other.whoAmI == Projectile.whoAmI) continue;
                if (other.type != beamType && other.type != orbType) continue;
                if (Vector2.DistanceSquared(other.Center, Projectile.Center) > 130f * 130f) continue;
                charge += 3.2f;
            }
            return charge;
        }

        private void ReleaseRain() {
            //主闪 CyberDataArc，下220px
            int arcDmg = Math.Max(Projectile.damage * 2, 1);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<CyberDataArcProj>(),
                arcDmg, 0f, Projectile.owner,
                ai0: Main.rand.NextFloat(-30f, 30f), ai1: 220f);
            //三道支线
            for (int i = -1; i <= 1; i++) {
                Vector2 spawn = Projectile.Center + new Vector2(i * 36f, -10f);
                int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawn, Vector2.UnitY * 12f,
                    ModContent.ProjectileType<CyberTraceBeamProj>(),
                    Math.Max(Projectile.damage, 1), 0f, Projectile.owner, ai0: Main.rand.Next(3), ai1: 0.2f);
                if (idx >= 0 && idx < Main.maxProjectiles
                    && Main.projectile[idx].ModProjectile is CyberTraceBeamProj beam) {
                    beam.IsDerived = true;
                    beam.LifeMul = 0.35f;
                    beam.SpeedMul = 1.25f;
                }
            }
            //放电视听在 OnKill 各端自演，这里只管弹幕生成
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ || blobOffsets == null) return;
            //云体撕散，瓣位交给 Fog 真雾余韵
            float charge01 = MathHelper.Clamp(Projectile.localAI[0] / 100f, 0f, 1f);
            Color puffCol = Color.Lerp(new Color(225, 232, 245), new Color(185, 205, 240), charge01);
            for (int i = 0; i < blobOffsets.Length; i++) {
                Vector2 drift = blobOffsets[i].SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(0.4f, 1.1f)
                    + new Vector2(0f, -0.25f);
                PRTLoader.NewParticle<PRT_SHPCCumulusPuff>(Projectile.Center + blobOffsets[i], drift,
                    puffCol, blobScales[i] * 0.9f)
                    .Configure(blobRotations[i], blobMirrors[i] == SpriteEffects.FlipHorizontally,
                        Main.rand.Next(24, 36), 0.75f);
            }
            //满充放电死带余量 timeLeft，自然到期为0；阈值80容忍远端充能进度小幅滞后，防高充能停火后的假雷
            if (Projectile.localAI[0] >= 80f && timeLeft > 1) {
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, new Color(220, 240, 255), 0.05f).Configure(0.05f, 0.7f, 24);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center + Vector2.UnitY * 220f, Vector2.Zero, new Color(180, 210, 255), 0.05f).Configure(0.05f, 0.55f, 22);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.4f, Pitch = 0.2f }, Projectile.Center);
                ShakeNear(Projectile.Center, 2.8f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null || blobOffsets == null) return false;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float charge01 = MathHelper.Clamp(Projectile.localAI[0] / 100f, 0f, 1f);
            //云色随充能偏蓝白
            Color cloudCore = Color.Lerp(new Color(245, 248, 255), new Color(200, 220, 255), charge01);
            Color cloudEdge = Color.Lerp(new Color(160, 165, 175), new Color(110, 130, 180), charge01);
            Vector2 fogOrigin = fog.Size() * 0.5f;
            //Fog 团块微飘，凝聚期自小胀足；逐瓣镜像防贴纸感
            float drift = (float)Main.timeForVisualEffects * 0.02f;
            float condense = MathHelper.SmoothStep(0f, 1f, Condense01);
            for (int i = 0; i < blobOffsets.Length; i++) {
                Vector2 offset = blobOffsets[i] * (0.55f + 0.45f * condense)
                    + new Vector2(MathF.Sin(drift + i) * 1.6f, MathF.Cos(drift + i * 0.7f) * 1.2f);
                Color c = Color.Lerp(cloudCore, cloudEdge, i / (float)BlobCount);
                Main.spriteBatch.Draw(fog, baseScreen + offset, null, c * (0.95f * condense),
                    blobRotations[i] + drift * 0.3f, fogOrigin,
                    blobScales[i] * (0.5f + 0.5f * condense), blobMirrors[i], 0f);
            }
            //充能进度环
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (ring != null && charge01 > 0.05f) {
                Color ringCol = Color.Lerp(new Color(150, 200, 255), new Color(180, 130, 255), charge01) * (charge01 * 0.7f + 0.2f);
                ringCol.A = 0;
                Vector2 ringOrigin = ring.Size() * 0.5f;
                Main.spriteBatch.Draw(ring, baseScreen, null, ringCol,
                    drift * 1.5f, ringOrigin, 0.32f + charge01 * 0.18f, SpriteEffects.None, 0f);
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //真加色批源因子是 SourceAlpha，染色必须带 A
            float charge01 = MathHelper.Clamp(Projectile.localAI[0] / 100f, 0f, 1f);
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null || blobOffsets == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            //云腹内闪，hash 逐瓣轮亮，蓄能越高越频繁
            if (charge01 > 0.45f) {
                uint step = (Main.GameUpdateCount / 5) + (uint)Projectile.whoAmI * 7u;
                int flashIdx = (int)(step * 2654435761u % (uint)blobOffsets.Length);
                bool flashOn = step % 3u == 0 || (charge01 > 0.85f && step % 3u == 1);
                if (flashOn) {
                    Color flashCol = new Color(215, 228, 255) * (0.35f + 0.45f * charge01);
                    spriteBatch.Draw(glow, baseScreen + blobOffsets[flashIdx] * 0.8f, null, flashCol,
                        0f, glow.Size() * 0.5f, 0.9f + charge01 * 0.4f, SpriteEffects.None, 0f);
                }
            }
            if (charge01 < 0.6f) return;
            //高充能辉光，蓝白→雷紫
            float t = (charge01 - 0.6f) / 0.4f;
            Color inner = Color.Lerp(new Color(200, 220, 255), new Color(160, 130, 255), t) * (t * 0.5f);
            Color outer = Color.Lerp(new Color(80, 110, 180), new Color(60, 30, 130), t) * (t * 0.28f);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, 1.4f + t * 0.5f, 0f, 3);
            //准发闪烁
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (star != null && t > 0.65f) {
                float pulse = 0.6f + 0.4f * MathF.Sin((float)Main.timeForVisualEffects * 0.45f);
                Color starCol = new Color(220, 230, 255) * (t * pulse * 0.75f);
                Vector2 starOrigin = star.Size() * 0.5f;
                spriteBatch.Draw(star, baseScreen, null, starCol,
                    (float)Main.timeForVisualEffects * 0.05f, starOrigin, 0.35f * pulse, SpriteEffects.None, 0f);
            }
        }
    }
}
