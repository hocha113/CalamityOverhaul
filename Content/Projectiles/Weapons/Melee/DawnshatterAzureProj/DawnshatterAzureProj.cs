﻿using CalamityMod.NPCs.Yharon;
using CalamityOverhaul.Content.Items.Melee;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class DawnshatterAzureHeld : BaseHeldProj
    {
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<DawnshatterAzure>();
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        protected float HoldoutRangeMin => -24f;
        protected float HoldoutRangeMax => 96f;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7;
            Projectile.hide = true;
        }

        public override void AI() {
            VaultUtils.ClockFrame(ref Projectile.frame, 5, 3);
            SetHeld();
            int duration = Owner.itemAnimationMax;
            if (Projectile.timeLeft > duration) {
                Projectile.timeLeft = duration;
            }
            Projectile.velocity = Vector2.Normalize(Projectile.velocity);
            float halfDuration = duration * 0.5f;
            float progress = Projectile.timeLeft < halfDuration ? Projectile.timeLeft / halfDuration : (duration - Projectile.timeLeft) / halfDuration;
            if (Projectile.timeLeft == duration / 2) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + Projectile.velocity, Projectile.velocity * 15
                , ModContent.ProjectileType<TheEndSun>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            }
            Projectile.Center = Owner.MountedCenter + Vector2.SmoothStep(Projectile.velocity * HoldoutRangeMin, Projectile.velocity * HoldoutRangeMax, progress);
            Projectile.rotation = Projectile.velocity.ToRotation();
            SetDirection();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                target.CWR().TheEndSunOnHitNum = true;
                SoundEngine.PlaySound(Yharon.ShortRoarSound, target.position);
            }
            if (Projectile.numHits < 3) {
                const int maxX = 860;
                const int maxY = 530;
                for (int i = 0; i < 6; i++) {
                    Vector2 spanPos = target.Center + new Vector2(maxX * (i < 3 ? -1 : 1), Main.rand.Next(-maxY, maxY));
                    Vector2 vr = spanPos.To(target.Center).UnitVector() * 25;
                    int proj = Projectile.NewProjectile(Projectile.GetSource_FromThis(), spanPos, vr
                    , ModContent.ProjectileType<TheDaybreak2>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0, 32);
                    Main.projectile[proj].timeLeft = 120;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float rot = Projectile.rotation + MathHelper.PiOver4 + (Owner.direction > 0 ? 0 : MathHelper.PiOver2);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = VaultUtils.GetOrig(texture, 4);
            Main.EntitySpriteDraw(texture, drawPosition, texture.GetRectangle(Projectile.frame, 4), Projectile.GetAlpha(lightColor)
                , rot, origin, Projectile.scale * 0.7f, Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}
