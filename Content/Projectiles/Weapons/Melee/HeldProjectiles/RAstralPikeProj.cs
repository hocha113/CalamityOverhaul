using CalamityMod;
using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Dusts;
using CalamityMod.Projectiles.BaseProjectiles;
using CalamityMod.Projectiles.Typeless;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.AstralProj;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class RAstralPikeProj : BaseSpearProjectile
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "Spears/AstralPikeProj";
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<AstralPikeEcType>();
        private Item astralPike => Main.player[Projectile.owner].GetItem();
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = ModContent.GetInstance<TrueMeleeDamageClass>();
            Projectile.timeLeft = 90;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override float InitialSpeed => 3f;
        public override float ReelbackSpeed => 2.4f;
        public override float ForwardSpeed => 0.95f;

        public override Action<Projectile> EffectBeforeReelback => delegate {
            if (astralPike != null) {
                astralPike.initialize();
                astralPike.CWR().ai[0]++;
                if (astralPike.CWR().ai[0] > AstralPikeEcType.ShootPeriod) {
                    SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.2f }, Projectile.Center);
                    const float sengs = 0.25f;
                    Vector2 spanPos = Projectile.Center + Projectile.velocity * 0.5f;
                    Vector2 targetPos = Projectile.velocity.UnitVector() * AstralPikeEcType.InTargetProjToLang + Projectile.Center;
                    for (int i = 0; i < 3; i++)
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), spanPos, Projectile.velocity.RotatedBy(sengs - sengs * i)
                            , ModContent.ProjectileType<AstralPikeBeam>(), Projectile.damage, Projectile.knockBack * 0.25f, Projectile.owner, targetPos.X, targetPos.Y);
                    astralPike.CWR().ai[0] = 0;
                }
            }
        };

        public override void ExtraBehavior() {
            if (astralPike.type != ModContent.ItemType<CalamityMod.Items.Weapons.Melee.AstralPike>() && astralPike.type != ModContent.ItemType<AstralPikeEcType>()) {
                Projectile.Kill();
                return;
            }
            if (Main.rand.NextBool(5))
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height, ModContent.DustType<AstralOrange>(), Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);
            if (hit.Crit) {
                var source = Projectile.GetSource_FromThis();
                for (int i = 0; i < 3; i++) {
                    if (Projectile.owner == Main.myPlayer) {
                        Projectile star = CalamityUtils.ProjectileBarrage(source, Projectile.Center, target.Center, Main.rand.NextBool(), 800f, 800f, 800f, 800f, 10f, ModContent.ProjectileType<AstralStar>(), (int)(Projectile.damage * 0.4), 1f, Projectile.owner, true);
                        if (star.whoAmI.WithinBounds(Main.maxProjectiles)) {
                            star.DamageType = DamageClass.Melee;
                            star.ai[0] = 3f;
                        }
                    }
                }
            }
        }
    }
}
