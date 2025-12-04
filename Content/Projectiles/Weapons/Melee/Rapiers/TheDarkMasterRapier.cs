using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class TheDarkMasterRapier : BaseRapiers
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TheDarkMaster";
        public override string GlowPath => CWRConstant.Cay_Wap_Melee + "TheDarkMaster";
        public override void SetRapiers() {
            PremanentToSkialthRot = 10;
            overHitModeing = 73;
            SkialithVarSpeedMode = 3;
            StabbingSpread = 0.15f;
            ShurikenOut = SoundID.Item71 with { Pitch = 0.7f };
        }

        public override void ExtraShoot() {
            if (HitNPCs.Count > 0) {
                if (Owner.ownedProjectileCounts[ModContent.ProjectileType<SemberDarkMasterClone>()] <= 0
                    && !Owner.HasBuff(BuffID.Darkness)
                    && Owner.CWR().DontHasSemberDarkMasterCloneTime <= 0) {
                    SoundEngine.PlaySound(SoundID.Item71, Owner.Center);
                    for (int i = 0; i < 3; i++) {
                        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Owner.Center.X, Owner.Center.Y
                            , 0, 0, ModContent.ProjectileType<SemberDarkMasterClone>(), Projectile.damage, Projectile.knockBack, Owner.whoAmI, i);
                    }
                }
                return;
            }
            Item.Initialize();
            if (++Item.CWR().ai[0] > 1) {
                int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.velocity * Main.rand.NextFloat(10, 16)
                , ModContent.ProjectileType<DarkMasterBeam>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
                Item.CWR().ai[0] = 0;
            }
        }
    }
}
