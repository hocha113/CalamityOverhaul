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

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Power
{
    /// <summary>等离子注入器，球爆留残阳灼烧+日珥追打</summary>
    internal sealed class PlasmaInjectorModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Power;
        //等离子注入粉紫
        public override Color TintColor => new(255, 100, 220);

        public override void Apply(ref ShootContext ctx) {
            ctx.OrbSpeedMul += 0.3f;
            ctx.ChargeTimeMul += -0.1f;
            ctx.ManaCostMul += 0.3f;
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            //同屏只留一颗残阳，旧的 Kill
            int sunType = ModContent.ProjectileType<SHPCPlasmaSunProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == orb.Projectile.owner && p.type == sunType) {
                    p.Kill();
                }
            }
            int dmg = Math.Max((int)(orb.Projectile.damage * 0.35f), 1);
            Projectile.NewProjectile(orb.Projectile.GetSource_FromThis(),
                orb.Projectile.Center, Vector2.Zero, sunType,
                dmg, 2f, orb.Projectile.owner);
        }
    }

    /// <summary>等离子残阳，半径 110 灼烧+日珥；SHPCPlasmaSun.fx</summary>
    internal sealed class SHPCPlasmaSunProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 300;
        private const float AuraRadius = 110f;
        private const int FlareInterval = 45;
        private const float FlareRange = 480f;

        private static readonly Color SunCore = new(255, 230, 180);
        private static readonly Color SunSurface = new(255, 85, 45);
        private static readonly Color SunCorona = new(185, 15, 35);

        private float fadeAlpha;
        private int flareTimer;

        public override void SetDefaults() {
            Projectile.width = (int)(AuraRadius * 2f);
            Projectile.height = (int)(AuraRadius * 2f);
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int age = Lifetime - Projectile.timeLeft;
            if (age == 0 && Main.netMode != NetmodeID.Server) {
                SoundStyle igniteSound = "CalamityMod/Sounds/Item/FlareSound".GetSound(SoundID.Item74);
                SoundEngine.PlaySound(igniteSound with { Volume = 0.8f, Pitch = -0.35f }, Projectile.Center);
            }

            //诞生膨胀 / 临终塌缩
            fadeAlpha = MathHelper.Clamp(age / 18f, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / 35f, 0f, 1f);

            Lighting.AddLight(Projectile.Center, SunSurface.ToVector3() * 1.4f * fadeAlpha);

            //表面等离子余烬
            if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 3 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 surfacePos = Projectile.Center + ang.ToRotationVector2() * Main.rand.NextFloat(48f, 66f) * fadeAlpha;
                PRTLoader.NewParticle<PRT_LavaFire>(surfacePos,
                    ang.ToRotationVector2() * Main.rand.NextFloat(0.4f, 1.6f) - Vector2.UnitY * 0.8f,
                    Color.White, Main.rand.NextFloat(0.5f, 1.0f))?.SetLifetime(20, 40);
            }

            //日珥火舌周期喷出
            flareTimer++;
            if (flareTimer >= FlareInterval && fadeAlpha > 0.6f) {
                flareTimer = 0;
                if (Projectile.owner == Main.myPlayer) {
                    NPC target = Projectile.Center.FindClosestNPC(FlareRange, false, true);
                    if (target != null) {
                        Vector2 dir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
                        int dmg = Math.Max((int)(Projectile.damage * 2.2f), 1);
                        //出膛音改由日珥弹幕首帧自播，旁观端同样可闻
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                            Projectile.Center + dir * 60f, dir * 15f,
                            ModContent.ProjectileType<SHPCSolarFlareProj>(),
                            dmg, 3f, Projectile.owner);
                    }
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float dist = Vector2.Distance(Projectile.Center, targetHitbox.Center.ToVector2());
            return dist < AuraRadius * fadeAlpha;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (target.IsWormBody()) {
                modifiers.FinalDamage *= 0.75f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer) {
                target.AddBuff(BuffID.OnFire3, 120);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
            //塌缩回吸，余烬自外缘向心坠入
            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f);
                PRTLoader.NewParticle<PRT_LavaFire>(Projectile.Center + vel * 6f, -vel * 0.5f,
                    Color.White, Main.rand.NextFloat(0.7f, 1.3f))?.SetLifetime(30, 60);
            }
            //加色批A=0不可见，A须随强度走
            PRTLoader.NewParticle<PRT_StarPulseRing>(Projectile.Center, Vector2.Zero, new Color(255, 90, 50), 0.05f).Configure(0.05f, 0.7f, 26);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.SHPCPlasmaSun?.Value;
            Texture2D canvas = VaultAsset.placeholder2?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (shader == null || canvas == null || noise == null) return false;

            shader.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.03f);
            shader.Parameters["fadeAlpha"]?.SetValue(fadeAlpha);
            shader.Parameters["sunRadius"]?.SetValue(0.34f);
            shader.Parameters["coreColor"]?.SetValue(SunCore.ToVector3());
            shader.Parameters["surfaceColor"]?.SetValue(SunSurface.ToVector3());
            shader.Parameters["coronaColor"]?.SetValue(SunCorona.ToVector3());

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float drawSize = 400f * (0.4f + 0.6f * fadeAlpha);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive,
                SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Main.spriteBatch.Draw(canvas, drawPos, null, Color.White,
                0f, canvas.Size() * 0.5f, new Vector2(drawSize, drawSize),
                SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    /// <summary>日珥火舌，轻追目标，命中点燃</summary>
    internal sealed class SHPCSolarFlareProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.extraUpdates = 1;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Main.netMode != NetmodeID.Server) {
                    SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
                }
            }
            //轻追
            NPC target = Projectile.Center.FindClosestNPC(420f, false, true);
            if (target != null) {
                Vector2 desired = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 15f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.045f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, new Vector3(1.1f, 0.45f, 0.18f));

            if (Main.netMode != NetmodeID.Server) {
                PRTLoader.NewParticle<PRT_LavaFire>(
                    Projectile.Center + Main.rand.NextVector2Circular(5f, 5f),
                    -Projectile.velocity * 0.18f,
                    Color.White, Main.rand.NextFloat(0.5f, 0.9f))?.SetLifetime(14, 26);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.owner == Main.myPlayer) {
                target.AddBuff(BuffID.OnFire3, 180);
            }
            if (Main.netMode == NetmodeID.Server) return;
            SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.5f, Pitch = 0.1f }, target.Center);
            for (int i = 0; i < 8; i++) {
                PRT_HellFlame hf = new() {
                    Position = target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Velocity = Main.rand.NextVector2CircularEdge(3.5f, 3.5f),
                    Scale = Main.rand.NextFloat(0.6f, 1.1f),
                };
                PRTLoader.AddParticle(hf);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            //此批为真Additive（源因子=SourceAlpha），A=0整层消失，A须随强度走
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D shot = CWRAsset.LightShot?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speedT = MathHelper.Clamp(Projectile.velocity.Length() / 15f, 0.4f, 1.2f);
            //取velocity朝向，防首帧AI未跑时rotation为0横闪
            float rot = Projectile.velocity.ToRotation();
            if (glow != null) {
                //火舌沿速度拉伸，外暗红/中橙/芯暖金
                Vector2 origin = glow.Size() * 0.5f;
                spriteBatch.Draw(glow, drawPos, null, new Color(185, 25, 40) * 0.5f, rot,
                    origin, new Vector2(1.5f * speedT, 0.85f), SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, drawPos, null, new Color(255, 130, 60) * 0.8f, rot,
                    origin, new Vector2(0.85f * speedT, 0.45f), SpriteEffects.None, 0f);
                spriteBatch.Draw(glow, drawPos, null, new Color(255, 230, 180) * 0.9f, rot,
                    origin, new Vector2(0.42f * speedT, 0.22f), SpriteEffects.None, 0f);
            }
            if (shot != null) {
                //彗尾反向
                Vector2 origin = new(shot.Width, shot.Height * 0.5f);
                spriteBatch.Draw(shot, drawPos, null, new Color(255, 110, 50) * 0.7f,
                    rot, origin, new Vector2(0.55f * speedT, 0.3f), SpriteEffects.None, 0f);
            }
        }
    }
}
