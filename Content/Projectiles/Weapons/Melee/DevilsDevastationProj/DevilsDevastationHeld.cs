using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DevilsDevastationProj
{
    internal class DevilsDevastationHeld : BaseSwing
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DevilsDevastation";
        public override string GlowTexture => CWRConstant.Masking + "SplitTrail";
        public override string gradientTexturePath => CWRConstant.ColorBar + "DevilsDevastation_Bar";
        public override void SetSwingProperty() {
            drawTrailHighlight = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = 122;
            Projectile.height = 122;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 4;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            distanceToOwner = 34;
            drawTrailTopWidth = 10;
            canDrawSlashTrail = true;
            ownerOrientationLock = true;
            Length = 120;
        }

        public override void Shoot() {
            if (Projectile.ai[0] == 2 || Projectile.ai[0] == 1) {
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Source, Owner.Center, UnitToMouseV.RotatedBy((-1 + i) * 0.1f) * 6
                        , ModContent.ProjectileType<EXOathblade>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI, 1);
                }
                return;
            }
            if (Projectile.ai[0] == 3) {
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = 0.82f, Volume = 2.2f }, Projectile.Center);
                float lengs = ToMouse.Length();
                if (lengs < Length * Projectile.scale) {
                    lengs = Length * Projectile.scale;
                }
                Vector2 targetPos = Owner.GetPlayerStabilityCenter() + ToMouse.UnitVector() * lengs;
                Vector2 unitToM = UnitToMouseV;
                for (int i = 0; i < lengs / 12; i++) {
                    Vector2 spwanPos = Owner.GetPlayerStabilityCenter() + unitToM * (1 + i) * 12;
                    Dust dust = Dust.NewDustPerfect(spwanPos, DustID.Blood, UnitToMouseV * 6, 125, Color.OrangeRed, 3);
                    dust.noGravity = true;
                    dust.scale = 5;
                }
                int bloodQuantity = 10;
                for (int i = 0; i < bloodQuantity; i++) {
                    Vector2 target = ((float)Math.PI * 2 / bloodQuantity * i + 1).ToRotationVector2() * 300;
                    int bloodQuantity2 = 150;
                    for (int a = 1; a <= bloodQuantity2; a++) {
                        Dust.NewDust(InMousePos, 0, 0, DustID.Blood, target.X / a, target.Y / a, 0, default, 3);
                    }
                }
                for (int i = 0; i < 6; i++) {
                    Projectile.NewProjectile(Source, InMousePos, (MathHelper.TwoPi / 6 * i).ToRotationVector2() * 3
                    , ModContent.ProjectileType<EXDemonBlastAlt>(), Projectile.damage * 5, Projectile.knockBack, Owner.whoAmI);
                }
                return;
            }
            int num = 0;
            foreach (var proj in Main.projectile) {
                if (!proj.active) {
                    continue;
                }
                if (proj.type != ModContent.ProjectileType<EXDemonBlast>()) {
                    continue;
                }
                if (proj.ai[0] == 0) {
                    num++;
                }
            }
            if (num >= 16) {
                return;
            }
            Projectile.NewProjectile(Source, Owner.Center, UnitToMouseV * 6
                , ModContent.ProjectileType<EXDemonBlast>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI);
        }

        public override void SwingAI() {
            if (Time == 0) {
                SoundEngine.PlaySound(SoundID.Item71, Owner.position);
            }

            if (Projectile.ai[0] == 1) {
                SwingBehavior(starArg: 63, baseSwingSpeed: 6, ler1_UpLengthSengs: 0.1f, ler1_UpSpeedSengs: 0.1f, ler1_UpSizeSengs: 0.062f
                , ler2_DownLengthSengs: 0.01f, ler2_DownSpeedSengs: 0.14f, ler2_DownSizeSengs: 0
                , minClampLength: 160, maxClampLength: 220, ler1Time: 18, maxSwingTime: 30);
                return;
            }
            else if (Projectile.ai[0] == 2) {
                SwingBehavior(starArg: 63, baseSwingSpeed: -6, ler1_UpLengthSengs: 0.1f, ler1_UpSpeedSengs: 0.1f, ler1_UpSizeSengs: 0.062f
                , ler2_DownLengthSengs: 0.01f, ler2_DownSpeedSengs: 0.14f, ler2_DownSizeSengs: 0
                , minClampLength: 160, maxClampLength: 220, ler1Time: 18, maxSwingTime: 30);
                return;
            }
            else if (Projectile.ai[0] == 3) {
                shootSengs = 0.95f;
                maxSwingTime = 70;
                canDrawSlashTrail = false;
                SwingBehavior(starArg: 13, baseSwingSpeed: 2, ler1_UpLengthSengs: 0.1f, ler1_UpSpeedSengs: 0.1f, ler1_UpSizeSengs: 0.062f
                , ler2_DownLengthSengs: 0.01f, ler2_DownSpeedSengs: 0.14f, ler2_DownSizeSengs: 0
                , minClampLength: 160, maxClampLength: 200, ler1Time: 8, maxSwingTime: 60);
                return;
            }

            SwingBehavior(starArg: 63, baseSwingSpeed: 6, ler1_UpLengthSengs: 0.1f, ler1_UpSpeedSengs: 0.1f, ler1_UpSizeSengs: 0.022f
                , ler2_DownLengthSengs: 0.01f, ler2_DownSpeedSengs: 0.14f, ler2_DownSizeSengs: 0
                , minClampLength: 0, maxClampLength: 0, ler1Time: 8, maxSwingTime: 20);
        }

        private void HitEffect(Entity target, bool theofSteel) {
            HitEffectValue(target, 13, out Vector2 rotToTargetSpeedTrengsVumVer, out int sparkCount);
            for (int i = 0; i < sparkCount; i++) {
                Vector2 sparkVelocity2 = rotToTargetSpeedTrengsVumVer.RotatedByRandom(0.35f) * Main.rand.NextFloat(0.3f, 1.6f);
                int sparkLifetime2 = Main.rand.Next(18, 30);
                float sparkScale2 = Main.rand.NextFloat(0.65f, 1.2f);
                Color sparkColor2 = Main.rand.NextBool(3) ? Color.OrangeRed : Color.DarkRed;
                if (theofSteel) {
                    sparkColor2 = Main.rand.NextBool(3) ? Color.Gold : Color.Goldenrod;
                }

                PRT_Spark spark = new PRT_Spark(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f
                        , target.height * 0.5f) + Projectile.velocity * 1.2f, sparkVelocity2 * 1f
                        , false, (int)(sparkLifetime2 * 1.2f), sparkScale2 * 1.4f, sparkColor2);
                PRTLoader.AddParticle(spark);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            HitEffect(target, CWRLoad.NPCValue.TheofSteel[target.type]);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            HitEffect(target, false);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) {
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 toOwner = Projectile.Center - Owner.GetPlayerStabilityCenter();
            Vector2 offsetOwnerPos = toOwner.GetNormalVector() * -6 * Projectile.spriteDirection;
            Vector2 pos = Projectile.Center - RodingToVer(48, toOwner.ToRotation()) + offsetOwnerPos;
            Vector2 drawPos = pos - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY;

            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }
            if (Projectile.ai[0] != 0) {
                CWRUtils.DrawMarginEffect(Main.spriteBatch, texture, Projectile.timeLeft, drawPos
                , null, Color.Red, drawRoting, drawOrigin, Projectile.scale, effects);
            }

            Main.EntitySpriteDraw(texture, drawPos, new Rectangle?(rect), Color.White
                , drawRoting, drawOrigin, Projectile.scale * MeleeSize, effects, 0);
        }
    }
}
