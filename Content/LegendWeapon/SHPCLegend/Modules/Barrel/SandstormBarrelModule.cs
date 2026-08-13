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
    /// <summary>砂暴枪管，光束卷砂幕，磨蚀敌与削弱敌弹</summary>
    internal sealed class SandstormBarrelModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Barrel;
        public override Color TintColor => new(220, 180, 90);

        public override void Apply(ref ShootContext ctx) {
            ctx.AttackSpeedMul += 0.06f;
            ctx.DamageMul += -0.08f;
            ctx.BeamExtraPierce += 1;
            ctx.ManaCostMul += 0.48f;
        }

        //同主砂幕上限(寿命90≈最多3个)
        private const int MaxConcurrentCurtains = 3;
        //同点160px内已有则跳过
        private const float MinSpacing = 160f;
        //单束生成间隔帧
        private const int SpawnInterval = 42;

        public override void OnBeamAI(CyberTraceBeamProj beam) {
            if (beam.IsDerived || beam.Projectile.owner != Main.myPlayer) return;
            if ((Main.GameUpdateCount + (uint)beam.Projectile.whoAmI) % SpawnInterval != 0) return;
            int curtainType = ModContent.ProjectileType<SHPCSandCurtainProj>();
            if (SHPCNaturalFx.CountOwned(beam.Projectile.owner, curtainType) >= MaxConcurrentCurtains) return;
            Vector2 pos = beam.Projectile.Center + beam.Projectile.velocity.SafeNormalize(Vector2.UnitX) * 42f;
            if (SHPCNaturalFx.HasOwnedNear(beam.Projectile.owner, curtainType, pos, MinSpacing)) return;
            Projectile.NewProjectile(beam.Projectile.GetSource_FromThis(),
                pos, beam.Projectile.velocity.SafeNormalize(Vector2.Zero) * 1.5f,
                curtainType, Math.Max(beam.Projectile.damage / 2, 1), 0f, beam.Projectile.owner);
        }
    }

    /// <summary>砂幕，干沙磨蚀旋幕；真alpha尘体遮蔽+双反转旋涡+磨蚀粒闪</summary>
    internal sealed class SHPCSandCurtainProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        private float radius => 60f;

        /// <summary>视觉包络，12f 卷起→平台期→末 18f 散逸；判定不吃它</summary>
        private float VisualEnv {
            get {
                float fadeIn = MathHelper.Clamp((90 - Projectile.timeLeft) / 12f, 0f, 1f);
                float fadeOut = MathHelper.Clamp(Projectile.timeLeft / 18f, 0f, 1f);
                return fadeIn * fadeOut;
            }
        }

        public override void SetDefaults() {
            Projectile.width = (int)(radius * 2);
            Projectile.height = (int)(radius * 2);
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 90;
            Projectile.DamageType = DamageClass.Magic;
        }

        //命中扫描，每3帧错峰
        private const int ScanInterval = 3;

        public override void AI() {
            //出生拍,卷起音+上卷沙羽
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.3f, Pitch = 0.15f }, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        PRTLoader.NewParticle<PRT_Smoke>(
                            Projectile.Center + Main.rand.NextVector2Circular(radius * 0.5f, radius * 0.3f),
                            new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-3f, -1.2f)),
                            new Color(225, 190, 110), Main.rand.NextFloat(0.5f, 0.9f))
                            .Configure(Main.rand.Next(20, 34), 0.7f, Main.rand.NextFloat(-0.08f, 0.08f));
                    }
                }
            }
            Projectile.velocity *= 0.92f;
            int frame = (int)Main.GameUpdateCount + Projectile.whoAmI;
            if (frame % ScanInterval == 0) {
                float radiusSq = radius * radius;
                bool damageTick = Main.GameUpdateCount % 20 == 0;
                int dmg = Math.Max(Projectile.damage / 8, 1);
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.boss) continue;
                    if (Vector2.DistanceSquared(npc.Center, Projectile.Center) > radiusSq) continue;
                    npc.velocity *= 0.92f;
                    if (damageTick) {
                        npc.SimpleStrikeNPC(dmg, 0, false, 0f, DamageClass.Magic, false, 0f, true);
                    }
                }
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile hostile = Main.projectile[i];
                    if (!hostile.active || !hostile.hostile || hostile.friendly) continue;
                    if (Vector2.DistanceSquared(hostile.Center, Projectile.Center) > radiusSq) continue;
                    hostile.velocity *= 0.96f;
                    if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 18 == 0 && Main.rand.NextBool(3)) {
                        PRTLoader.NewParticle<PRT_GammaIonize>(hostile.Center, Vector2.Zero, new Color(255, 200, 110), 0.6f).Configure(16, Main.rand.NextFloat());
                    }
                }
            }
            //身在砂幕，每3帧轻震
            if (frame % 3 == 0) {
                Player local = Main.LocalPlayer;
                if (local != null && local.active && Vector2.DistanceSquared(local.Center, Projectile.Center) < radius * radius) {
                    SHPCNaturalFx.Shake(0.4f);
                }
            }
            //砂烟+火星，6/12帧节流
            if (Main.netMode == NetmodeID.Server) return;
            if (Main.GameUpdateCount % 6 == 0) {
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + Main.rand.NextVector2Circular(radius * 0.8f, radius * 0.6f), Main.rand.NextVector2Circular(2.5f, 1.2f), new Color(225, 190, 110), Main.rand.NextFloat(0.5f, 0.95f)).Configure(Main.rand.Next(28, 50), 0.7f, Main.rand.NextFloat(-0.05f, 0.05f));
            }
            if (Main.GameUpdateCount % 12 == 0) {
                Vector2 vel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(radius * 0.5f, radius * 0.5f), vel, Color.Lerp(new Color(255, 220, 130), new Color(160, 90, 30), Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.8f)).Configure(false, Main.rand.Next(12, 22));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //Cyclone+Airflow 暖沙旋核,外裹真alpha尘幕
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float alpha = VisualEnv;
            float t = (float)Main.timeForVisualEffects * 0.04f;

            //底部大旋涡
            Texture2D cyclone = CWRAsset.Cyclone?.Value;
            if (cyclone != null) {
                Vector2 origin = cyclone.Size() * 0.5f;
                Color c = new Color(220, 185, 110, 0) * alpha * 0.55f;
                Main.spriteBatch.Draw(cyclone, baseScreen, null, c, t * 1.4f, origin, radius / cyclone.Width * 2.4f, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(cyclone, baseScreen, null, c * 0.6f, -t * 0.7f, origin, radius / cyclone.Width * 1.7f, SpriteEffects.None, 0f);
            }
            //4 张 Airflow
            Texture2D airflow = CWRAsset.Airflow?.Value;
            if (airflow != null) {
                Vector2 origin = airflow.Size() * 0.5f;
                const int airflowCount = 4;
                for (int i = 0; i < airflowCount; i++) {
                    float a = i * (MathHelper.TwoPi / airflowCount) + t * (i % 2 == 0 ? 1f : -0.6f);
                    Vector2 offset = a.ToRotationVector2() * radius * 0.5f;
                    Color c = new Color(225, 190, 110, 0) * alpha * 0.4f;
                    Main.spriteBatch.Draw(airflow, baseScreen + offset, null, c,
                        a + t * 0.6f, origin, radius / airflow.Width * 1.5f, SpriteEffects.None, 0f);
                }
            }
            //6 张 Fog 光尘，同种子稳帧,逐层镜像防同贴纸盖三遍
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog != null) {
                Vector2 origin = fog.Size() * 0.5f;
                int seed = Projectile.whoAmI * 7919;
                const int fogCount = 6;
                for (int i = 0; i < fogCount; i++) {
                    float fa = (seed + i * 173) % 360 * MathHelper.Pi / 180f + t * 0.4f;
                    float fr = ((seed + i * 211) % 100) / 100f;
                    Vector2 offset = fa.ToRotationVector2() * radius * (0.3f + fr * 0.7f);
                    Color c = new Color(225, 195, 130, 0) * alpha * 0.35f;
                    SpriteEffects fx = ((seed >> i) & 1) == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    Main.spriteBatch.Draw(fog, baseScreen + offset, null, c,
                        fa + t * 0.3f * (i % 3 - 1), origin, 0.5f + fr * 0.7f, fx, 0f);
                }
                //尘幕本体,真alpha暖沙裹住光尘,带暗缘沉底;沙是遮蔽介质不是发光气体
                const int dustCount = 5;
                for (int i = 0; i < dustCount; i++) {
                    float da = (seed + i * 251) % 360 * MathHelper.Pi / 180f + t * (0.28f * ((i % 2) * 2 - 1));
                    float dr = ((seed + i * 137) % 100) / 100f;
                    Vector2 offset = da.ToRotationVector2() * radius * (0.35f + dr * 0.55f);
                    float scale = 0.55f + dr * 0.5f;
                    float rot = da + t * 0.35f * (i % 3 - 1);
                    SpriteEffects fx = ((seed >> (i + 2)) & 1) == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                    //暗缘先落,主体压上,读出尘瓣厚度
                    Main.spriteBatch.Draw(fog, baseScreen + offset + new Vector2(2f, 3f), null,
                        new Color(118, 86, 50) * (alpha * 0.3f), rot, origin, scale * 1.04f, fx, 0f);
                    Main.spriteBatch.Draw(fog, baseScreen + offset, null,
                        new Color(199, 162, 100) * (alpha * 0.42f), rot, origin, scale, fx, 0f);
                }
            }
            return false;
        }

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //暖色发光中心;真加色批 tint 必须带 A,A=0 什么都画不出
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) return;
            Vector2 baseScreen = Projectile.Center - Main.screenPosition;
            float env = VisualEnv;
            Color inner = new Color(255, 200, 130) * (env * 0.4f);
            Color outer = new Color(160, 90, 30) * (env * 0.2f);
            SHPCNaturalFx.GlowLayered(spriteBatch, glow, baseScreen, inner, outer, radius / 32f * 0.8f, 0f, 3);
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.35f, Pitch = -0.4f }, Projectile.Center);
            //散逸拍,沙瓣外抛+粒闪余韵
            for (int i = 0; i < 7; i++) {
                float a = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 vel = a.ToRotationVector2() * Main.rand.NextFloat(1.8f, 4f);
                PRTLoader.NewParticle<PRT_Smoke>(Projectile.Center + a.ToRotationVector2() * radius * 0.5f, vel,
                    new Color(215, 180, 105), Main.rand.NextFloat(0.5f, 1f))
                    .Configure(Main.rand.Next(24, 44), 0.6f, Main.rand.NextFloat(-0.08f, 0.08f));
            }
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(3.5f, 2.5f);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + Main.rand.NextVector2Circular(radius * 0.6f, radius * 0.4f),
                    vel, Color.Lerp(new Color(255, 220, 130), new Color(160, 90, 30), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.4f, 0.75f)).Configure(true, Main.rand.Next(14, 26));
            }
        }
    }
}
