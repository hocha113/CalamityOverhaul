using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RemakeItems.Melee;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class DragonRageHeld : BaseSwing
    {
        public override int TargetID => ModContent.ItemType<DragonRage>();
        public override string Texture => CWRConstant.Cay_Proj_Melee + "DragonRageStaff";
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "DragonRage_Bar";
        private int Time2;
        public override void SetSwingProperty() {
            Projectile.CloneDefaults(ProjectileID.Spear);
            Projectile.aiStyle = AIType = ProjectileID.None;
            Projectile.extraUpdates = 3;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            distanceToOwner = 125;
            drawTrailTopWidth = 90;
            Length = 80;
        }

        public override void SwingAI() {
            float speedUp = 1 / Owner.GetAttackSpeed(DamageClass.Melee);
            if (Projectile.ai[0] == 0) {
                if (Time == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6) / speedUp;
                }

                if (Time < 10 * speedUp) {
                    Length *= 1 + 0.1f / UpdateRate;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / UpdateRate;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                else {
                    Length *= 1 - 0.01f / UpdateRate;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / UpdateRate / speedUp;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                if (Time >= 22 * UpdateRate * speedUp) {
                    Projectile.Kill();
                }
                if (Time % UpdateRate == UpdateRate - 1) {
                    Length = MathHelper.Clamp(Length, 120, 160);
                }
            }
            else if (Projectile.ai[0] == 1) {
                if (Time == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() + MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6) / speedUp;
                }

                if (Time < 10 * speedUp) {
                    Length *= 1 + 0.1f / UpdateRate;
                    Rotation -= speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / UpdateRate;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                else {
                    Length *= 1 - 0.01f / UpdateRate;
                    Rotation -= speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / UpdateRate / speedUp;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                if (Time >= 22 * UpdateRate * speedUp) {
                    Projectile.Kill();
                }
                if (Time % UpdateRate == UpdateRate - 1) {
                    Length = MathHelper.Clamp(Length, 110, 120);
                }
            }
            else if (Projectile.ai[0] == 2) {
                if (Time == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6) / speedUp;
                }

                if (Time < 10 * speedUp) {
                    Length *= 1 + 0.11f / UpdateRate;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.3f / UpdateRate;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                else {
                    Length *= 1 - 0.01f / UpdateRate;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 - (0.13f * speedUp * 1.15f) / UpdateRate / speedUp;//它今儿个非得转过来不可
                    vector = startVector.RotatedBy(Rotation) * Length;
                }

                if (Time >= 26 * UpdateRate * speedUp) {
                    Projectile.Kill();
                }
                if (Time % UpdateRate == UpdateRate - 1) {
                    Length = MathHelper.Clamp(Length, 60, 120);
                }
            }
            else if (Projectile.ai[0] == 3) {
                if (Time == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation());
                    speed = 1 + 0.6f / UpdateRate;
                }

                if (Time < 6 * UpdateRate) {
                    Vector2 position = Projectile.Center + startVector * Projectile.scale;
                    Dust dust = Main.dust[Dust.NewDust(Owner.position, Owner.width, Owner.height, DustID.CopperCoin)];
                    dust.position = position;
                    dust.velocity = Projectile.velocity.RotatedBy(1.57) * 0.33f + Projectile.velocity / 4f * Projectile.scale;
                    dust.position += Projectile.velocity.RotatedBy(1.57);
                    dust.scale = Projectile.scale * 3;
                    dust.fadeIn = 0.5f;
                    dust.noGravity = true;

                    dust = Main.dust[Dust.NewDust(Owner.position, Owner.width, Owner.height, DustID.CopperCoin)];
                    dust.position = position;
                    dust.velocity = Projectile.velocity.RotatedBy(-1.57) * 0.33f + Projectile.velocity / 4f * Projectile.scale;
                    dust.position += Projectile.velocity.RotatedBy(-1.57);
                    dust.scale = Projectile.scale * 3;
                    dust.fadeIn = 0.5f;
                    dust.noGravity = true;

                    Vector2 spanSparkPos = Projectile.Center + Projectile.velocity.UnitVector() * Length;
                    BasePRT spark = new PRT_Spark(spanSparkPos, Projectile.velocity, false, 6, 4.26f, Color.Gold, Owner);
                    PRTLoader.AddParticle(spark);
                }

                Length *= speed;
                vector = startVector * Length;
                speed -= 0.015f / UpdateRate;

                if (Time >= 26 * UpdateRate) {
                    Projectile.Kill();
                }
                float toTargetSengs = Projectile.Center.To(Owner.Center).Length();
                Projectile.scale = 0.8f + toTargetSengs / 520f;
                if (Time % UpdateRate == UpdateRate - 1) {
                    Length = MathHelper.Clamp(Length, 30, 260);
                }
            }
            else if (Projectile.ai[0] == 4) {
                if (Time == 0) {
                    distanceToOwner = 105;
                    drawTrailTopWidth = 190;
                    InitializeCaches();
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6) / speedUp;
                    Rotation = MathHelper.ToRadians(-30 * Projectile.spriteDirection);
                }

                if (Time < 10 * speedUp) {
                    Length *= 1 + 0.1f / UpdateRate;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / UpdateRate;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    Projectile.scale += 0.03f;
                }
                else {
                    Length *= 1 - 0.01f / UpdateRate;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / UpdateRate / speedUp;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    if (Time >= 20 * UpdateRate) {
                        Projectile.scale -= 0.001f;
                    }
                }
                if (Time >= 22 * UpdateRate * speedUp) {
                    Projectile.Kill();
                }
                if (Time % UpdateRate == UpdateRate - 1) {
                    Length = MathHelper.Clamp(Length, 120, 260);
                }
            }
            else if (Projectile.ai[0] == 5) {
                if (Time == 0) {
                    distanceToOwner = 105;
                    drawTrailTopWidth = 190;
                    InitializeCaches();
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6) / speedUp;
                    Rotation = MathHelper.ToRadians(-110 * Projectile.spriteDirection);
                }

                if (Time < 10 * speedUp) {
                    Length *= 1 + 0.1f / UpdateRate;
                    Rotation -= speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / UpdateRate;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    Projectile.scale += 0.03f;
                }
                else {
                    Length *= 1 - 0.01f / UpdateRate;
                    Rotation -= speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / UpdateRate / speedUp;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    if (Time >= 20 * UpdateRate) {
                        Projectile.scale -= 0.001f;
                    }
                }
                if (Time >= 22 * UpdateRate * speedUp) {
                    Projectile.Kill();
                }
                if (Time % UpdateRate == UpdateRate - 1) {
                    Length = MathHelper.Clamp(Length, 120, 260);
                }
            }
            else if (Projectile.ai[0] == 6) {
                canFormOwnerSetDir = false;
                canSetOwnerArmBver = false;
                if (Time == 0) {
                    distanceToOwner = 155;
                    drawTrailTopWidth = 60;
                    InitializeCaches();
                    Projectile.spriteDirection = Owner.direction;
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                }

                if (Time < 10) {
                    Length *= 1 + 0.11f / UpdateRate;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.3f / UpdateRate;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    Projectile.scale += 0.011f;
                }
                else {
                    Rotation += speed * Projectile.spriteDirection;
                    if (!DownRight) {
                        speed *= 1 - 0.01f / UpdateRate;
                        if (Time >= 60 * UpdateRate) {
                            Length *= 1 - 0.01f / UpdateRate;
                            Projectile.scale -= 0.001f;
                        }
                    }
                    else {
                        if (Time > 30 * UpdateRate) {
                            Time = 30 * UpdateRate;
                        }
                        if (Projectile.soundDelay <= 0) {
                            SoundEngine.PlaySound(SupremeCalamitas.CatastropheSwing with { MaxInstances = 6, Volume = 0.45f }, Owner.Center);
                            Projectile.soundDelay = 30 * UpdateRate;
                        }
                    }

                    Owner.ChangeDir(Math.Sign(ToMouse.X));
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter
                        , Owner.direction < 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi + MathHelper.PiOver2);

                    vector = startVector.RotatedBy(Rotation) * Length;
                    if (Time % UpdateRate == 0) {
                        SpawnDust(Owner, Owner.direction);
                    }
                }
                Projectile.timeLeft = 1200;
                if (Time >= 90 * UpdateRate && !DownRight || (Math.Abs(rotSpeed) <= 0.06f && Time2 >= 90 * UpdateRate)) {
                    Projectile.Kill();
                }
                if (Time % UpdateRate == UpdateRate - 1) {
                    Length = MathHelper.Clamp(Length, 60, 220);
                }
            }

            if (Time > 1) {
                Projectile.alpha = 0;
            }
            Time2++;
            canDrawSlashTrail = Projectile.ai[0] != 3;
            inDrawFlipdiagonally = Projectile.ai[0] == 1 || Projectile.ai[0] == 5;
        }

        private void SpawnDust(Player player, int direction) {
            float rot = Projectile.rotation - MathF.PI / 4f * direction;
            Vector2 vector = Projectile.Center + (rot + (direction == -1 ? MathF.PI : 0f)).ToRotationVector2() * 200 * Projectile.scale;
            Vector2 vector2 = rot.ToRotationVector2();
            Vector2 vector3 = vector2.RotatedBy(MathF.PI / 2f * Projectile.spriteDirection);
            if (Main.rand.NextBool()) {
                Dust dust = Dust.NewDustDirect(vector - new Vector2(5f), 10, 10, DustID.CopperCoin, player.velocity.X, player.velocity.Y, 150);
                dust.velocity = Projectile.SafeDirectionTo(dust.position) * 0.1f + dust.velocity * 0.1f;
            }

            for (int i = 0; i < 4; i++) {
                float speedRands = 1f;
                float modeRands = 1f;
                switch (i) {
                    case 1:
                        modeRands = -1f;
                        break;
                    case 2:
                        modeRands = 1.25f;
                        speedRands = 0.5f;
                        break;
                    case 3:
                        modeRands = -1.25f;
                        speedRands = 0.5f;
                        break;
                }

                if (!Main.rand.NextBool(6)) {
                    Dust dust2 = Dust.NewDustDirect(Projectile.position, 0, 0, DustID.CopperCoin, 0f, 0f, 100);
                    dust2.position = Projectile.Center + vector2 * (180 * Projectile.scale + Main.rand.NextFloat() * 20f) * modeRands;
                    dust2.velocity = vector3 * (4f + 4f * Main.rand.NextFloat()) * modeRands * speedRands;
                    dust2.noGravity = true;
                    dust2.noLight = true;
                    dust2.scale = 0.5f;
                    if (Main.rand.NextBool(4)) {
                        dust2.noGravity = false;
                    }
                }
            }
        }

        //模拟出一个勉强符合物理逻辑的命中粒子效果，最好不要动这些，这个效果是我凑出来的，我也不清楚这具体的数学逻辑，代码太乱了
        private void HitEffect(Entity target, bool theofSteel) {
            if (target.Distance(Owner.Center) < Owner.width * 2) {
                return;
            }

            int type = ModContent.ProjectileType<DragonRageFireOrb>();
            if (Projectile.ai[0] == 3) {
                float OrbSize = Main.rand.NextFloat(0.5f, 0.8f) * Projectile.numHits;
                if (OrbSize > 2.2f) {
                    OrbSize = 2.2f;
                }
                CalamityMod.Particles.Particle orb = new CalamityMod.Particles.GenericBloom(target.Center, Vector2.Zero, Color.OrangeRed, OrbSize + 0.6f, 8, true);
                CalamityMod.Particles.GeneralParticleHandler.SpawnParticle(orb);
                CalamityMod.Particles.Particle orb2 = new CalamityMod.Particles.GenericBloom(target.Center, Vector2.Zero, Color.White, OrbSize + 0.2f, 8, true);
                CalamityMod.Particles.GeneralParticleHandler.SpawnParticle(orb2);

                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center
                    , Vector2.Zero, ModContent.ProjectileType<FuckYou>(), Projectile.damage / 4
                    , Projectile.knockBack, Projectile.owner, 0f, 0.85f + Main.rand.NextFloat() * 1.15f);
                Main.projectile[proj].DamageType = DamageClass.Melee;
            }
            else if (Projectile.ai[0] == 6 && Projectile.IsOwnedByLocalPlayer() && Projectile.numHits % 3 == 0 && RDragonRage.CoolWorld) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vr = (MathHelper.TwoPi / 3f * i + Main.GameUpdateCount * 0.1f).ToRotationVector2();
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center + vr * Main.rand.Next(22, 38), vr.RotatedByRandom(0.32f) * 3
                    , type, Projectile.damage / 6, Projectile.knockBack, Projectile.owner, 0f, rotSpeed * 0.1f);
                }
            }

            HitEffectValue(target, 13, out Vector2 rotToTargetSpeedTrengsVumVer, out int sparkCount);
            if (theofSteel) {
                SoundEngine.PlaySound(Murasama.InorganicHit with { Pitch = 0.75f }, target.Center);
            }
            else {
                SoundEngine.PlaySound(Murasama.OrganicHit with { Pitch = 1.25f }, target.Center);
            }

            for (int i = 0; i < sparkCount; i++) {
                Vector2 sparkVelocity2 = rotToTargetSpeedTrengsVumVer.RotatedByRandom(0.35f) * Main.rand.NextFloat(0.3f, 1.6f);
                int sparkLifetime2 = Main.rand.Next(18, 30);
                float sparkScale2 = Main.rand.NextFloat(0.65f, 1.2f);
                Color sparkColor2 = Main.rand.NextBool(3) ? Color.OrangeRed : Color.DarkRed;

                if (Projectile.ai[0] == 0 || Projectile.ai[0] == 1) {
                    sparkVelocity2 *= 0.8f;
                    sparkScale2 *= 0.9f;
                    sparkLifetime2 = Main.rand.Next(13, 25);
                }
                else if (Projectile.ai[0] == 3) {
                    sparkVelocity2 *= 1.28f;
                }
                else if (Projectile.ai[0] == 4 || Projectile.ai[0] == 5) {
                    sparkVelocity2 *= 1.28f;
                    sparkScale2 *= 1.19f;
                    sparkLifetime2 = Main.rand.Next(23, 35);
                }

                if (theofSteel) {
                    sparkColor2 = Main.rand.NextBool(3) ? Color.Gold : Color.Goldenrod;
                }

                PRT_Spark spark = new PRT_Spark(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f
                        , target.height * 0.5f) + (Projectile.velocity * 1.2f), sparkVelocity2 * 1f
                        , false, (int)(sparkLifetime2 * 1.2f), sparkScale2 * 1.4f, sparkColor2);
                PRTLoader.AddParticle(spark);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.ai[0] == 3) {
                target.AddBuff(ModContent.BuffType<HellburnBuff>(), 300);
            }
            HitEffect(target, CWRLoad.NPCValue.ISTheofSteel(target));
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            if (Projectile.ai[0] == 3) {
                target.AddBuff(ModContent.BuffType<HellburnBuff>(), 300);
            }
            HitEffect(target, false);
        }

        public override void SwingModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            bool isBrimstoneHeart = target.type == ModContent.NPCType<BrimstoneHeart>();
            if (Projectile.ai[0] == 3) {
                if (modifiers.SuperArmor || target.defense > 999
                    || target.Calamity().DR >= 0.95f || target.Calamity().unbreakableDR) {
                    return;
                }
                modifiers.DefenseEffectiveness *= 0f;
                if (isBrimstoneHeart) {
                    modifiers.FinalDamage *= 1.25f;
                }
            }
            else if (Projectile.ai[0] == 6) {
                if (isBrimstoneHeart) {
                    modifiers.FinalDamage *= 1.45f;
                    modifiers.DefenseEffectiveness *= 0f;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            float rotding = Owner.Center.To(Projectile.Center).ToRotation();
            float size = Projectile.scale * MeleeSize;
            Vector2 endPos = rotding.ToRotationVector2() * Length * size * 1.3f + Projectile.Center;
            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Owner.Center, endPos, 25 * size, ref point)) {
                return true;
            }
            return null;
        }

        public override void DrawTrail(List<VertexPositionColorTexture> bars) {
            Effect effect = EffectLoader.KnifeRendering.Value;

            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["sampleTexture"].SetValue(TrailTexture);
            effect.Parameters["gradientTexture"].SetValue(GradientTexture);
            //应用shader，并绘制顶点
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
                Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
                Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
            }
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) {
            if (Projectile.ai[0] == 6) {
                Texture2D value = CWRAsset.SemiCircularSmear.Value;
                Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
                Main.EntitySpriteDraw(color: Color.Red * 0.9f
                    , origin: value.Size() * 0.5f, texture: value, position: Owner.Center - Main.screenPosition
                    , sourceRectangle: null, rotation: Projectile.rotation
                    , scale: Projectile.scale * 2.15f, effects: SpriteEffects.None);
                Main.spriteBatch.ExitShaderRegion();
            }
            base.DrawSwing(spriteBatch, lightColor);
        }
    }
}
