using CalamityMod;
using CalamityMod.NPCs.SupremeCalamitas;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
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
    internal class DragonRageHeld : BaseHeldProj, ILoader
    {
        public override string Texture => CWRConstant.Projectile_Melee + "DragonRageHeld";
        private Vector2 vector;
        private Vector2 startVector;
        public ref float Length => ref Projectile.localAI[0];
        public ref float Rot => ref Projectile.localAI[1];
        private float oldRot;
        private float rotSpeed;
        public int Timer;
        private float speed;
        int trailCount;
        int distanceToOwner;
        float trailTopWidth;
        private float[] oldRotate;
        private float[] oldLength;
        private float[] oldDistanceToOwner;
        private static Asset<Texture2D> trailTexture;
        private static Asset<Texture2D> gradientTexture;
        void ILoader.LoadAsset() {
            trailTexture = CWRUtils.GetT2DAsset(CWRConstant.Masking + "MotionTrail3");
            gradientTexture = CWRUtils.GetT2DAsset(CWRConstant.Masking + "DragonRageEffectColorBar");
        }
        void ILoader.UnLoadData() {
            trailTexture = null;
            gradientTexture = null;
        }
        public Vector2 RodingToVer(float radius, float theta) {
            Vector2 vector2 = theta.ToRotationVector2();
            vector2.X *= radius;
            vector2.Y *= radius;
            return vector2;
        }
        private int getExtraUpdatesCount() {
            int num = Projectile.extraUpdates;
            num += 1;
            return num;
        }
        public override bool ShouldUpdatePosition() => false;
        public override void SetDefaults() {
            Projectile.CloneDefaults(ProjectileID.Spear);
            AIType = Projectile.aiStyle = 0;
            Projectile.scale = 1f;
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.alpha = 255;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15 * getExtraUpdatesCount();
            Projectile.extraUpdates = 3;
            trailCount = 15 * getExtraUpdatesCount();
            distanceToOwner = 125;
            trailTopWidth = 90;
            oldRotate = new float[trailCount];
            oldDistanceToOwner = new float[trailCount];
            oldLength = new float[trailCount];
            InitializeCaches();
            Rot = MathHelper.ToRadians(3);
            Length = 80;
        }

        public override void AI() {
            float updateCount = getExtraUpdatesCount();
            Projectile.Calamity().timesPierced = 0;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Projectile.Center = Owner.MountedCenter + vector;

            if (Projectile.ai[0] != 6) {
                Projectile.spriteDirection = Owner.direction;
                Owner.SetCompositeArmFront(true, Length >= 80 ? Player.CompositeArmStretchAmount.Full : Player.CompositeArmStretchAmount.Quarter
                    , (Owner.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2);
            }

            if (Projectile.spriteDirection == 1) {
                Projectile.rotation = (Projectile.Center - Owner.Center).ToRotation() + MathHelper.PiOver4;
            }
            else {
                Projectile.rotation = (Projectile.Center - Owner.Center).ToRotation() - MathHelper.Pi - MathHelper.PiOver4;
            }

            if (Projectile.ai[0] == 0) {
                if (Timer++ == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                }

                if (Timer < 10) {
                    Length *= 1 + 0.1f / updateCount;
                    Rot += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rot += speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                if (Timer >= 22 * updateCount) {
                    Projectile.Kill();
                }
                if (Timer % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 120, 160);
                }
            }
            else if (Projectile.ai[0] == 1) {
                if (Timer++ == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() + MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                }

                if (Timer < 10) {
                    Length *= 1 + 0.1f / updateCount;
                    Rot -= speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rot -= speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                if (Timer >= 22 * updateCount) {
                    Projectile.Kill();
                }
                if (Timer % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 110, 120);
                }
            }
            else if (Projectile.ai[0] == 2) {
                if (Timer++ == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                }

                if (Timer < 10) {
                    Length *= 1 + 0.11f / updateCount;
                    Rot += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.3f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rot += speed * Projectile.spriteDirection;
                    speed *= 1 - 0.11f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                }

                if (Timer >= 26 * updateCount) {
                    Projectile.Kill();
                }
                if (Timer % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 60, 120);
                }
            }
            else if (Projectile.ai[0] == 3) {
                if (Timer++ == 0) {
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation());
                    speed = 1 + 0.6f / updateCount;
                }

                if (Timer < 6 * updateCount) {
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
                    CWRParticle spark = new SparkParticle(spanSparkPos, Projectile.velocity, false, 6, 4.26f, Color.Gold, Owner);
                    CWRParticleHandler.AddParticle(spark);
                }

                Length *= speed;
                vector = startVector * Length;
                speed -= 0.015f / updateCount;

                if (Timer >= 26 * updateCount) {
                    Projectile.Kill();
                }
                float toTargetSengs = Projectile.Center.To(Owner.Center).Length();
                Projectile.scale = 0.8f + toTargetSengs / 520f;
                if (Timer % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 30, 260);
                }
            }
            else if (Projectile.ai[0] == 4) {
                if (Timer++ == 0) {
                    distanceToOwner = 125;
                    trailTopWidth = 190;
                    InitializeCaches();
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                    Rot = MathHelper.ToRadians(-30 * Projectile.spriteDirection);
                }

                if (Timer < 10) {
                    Length *= 1 + 0.1f / updateCount;
                    Rot += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                    Projectile.scale += 0.025f;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rot += speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                    if (Timer >= 20 * updateCount) {
                        Projectile.scale -= 0.001f;
                    }
                }
                if (Timer >= 22 * updateCount) {
                    Projectile.Kill();
                }
                if (Timer % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 120, 260);
                }
            }
            else if (Projectile.ai[0] == 5) {
                if (Timer++ == 0) {
                    distanceToOwner = 125;
                    trailTopWidth = 190;
                    InitializeCaches();
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                    Rot = MathHelper.ToRadians(-110 * Projectile.spriteDirection);
                }

                if (Timer < 10) {
                    Length *= 1 + 0.1f / updateCount;
                    Rot -= speed * Projectile.spriteDirection;
                    speed *= 1 + 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                    Projectile.scale += 0.025f;
                }
                else {
                    Length *= 1 - 0.01f / updateCount;
                    Rot -= speed * Projectile.spriteDirection;
                    speed *= 1 - 0.2f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                    if (Timer >= 20 * updateCount) {
                        Projectile.scale -= 0.001f;
                    }
                }
                if (Timer >= 22 * updateCount) {
                    Projectile.Kill();
                }
                if (Timer % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 120, 260);
                }
            }
            else if (Projectile.ai[0] == 6) {
                if (Timer++ == 0) {
                    distanceToOwner = 155;
                    trailTopWidth = 60;
                    InitializeCaches();
                    Projectile.spriteDirection = Owner.direction;
                    startVector = RodingToVer(1, Projectile.velocity.ToRotation() - MathHelper.PiOver2 * Projectile.spriteDirection);
                    speed = MathHelper.ToRadians(6);
                }

                if (Timer < 10) {
                    Length *= 1 + 0.11f / updateCount;
                    Rot += speed * Projectile.spriteDirection;
                    speed *= 1 + 0.3f / updateCount;
                    vector = startVector.RotatedBy(Rot) * Length;
                    Projectile.scale += 0.011f;
                }
                else {
                    Rot += speed * Projectile.spriteDirection;
                    if (!DownRight) {
                        speed *= 1 - 0.01f / updateCount;
                        if (Timer >= 60 * updateCount) {
                            Length *= 1 - 0.01f / updateCount;
                            Projectile.scale -= 0.001f;
                        }
                    }
                    else {
                        if (Timer > 30 * updateCount) {
                            Timer = (int)(30 * updateCount);
                        }
                        if (Projectile.soundDelay <= 0) {
                            SoundEngine.PlaySound(SupremeCalamitas.CatastropheSwing with { MaxInstances = 6, Volume = 0.45f }, Owner.Center);
                            Projectile.soundDelay = (int)(30 * updateCount);
                        }
                    }

                    Owner.ChangeDir(Math.Sign(ToMouse.X));
                    Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter
                        , Owner.direction < 0 ? MathHelper.PiOver4 : MathHelper.PiOver4 + MathHelper.Pi + MathHelper.PiOver2);

                    vector = startVector.RotatedBy(Rot) * Length;

                    SpawnDust(Owner, Owner.direction);
                }

                if (Timer >= 90 * updateCount && !DownRight) {
                    Projectile.Kill();
                }
                if (Timer % updateCount == updateCount - 1) {
                    Length = MathHelper.Clamp(Length, 60, 220);
                }
            }

            UpdateCaches();

            rotSpeed = Rot - oldRot;
            oldRot = Rot;

            if (Timer > 1) {
                Projectile.alpha = 0;
            }
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
            SparkParticle inds = new SparkParticle(Vector2.Zero, Vector2.Zero, false, 0, 0, Color.White);
            int pysCount = CWRParticleHandler.GetParticlesCount(CWRParticleHandler.GetParticleType(inds.GetType()));
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
                else if(Projectile.ai[0] == 4 || Projectile.ai[0] == 5) {
                    sparkVelocity2 *= 1.28f;
                    sparkScale2 *= 1.19f;
                    sparkLifetime2 = Main.rand.Next(23, 35);
                }

                if (theofSteel) {
                    sparkColor2 = Main.rand.NextBool(3) ? Color.Gold : Color.Goldenrod;
                }

                SparkParticle spark = new SparkParticle(target.Center + Main.rand.NextVector2Circular(target.width * 0.5f
                        , target.height * 0.5f) + (Projectile.velocity * 1.2f), sparkVelocity2 * 1f
                        , false, (int)(sparkLifetime2 * 1.2f), sparkScale2 * 1.4f, sparkColor2);
                CWRParticleHandler.AddParticle(spark);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<HellfireExplosion>(), 300);
            if (Projectile.ai[0] == 3) {
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center
                    , Vector2.Zero, ModContent.ProjectileType<FuckYou>(), Projectile.damage / 4
                    , Projectile.knockBack, Projectile.owner, 0f, 0.85f + Main.rand.NextFloat() * 1.15f);
                Main.projectile[proj].DamageType = DamageClass.Melee;
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
            Vector2 endPos = rotding.ToRotationVector2() * Length * Projectile.scale * 1.25f + Projectile.Center;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, endPos, 25 * Projectile.scale, ref point);
        }

        private void InitializeCaches() {
            for (int j = trailCount - 1; j >= 0; j--) {
                oldRotate[j] = 100f;
                oldDistanceToOwner[j] = distanceToOwner;
                oldLength[j] = Projectile.height * Projectile.scale;
            }
        }

        private void UpdateCaches() {
            if (Timer < 2) {
                return;
            }

            for (int i = trailCount - 1; i > 0; i--) {
                oldRotate[i] = oldRotate[i - 1];
                oldDistanceToOwner[i] = oldDistanceToOwner[i - 1];
                oldLength[i] = oldLength[i - 1];
            }

            oldRotate[0] = (Projectile.Center - Owner.Center).ToRotation();
            oldDistanceToOwner[0] = distanceToOwner;
            oldLength[0] = Projectile.height * Projectile.scale;
        }

        private float ControlTrailBottomWidth(float factor) {
            return 70 * Projectile.scale;
        }

        private void GetCurrentTrailCount(out float count) {
            count = 0f;
            if (oldRotate == null)
                return;

            for (int i = 0; i < oldRotate.Length; i++)
                if (oldRotate[i] != 100f)
                    count += 1f;
        }

        public static void DrawTrail(GraphicsDevice device, Action draw
            , BlendState blendState = null, SamplerState samplerState = null, RasterizerState rasterizerState = null) {
            RasterizerState originalState = Main.graphics.GraphicsDevice.RasterizerState;
            BlendState originalBlendState = Main.graphics.GraphicsDevice.BlendState;
            SamplerState originalSamplerState = Main.graphics.GraphicsDevice.SamplerStates[0];

            device.BlendState = blendState ?? originalBlendState;
            device.SamplerStates[0] = samplerState ?? originalSamplerState;
            device.RasterizerState = rasterizerState ?? originalState;

            draw();

            device.RasterizerState = originalState;
            device.BlendState = originalBlendState;
            device.SamplerStates[0] = originalSamplerState;
            Main.pixelShader.CurrentTechnique.Passes[0].Apply();
        }

        public static Matrix GetTransfromMaxrix() {
            Matrix world = Matrix.CreateTranslation(-Main.screenPosition.Vec3());
            Matrix view = Main.GameViewMatrix.TransformationMatrix;
            Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);
            return world * view * projection;
        }

        private void DrawSlashTrail() {
            List<VertexPositionColorTexture> bars = new List<VertexPositionColorTexture>();
            GetCurrentTrailCount(out float count);

            for (int i = 0; i < count; i++) {
                if (oldRotate[i] == 100f)
                    continue;

                float factor = 1f - i / count;
                Vector2 Center = Owner.GetPlayerStabilityCenter();
                Vector2 Top = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] + trailTopWidth + oldDistanceToOwner[i]);
                Vector2 Bottom = Center + oldRotate[i].ToRotationVector2() * (oldLength[i] - ControlTrailBottomWidth(factor) + oldDistanceToOwner[i]);

                var topColor = Color.Lerp(new Color(238, 218, 130, 200), new Color(167, 127, 95, 0), 1 - factor);
                var bottomColor = Color.Lerp(new Color(109, 73, 86, 200), new Color(83, 16, 85, 0), 1 - factor);
                bars.Add(new(Top.Vec3(), topColor, new Vector2(factor, 0)));
                bars.Add(new(Bottom.Vec3(), bottomColor, new Vector2(factor, 1)));
            }

            if (bars.Count > 2) {
                DrawTrail(Main.graphics.GraphicsDevice, () => {
                    Effect effect = CWRMod.Instance.Assets.Request<Effect>(CWRConstant.noEffects + "KnifeRendering").Value;

                    effect.Parameters["transformMatrix"].SetValue(GetTransfromMaxrix());
                    effect.Parameters["sampleTexture"].SetValue(trailTexture.Value);
                    effect.Parameters["gradientTexture"].SetValue(gradientTexture.Value);

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes) //应用shader，并绘制顶点
                    {
                        pass.Apply();
                        Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
                        Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
                        Main.graphics.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bars.ToArray(), 0, bars.Count - 2);
                    }
                }, BlendState.NonPremultiplied, SamplerState.PointWrap, RasterizerState.CullNone);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.ai[0] != 3) {
                DrawSlashTrail();
            }
            if (Projectile.ai[0] == 6) {
                Texture2D value = ModContent.Request<Texture2D>("CalamityMod/Particles/SemiCircularSmear").Value;
                Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
                Main.EntitySpriteDraw(color: Color.Red * 0.9f
                    , origin: value.Size() * 0.5f, texture: value, position: Owner.Center - Main.screenPosition
                    , sourceRectangle: null, rotation: Projectile.rotation
                    , scale: Projectile.scale * 3.15f, effects: SpriteEffects.None);
                Main.spriteBatch.ExitShaderRegion();
            }
            Texture2D texture = CWRUtils.GetT2DValue(Texture);
            Rectangle rect = new(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new(texture.Width / 2, texture.Height / 2);
            var effects = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Vector2 v = Projectile.Center - RodingToVer(48, (Projectile.Center - Owner.Center).ToRotation());

            Main.EntitySpriteDraw(texture, v - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY, new Rectangle?(rect)
                , Color.White, Projectile.rotation, drawOrigin, Projectile.scale, effects, 0);
            return false;
        }
    }
}
