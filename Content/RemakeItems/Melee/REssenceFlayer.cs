using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Particles;
using CalamityMod.Projectiles;
using CalamityMod.Projectiles.Healing;
using CalamityMod.Sounds;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class REssenceFlayer : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<EssenceFlayer>();
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.shoot = ModContent.ProjectileType<EssencePlunder>();
            item.SetKnifeHeld<EssenceFlayerHeld>();
        }
    }

    internal class EssencePlunder : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "EssenceScythe";
        private HashSet<NPC> onHitNPCs = [];
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = Projectile.height = 52;
            Projectile.friendly = true;
            Projectile.penetrate = 6;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.MaxUpdates = 2;
            Projectile.timeLeft = 120 * Projectile.MaxUpdates;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15 * Projectile.MaxUpdates;
            onHitNPCs = [];
            Projectile.alpha = 0;
        }

        public override void PostAI() {
            float rotSpeed = Math.Sign(Projectile.velocity.X) * (Projectile.ai[0] + 0.1f);
            int dir = rotSpeed > 0 ? 1 : -1;
            if (Math.Abs(rotSpeed) < 0.1f) {
                rotSpeed = 0.1f * dir;
            }
            Projectile.rotation += rotSpeed;
            Projectile.ai[0] += 0.01f;
            if (Projectile.ai[0] > 0.5f) {
                Projectile.ai[0] = 0.5f;
            }
            Projectile.velocity *= 0.97f;

            if (Projectile.timeLeft < 120) {
                NPC target = Projectile.Center.FindClosestNPC(850, false, true, onHitNPCs);
                if (target != null) {
                    Projectile.ChasingBehavior(target.Center, 30f);
                    if (!Main.dedServ) {
                        LineParticle spark2 = new LineParticle(Projectile.Center + CWRUtils.randVr(Projectile.width / 2)
                            , -Projectile.velocity * 0.05f, false, 7, 1.7f, Color.AliceBlue);
                        GeneralParticleHandler.SpawnParticle(spark2);
                    }
                }
            }
            else {
                if (Projectile.alpha < 255) {
                    Projectile.alpha += 5;
                }
            }

            Lighting.AddLight(Projectile.Center, Color.DarkGray.ToVector3());
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 180);
            if (target.life <= 0 && target.lifeMax > 5 && Projectile.IsOwnedByLocalPlayer()) {
                CalamityGlobalProjectile.SpawnLifeStealProjectile(Projectile, Main.player[Projectile.owner]
                    , 8, ModContent.ProjectileType<EssenceFlame>(), 2000, 0f);
            }

            if (!onHitNPCs.Contains(target)) {
                SoundStyle sound = CommonCalamitySounds.SwiftSliceSound;
                sound.Pitch = 0.2f;
                sound.MaxInstances = 3;
                sound.Volume = 0.6f;
                SoundEngine.PlaySound(sound, Projectile.Center);
                onHitNPCs.Add(target);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 180);
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
            for (int i = 0; i < 166; i++) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.ShadowbeamStaff);
                Main.dust[dust].velocity = CWRUtils.randVr(2, 14);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = CWRUtils.GetT2DValue(Texture);

            Main.spriteBatch.Draw(value, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation + (Projectile.ai[2] == 0 ? MathHelper.PiOver2 : 0), value.Size() / 2
                , Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);

            Texture2D value2 = CWRAsset.SemiCircularSmear.Value;
            Main.spriteBatch.EnterShaderRegion(BlendState.Additive);
            Main.EntitySpriteDraw(color: Color.AliceBlue * (Projectile.alpha / 255f)
                , origin: value2.Size() * 0.5f, texture: value2
                , position: Projectile.Center - Main.screenPosition
                , sourceRectangle: null, rotation: Projectile.rotation + MathHelper.Pi
                , scale: Projectile.scale * 0.7f, effects: SpriteEffects.None);
            Main.spriteBatch.ExitShaderRegion();

            return false;
        }
    }

    internal class EssenceFlayerHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<EssenceFlayer>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Excelsus_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 20;
            drawTrailTopWidth = 40;
            distanceToOwner = 10;
            drawTrailBtommWidth = 30;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            unitOffsetDrawZkMode = -4;
            Length = 66;
            shootSengs = 0.8f;
            autoSetShoot = true;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 300);
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<GodSlayerInferno>(), 300);
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 6.2f, phase2SwingSpeed: 3f, swingSound: SoundID.Item71);
            return base.PreInOwnerUpdate();
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(3)) {
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.ShadowbeamStaff);
                Main.dust[dust].velocity *= 0f;
            }
        }

        public override void Shoot() => OrigItemShoot();
    }
}
