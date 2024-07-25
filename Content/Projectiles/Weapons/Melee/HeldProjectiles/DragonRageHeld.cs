using CalamityMod;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
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
        public override string Texture => CWRConstant.Cay_Proj_Melee + "DragonRageStaff";
        private static Asset<Texture2D> trailTexture;
        private static Asset<Texture2D> gradientTexture;
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.Masking + "DragonRageEffectColorBar";
        public override void SetSwingProperty() {
            Projectile.CloneDefaults(ProjectileID.Spear);
            Projectile.extraUpdates = 3;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
            distanceToOwner = 125;
            trailTopWidth = 90;
            Length = 80;
        }

        public override void SwingAI() {
            if (Projectile.ai[0] == 0) {
                if (Time == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                }

                if (Time < 10) {
                    Length *= 1 + 0.1f / updateCount;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                if (Time >= 22 * updateCount) {
                    Projectile.Kill();
                }
                if (Time % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 120, 160);
                }
            }
            else if (Projectile.ai[0] == 1) {
                if (Time == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() + MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                }

                if (Time < 10) {
                    Length *= 1 + 0.1f / updateCount;
                    Rotation -= speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rotation -= speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                if (Time >= 22 * updateCount) {
                    Projectile.Kill();
                }
                if (Time % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 110, 120);
                }
            }
            else if (Projectile.ai[0] == 2) {
                if (Time == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                }

                if (Time < 10) {
                    Length *= 1 + 0.11f / updateCount;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.3f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 - 0.11f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                }

                if (Time >= 26 * updateCount) {
                    Projectile.Kill();
                }
                if (Time % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 60, 120);
                }
            }
            else if (Projectile.ai[0] == 3) {
                if (Time == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation());
                    speed = 1 + 0.6f / updateCount;
                }

                if (Time < 6 * updateCount) {
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
                    BaseParticle spark = new PRK_Spark(spanSparkPos, Projectile.velocity, false, 6, 4.26f, Color.Gold, Owner);
                    DRKLoader.AddParticle(spark);
                }

                Length *= speed;
                vector = startVector * Length;
                speed -= 0.015f / updateCount;

                if (Time >= 26 * updateCount) {
                    Projectile.Kill();
                }
                float toTargetSengs = Projectile.Center.To(Owner.Center).Length();
                Projectile.scale = 0.8f + toTargetSengs / 520f;
                if (Time % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 30, 260);
                }
            }
            else if (Projectile.ai[0] == 4) {
                if (Time == 0) {
                    distanceToOwner = 105;
                    trailTopWidth = 190;
                    InitializeCaches();
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                    Rotation = MathHelper.ToRadians(-30 * Projectile.spriteDirection);
                }

                if (Time < 10) {
                    Length *= 1 + 0.1f / updateCount;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    Projectile.scale += 0.03f;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    if (Time >= 20 * updateCount) {
                        Projectile.scale -= 0.001f;
                    }
                }
                if (Time >= 22 * updateCount) {
                    Projectile.Kill();
                }
                if (Time % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 120, 260);
                }
            }
            else if (Projectile.ai[0] == 5) {
                if (Time == 0) {
                    distanceToOwner = 105;
                    trailTopWidth = 190;
                    InitializeCaches();
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                    Rotation = MathHelper.ToRadians(-110 * Projectile.spriteDirection);
                }

                if (Time < 10) {
                    Length *= 1 + 0.1f / updateCount;
                    Rotation -= speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    Projectile.scale += 0.03f;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rotation -= speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    if (Time >= 20 * updateCount) {
                        Projectile.scale -= 0.001f;
                    }
                }
                if (Time >= 22 * updateCount) {
                    Projectile.Kill();
                }
                if (Time % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 120, 260);
                }
            }
            else if (Projectile.ai[0] == 6) {
                if (Time == 0) {
                    distanceToOwner = 155;
                    trailTopWidth = 60;
                    InitializeCaches();
                    Projectile.spriteDirection = Owner.direction;
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                }

                if (Time < 10) {
                    Length *= 1 + 0.11f / updateCount;
                    Rotation += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.3f / updateCount;
                    vector = startVector.RotatedBy(Rotation) * Length;
                    Projectile.scale += 0.011f;
                }
                else {
                    Rotation += speed * Projectile.spriteDirection;
                    if (!DownRight) {
                        speed *= 1 - 0.01f / updateCount;
                        if (Time >= 60 * updateCount) {
                            Length *= 1 - 0.01f / updateCount;
                            Projectile.scale -= 0.001f;
                        }
                    }
                    else {
                        if (Time > 30 * updateCount) {
                            Time = (int)(30 * updateCount);
                        }
                        if (Projectile.soundDelay <= 0) {
                            SoundEngine.PlaySound(SupremeCalamitas.CatastropheSwing with { MaxInstances = 6, Volume = 0.45f }, Owner.Center);
                            Projectile.soundDelay = (int)(30 * updateCount);
                        }
                    }

                    Owner.ChangeDir(Math.Sign(ToMouse.X));
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter
                        , Owner.direction < 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi + MathHelper.PiOver2);

                    vector = startVector.RotatedBy(Rotation) * Length;
                    if (Time % updateCount == 0) {
                        SpawnDust(Owner, Owner.direction);
                    }
                }

                if (Time >= 90 * updateCount && !DownRight) {
                    Projectile.Kill();
                }
                if (Time % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 60, 220);
                }
            }

            if (Time > 1) {
                Projectile.alpha = 0;
            }

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
            if (theofSteel) {
                SoundEngine.PlaySound(MurasamaEcType.InorganicHit with { Pitch = 0.75f }, target.Center);
            }
            else {
                SoundEngine.PlaySound(MurasamaEcType.OrganicHit with { Pitch = 1.25f }, target.Center);
            }

            int sparkCount = 13;
            Vector2 toTarget = Owner.Center.To(target.Center);
            Vector2 norlToTarget = toTarget.GetNormalVector();
            int ownerToTargetSetDir = Math.Sign(toTarget.X);
            if (ownerToTargetSetDir != DirSign) {
                ownerToTargetSetDir = -1;
            }
            else {
                ownerToTargetSetDir = 1;
            }

            if (rotSpeed > 0) {
                norlToTarget *= -1;
            }
            if (rotSpeed < 0) {
                norlToTarget *= 1;
            }

            float rotToTargetSpeedSengs = rotSpeed * 3 * ownerToTargetSetDir;
            Vector2 rotToTargetSpeedTrengsVumVer = norlToTarget.RotatedBy(-rotToTargetSpeedSengs) * 13;
            if (Projectile.ai[0] == 3) {
                rotToTargetSpeedTrengsVumVer = Projectile.velocity.RotatedBy(rotToTargetSpeedSengs);
            }
            
            int pysCount = DRKLoader.GetParticlesCount(DRKLoader.GetParticleType(typeof(PRK_Spark)));
            if (pysCount > 120) {
                sparkCount = 10;
            }
            if (pysCount > 220) {
                sparkCount = 8;
            }
            if (pysCount > 350) {
                sparkCount = 6;
            }
            if (pysCount > 500) {
                sparkCount = 3;
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

                PRK_Spark spark = new PRK_Spark(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f
                        , target.height * 0.5f) + (Projectile.velocity * 1.2f), sparkVelocity2 * 1f
                        , false, (int)(sparkLifetime2 * 1.2f), sparkScale2 * 1.4f, sparkColor2);
                DRKLoader.AddParticle(spark);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
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

                target.AddBuff(ModContent.BuffType<HellfireExplosion>(), 300);
            }

            else if (Projectile.ai[0] == 6 && Projectile.IsOwnedByLocalPlayer() && Projectile.numHits % 3 == 0 && DragonRageEcType.coolWorld) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vr = (MathHelper.TwoPi / 3f * i + Main.GameUpdateCount * 0.1f).ToRotationVector2();
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center + vr * Main.rand.Next(22, 38), vr.RotatedByRandom(0.32f) * 3
                    , type, Projectile.damage / 6, Projectile.knockBack, Projectile.owner, 0f, rotSpeed * 0.1f);
                }
            }

            HitEffect(target, CWRLoad.NPCValue.TheofSteel[target.type]);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<HellfireExplosion>(), 300);
            if (Projectile.ai[0] == 3) {
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center
                    , Vector2.Zero, ModContent.ProjectileType<FuckYou>(), Projectile.damage / 4
                    , Projectile.knockBack, Projectile.owner, 0f, 0.85f + Main.rand.NextFloat() * 1.15f);
                Main.projectile[proj].DamageType = DamageClass.Melee;
            }

            HitEffect(target, false);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
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
            else if (Projectile.ai[0] == 6 && isBrimstoneHeart) {
                modifiers.FinalDamage *= 1.25f;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            float rotding = Owner.Center.To(Projectile.Center).ToRotation();
            Vector2 endPos = rotding.ToRotationVector2() * Length * Projectile.scale * 1.3f + Projectile.Center;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, endPos, 25 * Projectile.scale, ref point);
        }

        public override void DrawTrail(List<VertexPositionColorTexture> bars) {
            Effect effect = CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffects + "KnifeRendering").Value;

            effect.Parameters["transformMatrix"].SetValue(GetTransfromMaxrix());
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
                Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Particles/SemiCircularSmear").Value;
                Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
                Main.EntitySpriteDraw(color: Color.Red * 0.9f
                    , origin: value.Size() * 0.5f, texture: value, position: Owner.Center - Main.screenPosition
                    , sourceRectangle: null, rotation: Projectile.rotation
                    , scale: Projectile.scale * 3.15f, effects: SpriteEffects.None);
                Main.spriteBatch.ExitShaderRegion();
            }
            base.DrawSwing(spriteBatch, lightColor);
        }
    }
}
