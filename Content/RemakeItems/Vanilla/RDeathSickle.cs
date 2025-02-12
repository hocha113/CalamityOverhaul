using CalamityMod;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Vanilla
{
    /// <summary>
    /// 死神镰刀
    /// </summary>
    internal class RDeathSickle : ItemOverride
    {
        public override int TargetID => ItemID.DeathSickle;
        public override bool IsVanilla => true;
        private int swingIndex = 0;
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<DeathSickleHeld>();
        }

        public override bool? CanUseItem(Item item, Player player)
            => player.ownedProjectileCounts[item.shoot] <= 0
            && player.ownedProjectileCounts[ModContent.ProjectileType<DeathSickleThrowable>()] <= 0;

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (++swingIndex > 6) {
                type = ModContent.ProjectileType<DeathSickleThrowable>();
                Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI);
                swingIndex = 0;
                return false;
            }
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback
                , player.whoAmI, swingIndex % 2 == 0 ? 0 : 1, swingIndex + 1);
            return false;
        }
    }

    internal class DeathSickleHeld : BaseKnife
    {
        public override int TargetID => ItemID.DeathSickle;
        public override string gradientTexturePath => CWRConstant.ColorBar + "ExaltedOathblade_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 14;
            drawTrailTopWidth = 36;
            distanceToOwner = 10;
            drawTrailBtommWidth = 20;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            unitOffsetDrawZkMode = -4;
            Length = 66;
            shootSengs = 0.8f;
            autoSetShoot = true;
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: -0.3f
                , phase1SwingSpeed: 5.2f, phase2SwingSpeed: 3f, swingSound: SoundID.Item71);
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, ProjectileID.DeathSickle, Projectile.damage, 2);
        }
    }

    internal class DeathSickleThrowable : BaseThrowable
    {
        public override string Texture => CWRConstant.Placeholder;
        public override Texture2D TextureValue => TextureAssets.Item[ItemID.DeathSickle].Value;
        private bool outFive;
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 7;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetThrowable() {
            Projectile.DamageType = DamageClass.Melee;
            HandOnTwringMode = -66;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 8;
            Projectile.scale = 1.5f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            return VaultUtils.CircleIntersectsRectangle(Projectile.Center, 75, targetHitbox);
        }

        public override bool PreThrowOut() {
            outFive = true;
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, Owner.Center);
            return true;
        }

        public override void FlyToMovementAI() {
            base.FlyToMovementAI();
            float addSpeedBaf = Projectile.ai[2] * 0.01f;
            if (addSpeedBaf > 1.45f) {
                addSpeedBaf = 1.45f;
            }
            Projectile.rotation += (MathHelper.PiOver4 / 4f + MathHelper.PiOver4 / 2f *
                Math.Clamp(CurrentThrowProgress * 2f, 0, 1)) * Math.Sign(Projectile.velocity.X) * addSpeedBaf;

            if (Projectile.timeLeft < 60) {
                Projectile.Center = Vector2.Lerp(Projectile.Center, Owner.Center, 0.1f);
            }

            Projectile.ai[2]++;
        }

        public override void DrawThrowable(Color lightColor) {
            Vector2 orig = TextureValue.Size() / 2;
            SpriteEffects spriteEffects = Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
            GameShaders.Armor.ApplySecondary(ContentSamples.CommonlyUsedContentSamples.ColorOnlyShaderIndex, Owner, null);

            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + new Vector2(33, 33);
                Color color = Color.DarkBlue * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(TextureValue, drawPos, null, color * Projectile.Opacity * 0.65f
                    , Projectile.oldRot[k], orig, Projectile.scale, spriteEffects, 0);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None
                , RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            Main.instance.LoadItem(ItemID.DeathSickle);
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1)
                , orig, Projectile.scale, spriteEffects, 0);


            if (outFive) {
                Texture2D value = CWRAsset.SemiCircularSmear.Value;
                Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
                Main.EntitySpriteDraw(color: Color.BlueViolet * 0.9f
                    , origin: value.Size() * 0.5f, texture: value, position: Projectile.Center - Main.screenPosition
                    , sourceRectangle: null, rotation: Projectile.rotation - CWRUtils.PiOver5 + MathHelper.Pi
                    , scale: Projectile.scale * 0.6f, effects: SpriteEffects.None);
                Main.spriteBatch.ExitShaderRegion();
            }
        }
    }
}
