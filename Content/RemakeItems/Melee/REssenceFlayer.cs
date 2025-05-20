using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Particles;
using CalamityMod.Projectiles;
using CalamityMod.Projectiles.Healing;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class REssenceFlayer : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<EssenceFlayer>();
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.damage = 180;
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
                    if (Projectile.velocity.Length() < 6) {
                        Projectile.velocity = Projectile.Center.To(target.Center).UnitVector() * 26;
                    }
                    Projectile.SmoothHomingBehavior(target.Center, 1.01f, 0.2f);
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

            if (Projectile.numHits == 0) {
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Projectile.FromObjectGetParent(), Projectile.Center, (MathHelper.TwoPi / 3 * i).ToRotationVector2() * 6
                        , ModContent.ProjectileType<EssenceEnergy>(), Projectile.damage / 3, 0, Projectile.owner, 0, Projectile.whoAmI);
                }
                Projectile.numHits++;
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
            Texture2D value = TextureAssets.Projectile[Type].Value;

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

    internal class EssenceEnergy : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.Placeholder;
        private Trail Trail;
        private const int MaxPos = 40;
        private Vector2 offset;
        private Vector2 origInHomePos;
        public override void SetDefaults() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.height = 54;
            Projectile.width = 54;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
            Projectile.extraUpdates = 1;
            Projectile.localNPCHitCooldown = 10;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.alpha = 255;
        }
        public override bool ShouldUpdatePosition() => false;
        public override void AI() {
            Projectile homeProj;

            if (Projectile.localAI[0] == 0) {
                Projectile.oldPos = new Vector2[MaxPos];
                for (int i = 0; i < MaxPos; i++) {
                    Projectile.oldPos[i] = Projectile.Center + Projectile.velocity.UnitVector() * 160;
                }

                homeProj = CWRUtils.GetProjectileInstance((int)Projectile.ai[1]);
                if (homeProj.Alives() && homeProj.type == ModContent.ProjectileType<EssencePlunder>()) {
                    origInHomePos = homeProj.Center;
                }

                Projectile.localAI[0] = 1;
            }

            for (int i = 0; i < MaxPos - 1; i++) {
                Projectile.oldPos[i] = Projectile.oldPos[i + 1];
            }
            Projectile.oldPos[MaxPos - 1] = Projectile.Center + Projectile.velocity.UnitVector() * 160;

            Trail ??= new Trail(Projectile.oldPos, GetWeithFunc, GetColorFunc);
            Trail.TrailPositions = Projectile.oldPos;

            Projectile.velocity = Projectile.velocity.RotatedBy(0.11f) * 0.9f;
            offset += Projectile.velocity;

            homeProj = CWRUtils.GetProjectileInstance((int)Projectile.ai[1]);
            if (homeProj.Alives() && homeProj.type == ModContent.ProjectileType<EssencePlunder>()) {
                origInHomePos = Vector2.Lerp(origInHomePos, homeProj.Center, 0.2f);
            }
            else {
                NPC targetNPC = Main.player[Projectile.owner].Center.FindClosestNPC(900);
                if (targetNPC != null) {
                    origInHomePos = Vector2.Lerp(origInHomePos, targetNPC.Center, 0.1f);
                }
            }

            Projectile.Center = origInHomePos + offset;

            if (Projectile.timeLeft < 10) {
                Projectile.alpha -= 25;
            }
        }

        private Color GetColorFunc(Vector2 _) => Color.DarkBlue * (Projectile.alpha / 255f);
        private float GetWeithFunc(float sengs) => Projectile.scale * 110f * (1 - sengs);

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Trail == null) {
                return;
            }
            Effect effect = EffectLoader.GradientTrail.Value;
            effect.Parameters["transformMatrix"].SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"].SetValue((float)Main.timeForVisualEffects * -0.08f);
            effect.Parameters["uTimeG"].SetValue(Main.GlobalTimeWrappedHourly * -0.2f);
            effect.Parameters["udissolveS"].SetValue(1f);
            effect.Parameters["uBaseImage"].SetValue(CWRUtils.GetT2DValue(CWRConstant.Masking + "StarTexture"));
            effect.Parameters["uFlow"].SetValue(CWRAsset.Airflow.Value);
            effect.Parameters["uGradient"].SetValue(CWRUtils.GetT2DValue(CWRConstant.ColorBar + "DarklightGreatsword_Bar"));
            effect.Parameters["uDissolve"].SetValue(CWRAsset.Extra_193.Value);

            Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
            Trail?.DrawTrail(effect);
            Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
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

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(initialMeleeSize: 1, phase0SwingSpeed: 0.3f
                , phase1SwingSpeed: 6.2f, phase2SwingSpeed: 3f, swingSound: SoundID.Item71);
            return base.PreInOwner();
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
