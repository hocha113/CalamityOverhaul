using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Extras;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Magic.HeldProjs
{
    internal class TheUpiSteleHeldProj : BaseGun
    {
        public override string Texture => CWRConstant.Item_Magic + "TheUpiStele";
        public override int targetCayItem => ModContent.ItemType<TheUpiStele>();
        public override int targetCWRItem => ModContent.ItemType<TheUpiStele>();
        public override void SetRangedProperty() {
            Projectile.DamageType = DamageClass.Magic;
            GunPressure = 0;
            HandDistance = 25;
            HandDistanceY = -5;
            Recoil = 0;
            ArmRotSengsFrontNoFireOffset = 13;
            AngleFirearmRest = 0;
        }

        public override void FiringIncident() {
            base.FiringIncident();
            if (onFire) {
                Projectile.Center = Owner.MountedCenter + (DirSign > 0 ? new Vector2(13, -20) : new Vector2(-13, -20));
                Projectile.rotation = DirSign > 0 ? 0 : MathHelper.Pi;
                ArmRotSengsBack = ArmRotSengsFront = (MathHelper.PiOver2 + (DirSign > 0 ? MathHelper.ToRadians(60) : MathHelper.ToRadians(120))) * DirSign;
            }
        }

        public override void FiringShoot() {
            if (Owner.CheckMana(Item)) {
                List<NPC> npcs = new List<NPC>();
                int dot = 0;
                while (npcs.Count < 5 && dot < 10) {
                    foreach (NPC n in Main.npc) {
                        if ((n.Center.Distance(Projectile.Center) - (n.width / 2)) < 280 && !n.friendly && !n.dontTakeDamage) {
                            npcs.Add(n);
                        }
                        if (npcs.Count > 5) {
                            break;
                        }
                    }
                    dot++;
                }

                if (npcs.Count > 0) {
                    foreach (NPC n in npcs) {
                        Vector2 vr = Projectile.Center.To(n.Center).RotatedByRandom(0.056).UnitVector() * Main.rand.Next(11, 13);
                        Projectile.NewProjectile(Source, Projectile.Center, vr, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                    }
                }
                else {
                    for (int i = 0; i < 5; i++) {
                        Vector2 vr = CWRUtils.randVr(7, 13);
                        Projectile.NewProjectile(Source, Projectile.Center, vr, Item.shoot, WeaponDamage, WeaponKnockback, Owner.whoAmI);
                    }
                }

                Owner.statMana -= Item.mana;
            }
        }
    }
}
