using CalamityMod;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee
{
    internal class EntropicClaymoreHoldoutProj : ModProjectile
    {
        public PrimitiveTrail SlashDrawer;

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

        public override string Texture => CWRConstant.Cay_Wap_Melee + "EntropicClaymore";

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

            Projectile.scale = Utils.GetLerpValue(0f, 0.13f, SwingCompletion, clamped: true) * Utils.GetLerpValue(1f, 0.87f, SwingCompletion, clamped: true) * 0.7f + 0.3f;
            AdjustPlayerValues();
            StickToOwner();
            CreateProjectiles();

            Projectile.rotation = SwordRotation;
            Time += 83 / Projectile.ai[2];
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
            if (Projectile.IsOwnedByLocalPlayer()) {
                if (Projectile.timeLeft == (int)(Projectile.ai[2] * 0.25f)) {
                    Vector2 toMou = Owner.Center.To(Main.MouseWorld);
                    Vector2 pos;
                    int type0 = 10;
                    float damageOffset = 0.5f;
                    switch (Projectile.localAI[0]) {
                        case 0:
                            type0 = ModContent.ProjectileType<EntropicFlechetteSmall>();
                            damageOffset = 0.3f;
                            break;
                        case 1:
                            type0 = ModContent.ProjectileType<EntropicFlechette>();
                            damageOffset = 0.5f;
                            break;
                        case 2:
                            type0 = ModContent.ProjectileType<EntropicFlechetteLarge>();
                            damageOffset = 1;
                            break;
                    }
                    for (int i = 0; i < 9; i++) {
                        float rot = toMou.ToRotation() + MathHelper.ToRadians(-70 + 140 / 9f * i);
                        pos = Projectile.Center + rot.ToRotationVector2() * 130;
                        Projectile.NewProjectile(new EntitySource_ItemUse(Owner, Owner.ActiveItem()), pos, toMou.UnitVector() * 13
                            , type0, (int)(Projectile.damage * damageOffset), Projectile.knockBack, Projectile.owner);
                    }
                }
            }
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
            return Projectile.scale * 12f;
        }

        public Color SlashColorFunction(float completionRatio) {
            return Color.BlanchedAlmond * Utils.GetLerpValue(0.9f, 0.6f, completionRatio, clamped: true) * Projectile.Opacity;
        }

        public IEnumerable<Vector2> GenerateSlashPoints() {
            for (int i = 0; i < 20; i++) {
                float completion = MathHelper.Lerp(SwingCompletion, SwingCompletionAtStartOfTrail, i / 20f);
                float num = Math.Abs(Projectile.oldRot[0] - Projectile.oldRot[1]) * 0.8f;
                if (SwingCompletion > RecoveryCompletionRatio) {
                    num = 0.21f;
                }

                float f = (GetSwingOffsetAngle(completion) - num) * Direction + InitialRotation;
                yield return f.ToRotationVector2() * Projectile.scale * 154f;
            }
        }

        public void DrawSlash() {
            if (SlashDrawer == null) {
                SlashDrawer = new PrimitiveTrail(SlashWidthFunction, SlashColorFunction, null, GameShaders.Misc["CalamityMod:ExobladeSlash"]);
            }

            Main.spriteBatch.EnterShaderRegion();
            PrepareSlashShader(Direction == 1);
            if (SwingCompletionAtStartOfTrail > SwingCompletionRatio) {
                SlashDrawer.Draw(GenerateSlashPoints(), Projectile.Center - Main.screenPosition, 95);
            }

            Main.spriteBatch.ExitShaderRegion();
        }

        public static void PrepareSlashShader(bool flipped) {
            GameShaders.Misc["CalamityMod:ExobladeSlash"].SetShaderTexture(ModContent.Request<Texture2D>(CWRConstant.Masking + "Extra_193"));//"CalamityMod/ExtraTextures/GreyscaleGradients/VoronoiShapes2"
            GameShaders.Misc["CalamityMod:ExobladeSlash"].UseColor(EntropicClaymoreEcType.EntropicColor1);
            GameShaders.Misc["CalamityMod:ExobladeSlash"].UseSecondaryColor(EntropicClaymoreEcType.EntropicColor2);
            GameShaders.Misc["CalamityMod:ExobladeSlash"].Shader.Parameters["fireColor"].SetValue(EntropicClaymoreEcType.EntropicColor1.ToVector3());
            GameShaders.Misc["CalamityMod:ExobladeSlash"].Shader.Parameters["flipped"].SetValue(flipped);
            GameShaders.Misc["CalamityMod:ExobladeSlash"].Apply();
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
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + vector * Projectile.height * Projectile.scale * 2.5f, Projectile.width * 0.25f, ref collisionPoint);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {

        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            OnHitHealEffect();
        }
    }
}
