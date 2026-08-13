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

namespace CalamityOverhaul.Content.LegendWeapon.SHPCLegend.Modules.Grip
{
    /// <summary>谐振握把，命中溢雾，拾取回蓝叠谐鸣（最多5层）</summary>
    internal sealed class HarmonyGripModule : SHPCModuleItem
    {
        public override SHPCSlotCategory SlotCategory => SHPCSlotCategory.Grip;
        //节能薄荷绿
        public override Color TintColor => new(120, 255, 180);

        private const int MaxResonance = 5;
        /// <summary>谐鸣层数</summary>
        internal int ResonanceStacks;
        /// <summary>层保持计时，归零后衰减</summary>
        internal int ResonanceTimer;
        private float _resonanceCarry;

        public override void Apply(ref ShootContext ctx) {
            ctx.ManaCostMul += -0.2f;
            if (ResonanceStacks > 0) {
                ctx.AttackSpeedMul += ResonanceStacks * 0.03f;
            }
        }

        /// <summary>拾雾叠层并刷新计时</summary>
        internal void AddResonance() {
            ResonanceStacks = Math.Min(ResonanceStacks + 1, MaxResonance);
            ResonanceTimer = 300;
            _resonanceCarry = 0f;
        }

        public override void OnBeamHitNPC(CyberTraceBeamProj beam, NPC target, NPC.HitInfo hit, int damageDone) {
            if (beam.Projectile.owner != Main.myPlayer || beam.IsDerived) return;
            if (!Main.rand.NextBool(2, 5)) return; //40% 概率溢流
            SpawnWisp(beam.Projectile, target.Center);
        }

        public override void OnLaserHitNPC(CyberPrismLaserProj laser, NPC target, NPC.HitInfo hit, int damageDone) {
            if (laser.Projectile.owner != Main.myPlayer) return;
            if (!Main.rand.NextBool(7)) return; //激光命中频繁，节流
            SpawnWisp(laser.Projectile, target.Center);
        }

        public override void OnOrbDetonation(CyberChargeOrbProj orb) {
            if (orb.Projectile.owner != Main.myPlayer) return;
            for (int i = 0; i < 3; i++) {
                SpawnWisp(orb.Projectile, orb.Projectile.Center + Main.rand.NextVector2Circular(40f, 40f));
            }
        }

        private static void SpawnWisp(Projectile source, Vector2 pos) {
            Projectile.NewProjectile(source.GetSource_FromThis(),
                pos, Main.rand.NextVector2CircularEdge(3f, 3f) - Vector2.UnitY * 1.5f,
                ModContent.ProjectileType<SHPCHarmonyWispProj>(),
                0, 0f, source.owner);
        }

        public override void OnPlayerUpdate(Player player) {
            if (ResonanceStacks <= 0) return;
            if (ResonanceTimer > 0) {
                TickDown(ref ResonanceTimer, ref _resonanceCarry);
                //满层指尖电雾
                if (ResonanceStacks >= MaxResonance && Main.netMode != NetmodeID.Server && Main.rand.NextBool(6)) {
                    Vector2 pos = player.Center + Main.rand.NextVector2Circular(20f, 26f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f),
                        new Color(140, 255, 190), Main.rand.NextFloat(0.4f, 0.8f)).Configure(new Color(40, 170, 110), Main.rand.Next(10, 20));
                }
                return;
            }
            ResonanceStacks--;
            ResonanceTimer = 60;
            _resonanceCarry = 0f;
        }
    }

    /// <summary>谐振灵雾，漂散后追随，触碰回蓝叠层</summary>
    internal sealed class SHPCHarmonyWispProj : ModProjectile, IAdditiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Lifetime = 420;
        private const int DriftPhase = 30;
        private const int GhostCount = 5;
        private const float GhostSpacing = 6f;
        private static readonly Color WispCore = new(190, 255, 220);
        private static readonly Color WispGlow = new(90, 230, 160);
        private static readonly Color WispAura = new(25, 120, 80);

        private float fadeAlpha;
        /// <summary>残影位置环，头=最近</summary>
        private Vector2[] ghostPos;
        private int ghostFilled;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (owner == null || !owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            int age = Lifetime - Projectile.timeLeft;
            fadeAlpha = MathHelper.Clamp(age / 12f, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / 30f, 0f, 1f);

            if (age < DriftPhase) {
                //漂散
                Projectile.velocity *= 0.93f;
            }
            else {
                //追随
                Vector2 toOwner = owner.Center - Projectile.Center;
                float dist = toOwner.Length();
                float chase = MathHelper.Clamp(MathHelper.Lerp(0.18f, 0.55f, 1f - dist / 600f), 0.18f, 0.55f);
                Vector2 desired = toOwner.SafeNormalize(Vector2.Zero) * MathHelper.Clamp(3f + (600f - dist) * 0.02f, 3f, 13f);
                desired += toOwner.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2)
                    * MathF.Sin(age * 0.12f + Projectile.whoAmI) * 1.2f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, chase * 0.12f);

                //仅owner端结算
                if (dist < 42f) {
                    Collect(owner);
                    return;
                }
            }

            //残影环采样
            ghostPos ??= new Vector2[GhostCount];
            if (ghostFilled == 0) {
                ghostPos[0] = Projectile.Center;
                ghostFilled = 1;
            }
            else if (Vector2.DistanceSquared(Projectile.Center, ghostPos[0]) >= GhostSpacing * GhostSpacing) {
                Array.Copy(ghostPos, 0, ghostPos, 1, Math.Min(ghostFilled, GhostCount - 1));
                ghostPos[0] = Projectile.Center;
                if (ghostFilled < GhostCount) ghostFilled++;
            }

            Lighting.AddLight(Projectile.Center, WispGlow.ToVector3() * 0.35f * fadeAlpha);
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_CyberSquare>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.15f - Vector2.UnitY * 0.4f,
                    WispCore, Main.rand.NextFloat(0.3f, 0.7f)).Configure(WispAura, Main.rand.Next(12, 22));
            }
        }

        private void Collect(Player owner) {
            if (Projectile.owner == Main.myPlayer) {
                int mana = Main.rand.Next(5, 9);
                owner.statMana = Math.Min(owner.statMana + mana, owner.statManaMax2);
                owner.ManaEffect(mana);
                SHPCModificationSystem.ForEachModule(owner, mod => {
                    if (mod is HarmonyGripModule grip) grip.AddResonance();
                });
            }
            if (Main.netMode != NetmodeID.Server) {
                SoundEngine.PlaySound(SoundID.Item112 with { Volume = 0.35f, Pitch = 0.65f }, Projectile.Center);
                for (int i = 0; i < 7; i++) {
                    Vector2 vel = Main.rand.NextVector2CircularEdge(2.6f, 2.6f);
                    PRTLoader.NewParticle<PRT_CyberSquare>(Projectile.Center, vel, WispCore, Main.rand.NextFloat(0.45f, 0.9f)).Configure(WispGlow, Main.rand.Next(12, 22));
                }
            }
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (fadeAlpha < 0.01f) return;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture_White?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (glow == null || star == null) return;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 glowOrigin = glow.Size() * 0.5f;
            Vector2 starOrigin = star.Size() * 0.5f;
            float t = (float)Main.timeForVisualEffects;
            float pulse = 0.85f + 0.15f * MathF.Sin(t * 0.18f + Projectile.whoAmI * 1.7f);
            float bodyRot = t * 0.05f + Projectile.whoAmI;
            float speed = Projectile.velocity.Length();
            float speedT = MathHelper.Clamp(speed / 11f, 0f, 1f);

            //速度残影链，越快越显
            if (ghostPos != null && speedT > 0.06f) {
                for (int i = ghostFilled - 1; i >= 0; i--) {
                    float k = 1f - (i + 1f) / (GhostCount + 1f);
                    Vector2 gp = ghostPos[i] - Main.screenPosition;
                    spriteBatch.Draw(star, gp, null, WispGlow * (fadeAlpha * speedT * 0.4f * k),
                        bodyRot - (i + 1) * 0.24f, starOrigin, 0.026f + 0.02f * k, SpriteEffects.None, 0f);
                }
            }

            //速度拉伸条，锚在尾侧
            if (pixel != null && speedT > 0.1f) {
                float len = 7f + speed * 2.1f;
                spriteBatch.Draw(pixel, drawPos, new Rectangle(0, 0, 1, 1), WispGlow * (fadeAlpha * speedT * 0.5f),
                    Projectile.velocity.ToRotation(), new Vector2(1f, 0.5f), new Vector2(len, 2.4f), SpriteEffects.None, 0f);
            }

            //单层底晕，体量压在30%以下
            spriteBatch.Draw(glow, drawPos, null, WispAura * (fadeAlpha * 0.5f * pulse), 0f, glowOrigin, 0.9f, SpriteEffects.None, 0f);

            //四芒星芯体，呼吸+慢旋
            spriteBatch.Draw(star, drawPos, null, WispGlow * (fadeAlpha * 0.85f * pulse),
                bodyRot, starOrigin, 0.085f * pulse, SpriteEffects.None, 0f);
            spriteBatch.Draw(star, drawPos, null, WispCore * (fadeAlpha * pulse),
                -bodyRot * 0.6f, starOrigin, 0.048f * pulse, SpriteEffects.None, 0f);

            //双卫星微粒相位环绕
            if (pixel != null) {
                for (int i = 0; i < 2; i++) {
                    float ang = t * 0.11f + Projectile.whoAmI * 2.1f + i * MathHelper.Pi;
                    float radius = 11f + 2.5f * MathF.Sin(t * 0.07f + i * 1.3f);
                    Vector2 sat = drawPos + ang.ToRotationVector2() * radius;
                    spriteBatch.Draw(pixel, sat, new Rectangle(0, 0, 1, 1), WispCore * (fadeAlpha * 0.8f),
                        ang + MathHelper.PiOver2, new Vector2(0.5f, 0.5f), new Vector2(4f, 1.8f), SpriteEffects.None, 0f);
                }
            }
        }
    }
}
