using CalamityMod;
using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Graphics.Primitives;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RTerratomereHoldoutProj : ModProjectile
    {
        public Player Owner => Main.player[Projectile.owner];

        public int Direction => Projectile.velocity.X.DirectionalSign();

        public float SwingCompletion => MathHelper.Clamp(Time / 83f, 0f, 1f);

        public float SwingCompletionAtStartOfTrail => MathHelper.Clamp(SwingCompletion - 0.2f, SwingCompletionRatio, 1f);

        public float SwordRotation {
            get {
                float num = InitialRotation + GetSwingOffsetAngle(SwingCompletion) * Projectile.spriteDirection + MathF.PI / 4f;
                if (Projectile.spriteDirection == -1) {
                    num += MathF.PI / 2f;
                }

                return num;
            }
        }

        public Vector2 SwordDirection => SwordRotation.ToRotationVector2() * Direction;

        public ref float Time => ref Projectile.ai[0];

        public ref float InitialRotation => ref Projectile.ai[1];

        public static float SwingCompletionRatio => 0.37f;

        public static float RecoveryCompletionRatio => 0.84f;

        public static CalamityUtils.CurveSegment AnticipationWait => new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyOut, 0f, -1.67f, 0f);

        public static CalamityUtils.CurveSegment Anticipation => new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyOut, 0.14f, AnticipationWait.EndingHeight, -1.05f, 2);

        public static CalamityUtils.CurveSegment Swing => new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyIn, SwingCompletionRatio, Anticipation.EndingHeight, 4.43f, 5);

        public static CalamityUtils.CurveSegment Recovery => new CalamityUtils.CurveSegment(CalamityUtils.EasingType.PolyOut, RecoveryCompletionRatio, Swing.EndingHeight, 0.97f, 3);

        public override string Texture => CWRConstant.Cay_Wap_Melee + "Terratomere";

        public static float GetSwingOffsetAngle(float completion) {
            return CalamityUtils.PiecewiseAnimation(completion, AnticipationWait, Anticipation, Swing, Recovery);
        }

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 100;
        }

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 66;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.timeLeft = 83;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.MaxUpdates = 2;
            Projectile.localNPCHitCooldown = Projectile.MaxUpdates * 7;
            Projectile.noEnchantmentVisuals = true;
        }

        public override bool ShouldUpdatePosition() {
            return false;
        }

        public override void AI() {
            if (InitialRotation == 0f) {
                InitialRotation = Projectile.velocity.ToRotation();
                Projectile.netUpdate = true;
            }

            Projectile.scale = Utils.GetLerpValue(0f, 0.13f, SwingCompletion, clamped: true) * Utils.GetLerpValue(1f, 0.87f, SwingCompletion, clamped: true) * 0.7f + 0.3f + 1f;
            AdjustPlayerValues();
            StickToOwner();
            CreateProjectiles();
            if (SwingCompletion > SwingCompletionRatio + 0.2f && SwingCompletion < RecoveryCompletionRatio) {
                CreateSlashSparkleDust();
            }

            Projectile.rotation = SwordRotation;
            Time += 1f;
        }

        public void AdjustPlayerValues() {
            Projectile.spriteDirection = Projectile.direction = Direction;
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
            Owner.itemRotation = (Projectile.direction * Projectile.velocity).ToRotation();
            float num = SwordRotation - Direction * 1.67f;
            Owner.SetCompositeArmFront(Math.Abs(num) > 0.01f, Player.CompositeArmStretchAmount.Full, num);
        }

        public void StickToOwner() {
            Projectile.Center = Owner.Center;
            Owner.heldProj = Projectile.whoAmI;
            Owner.SetDummyItemTime(2);
            Owner.direction = Direction;
        }

        public void CreateProjectiles() {
            if (Time == (int)(83f * (SwingCompletionRatio + 0.15f))) {
                SoundEngine.PlaySound(in TerratomereEcType.SwingSound, Projectile.Center);
            }

            if (Main.myPlayer == Projectile.owner && Time == (int)(83f * (SwingCompletionRatio + 0.34f))) {
                Vector2 vector = Projectile.SafeDirectionTo(Main.MouseWorld) * Owner.ActiveItem().shootSpeed;
                if (vector.AngleBetween(InitialRotation.ToRotationVector2()) > 1.456f) {
                    vector = InitialRotation.ToRotationVector2() * vector.Length();
                }

                for (int i = 0; i < 3; i++) {
                    Vector2 vr = Owner.Center.To(Main.MouseWorld).UnitVector().RotatedBy(MathHelper.ToRadians(-10 + 10 * i));
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center - vector * 0.4f, vr * 15,
                        ModContent.ProjectileType<TerratomereBolts>(), (int)(Projectile.damage * 0.4f), Projectile.knockBack, Projectile.owner);
                }
            }

            if (Main.myPlayer == Projectile.owner && Time == (int)(83f * RecoveryCompletionRatio) + 5f) {
                Vector2 vector2 = InitialRotation.ToRotationVector2() * Owner.ActiveItem().shootSpeed / 6f;
                Vector2 position = Projectile.Center + vector2.SafeNormalize(Vector2.UnitY) * 64f;
                int num = Projectile.NewProjectile(Projectile.GetSource_FromThis(), position, vector2, ModContent.ProjectileType<TerratomereBeams>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                if (Main.projectile.IndexInRange(num)) {
                    Main.projectile[num].ai[0] = (Direction == 1f).ToInt();
                    Main.projectile[num].ModProjectile<TerratomereBeams>().ControlPoints = GenerateSlashPoints().ToArray();
                }
            }
        }

        public void CreateSlashSparkleDust() {
            Vector2 vector = InitialRotation.ToRotationVector2();
            Vector2 position = Projectile.Center + (GetSwingOffsetAngle(SwingCompletion) * Direction + InitialRotation).ToRotationVector2() * Main.rand.NextFloat(8f, 66f) + vector * 76f;
            int type = Main.rand.NextBool() ? 267 : 264;
            Dust dust = Dust.NewDustPerfect(position, type, Vector2.Zero);
            dust.color = Color.Lerp(TerratomereEcType.TerraColor1, TerratomereEcType.TerraColor2, Main.rand.NextFloat());
            dust.color = Color.Lerp(dust.color, Color.Yellow, (float)Math.Pow(Main.rand.NextFloat(), 1.63));
            dust.fadeIn = Main.rand.NextFloat(1f, 2f);
            dust.scale = 0.4f;
            dust.velocity = vector * Main.rand.NextFloat(0.5f, 15f);
            dust.noLight = true;
            dust.noGravity = true;
        }

        public override Color? GetAlpha(Color lightColor) {
            return Color.White * Projectile.Opacity;
        }

        public override bool PreDraw(ref Color lightColor) {
            DrawSlash();
            DrawBlade(lightColor);
            return false;
        }

        public float SlashWidthFunction(float completionRatio) {
            return Projectile.scale * 22f;
        }

        public Color SlashColorFunction(float completionRatio) {
            return Color.Lime * Utils.GetLerpValue(0.9f, 0.4f, completionRatio, clamped: true) * Projectile.Opacity;
        }

        public IEnumerable<Vector2> GenerateSlashPoints() {
            for (int i = 0; i < 20; i++) {
                float completion = MathHelper.Lerp(SwingCompletion, SwingCompletionAtStartOfTrail, i / 20f);
                float offsetRot = Math.Abs(Projectile.oldRot[0] - Projectile.oldRot[1]) * 0.8f;
                if (SwingCompletion > RecoveryCompletionRatio) {
                    offsetRot = 0.21f;
                }

                float rots = (GetSwingOffsetAngle(completion) - offsetRot) * Direction + InitialRotation;
                yield return rots.ToRotationVector2() * Projectile.scale * 54f;
            }
        }

        public void DrawSlash() {
            Main.spriteBatch.EnterShaderRegion();
            TerratomereHoldoutProj.PrepareSlashShader(Direction == 1);
            if (SwingCompletionAtStartOfTrail > SwingCompletionRatio) {
                PrimitiveRenderer.RenderTrail(GenerateSlashPoints().ToArray(), new PrimitiveSettings(SlashWidthFunction, SlashColorFunction
                    , (float _) => Projectile.Center, smoothen: true, pixelate: false, GameShaders.Misc["CalamityMod:ExobladeSlash"]), 95);
            }

            Main.spriteBatch.ExitShaderRegion();
        }

        public void DrawBlade(Color lightColor) {
            Texture2D value = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 position = Projectile.Center - Main.screenPosition;
            Vector2 origin = value.Size() * Vector2.UnitY;
            if (Projectile.spriteDirection == -1) {
                origin.X += value.Width;
            }

            SpriteEffects effects = Projectile.spriteDirection != 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Main.spriteBatch.Draw(value, position, null, Projectile.GetAlpha(lightColor), Projectile.rotation, origin, Projectile.scale, effects, 0f);
        }

        public void OnHitHealEffect() {
            if (!Owner.moonLeech) {
                Owner.statLife += 4;
                Owner.HealEffect(4);
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float collisionPoint = 0f;
            Vector2 vector = (InitialRotation + GetSwingOffsetAngle(SwingCompletion)).ToRotationVector2() * new Vector2(Projectile.spriteDirection, 1f);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + vector * Projectile.height * Projectile.scale, Projectile.width * 0.25f, ref collisionPoint);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<GlacialState>(), 30);
            if (target.canGhostHeal) {
                OnHitHealEffect();
            }

            int num = ModContent.ProjectileType<TerratomereSlashCreator>();
            if (Owner.ownedProjectileCounts[num] < 2) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero, num, Projectile.damage, Projectile.knockBack, Projectile.owner, target.whoAmI, Main.rand.NextFloat(MathF.PI * 2f));
                Owner.ownedProjectileCounts[num]++;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<GlacialState>(), 30);
            OnHitHealEffect();
        }
    }
}
