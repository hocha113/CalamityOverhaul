using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj
{
    internal class MurasamaEndSkillOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        internal PrimitiveTrail LightningDrawer;

        public const int BaseProjTime = 45;
        public ref float OrigVelocityAngle => ref Projectile.ai[0];

        public ref float BaseTurnAngleRatio => ref Projectile.ai[1];
        public ref float AccumulatedXMovementSpeeds => ref Projectile.localAI[0];
        public ref float BranchingIteration => ref Projectile.localAI[1];

        public virtual float TurnRandomnessFactor { get; } = 2f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 7000;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 50;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.alpha = 255;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Default;
            Projectile.MaxUpdates = 15;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 13 * Projectile.MaxUpdates;
            Projectile.timeLeft = BaseProjTime * Projectile.MaxUpdates;
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(AccumulatedXMovementSpeeds);
            writer.Write(BranchingIteration);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            AccumulatedXMovementSpeeds = reader.ReadSingle();
            BranchingIteration = reader.ReadSingle();
        }

        public override bool PreAI() {
            Projectile.frameCounter++;
            Projectile.oldPos[1] = Projectile.oldPos[0];
            return true;
        }

        public override void AI() {
            float adjustedTimeLife = Projectile.timeLeft / Projectile.MaxUpdates;
            Projectile.Opacity = Utils.GetLerpValue(0f, 9f, adjustedTimeLife, true) * Utils.GetLerpValue(BaseProjTime, BaseProjTime - 3f, adjustedTimeLife, true);
            Projectile.scale = Projectile.Opacity;

            Lighting.AddLight(Projectile.Center, Color.White.ToVector3());
            if (Projectile.frameCounter >= Projectile.extraUpdates * 2) {
                Projectile.frameCounter = 0;

                float originalSpeed = MathHelper.Min(15f, Projectile.velocity.Length());
                UnifiedRandom unifiedRandom = new((int)BaseTurnAngleRatio);
                int turnTries = 0;
                Vector2 newBaseDirection = -Vector2.UnitY;
                Vector2 potentialBaseDirection;

                do {
                    BaseTurnAngleRatio = unifiedRandom.Next() % 100;
                    potentialBaseDirection = (BaseTurnAngleRatio / 100f * MathHelper.TwoPi).ToRotationVector2();

                    potentialBaseDirection.Y = -Math.Abs(potentialBaseDirection.Y);

                    bool canChangeLightningDirection = true;

                    if (potentialBaseDirection.Y > -0.02f)
                        canChangeLightningDirection = false;

                    if (Math.Abs(potentialBaseDirection.X * (Projectile.extraUpdates + 1) * 2f * originalSpeed + AccumulatedXMovementSpeeds) > Projectile.MaxUpdates * TurnRandomnessFactor)
                        canChangeLightningDirection = false;

                    if (canChangeLightningDirection)
                        newBaseDirection = potentialBaseDirection;

                    turnTries++;
                }
                while (turnTries < 100);

                if (Projectile.velocity != Vector2.Zero) {
                    AccumulatedXMovementSpeeds += newBaseDirection.X * (Projectile.extraUpdates + 1) * 2f * originalSpeed;
                    Projectile.velocity = newBaseDirection.RotatedBy(OrigVelocityAngle + MathHelper.PiOver2) * originalSpeed;
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                }
            }
        }

        public virtual float PrimitiveWidthFunction(float completionRatio) => CalamityUtils.Convert01To010(completionRatio) * Projectile.scale * Projectile.width;

        public virtual Color PrimitiveColorFunction(float completionRatio) {
            float colorInterpolant = (float)Math.Sin(Projectile.identity / 3f + completionRatio * 20f + Main.GlobalTimeWrappedHourly * 1.1f) * 0.5f + 0.5f;
            Color color = CalamityUtils.MulticolorLerp(colorInterpolant, Color.Red, Color.IndianRed, Color.DarkRed);
            return color;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRIDs.targetNpcTypes7_1.Contains(target.type)) {
                modifiers.FinalDamage *= 0.1f;
                modifiers.SetMaxDamage(6000);
            }
            if (CWRIDs.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.5f;
            }
        }

        public override bool? CanDamage() {
            if (!EndSkillEffectStart.CanDealDamageToNPCs()) {
                return false;
            }
            return base.CanDamage();
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float point = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center
                , Projectile.ai[0].ToRotationVector2() * 5000 + Projectile.Center, 32, ref point);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (LightningDrawer is null)
                LightningDrawer = new PrimitiveTrail(PrimitiveWidthFunction, PrimitiveColorFunction, PrimitiveTrail.RigidPointRetreivalFunction, GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"]);

            GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].SetShaderTexture(CWRUtils.GetT2DAsset(CWRConstant.Masking + "WavyNoise"));
            GameShaders.Misc["CalamityMod:HeavenlyGaleLightningArc"].Apply();

            LightningDrawer.Draw(Projectile.oldPos, Projectile.Size * 0.5f - Main.screenPosition, 28);
            return false;
        }
    }
}
