using CalamityMod.Buffs.DamageOverTime;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    internal class TitanArmEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TitanArm";
        public override void SetDefaults() {
            Item.SetItemCopySD<TitanArm>();
            SetDefaultsFunc(Item);
        }
        public override void ModifyWeaponCrit(Player player, ref float crit) => crit = 100;
        public static void SetDefaultsFunc(Item Item) {
            Item.UseSound = null;
            Item.useTime = 100;
            Item.SetKnifeHeld<TitanArmHeld>();
        }
    }

    internal class RTitanArm : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<TitanArm>();
        public override int ProtogenesisID => ModContent.ItemType<TitanArmEcType>();
        public override void SetDefaults(Item item) => TitanArmEcType.SetDefaultsFunc(item);
    }

    internal class TitanArmHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<TitanArm>();
        private List<NPC> onHitNpcs = [];
        public override void SetKnifeProperty() {
            canDrawSlashTrail = false;
            drawTrailCount = 10;
            drawTrailTopWidth = 50;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Length = 66;
        }

        public override bool PreInOwnerUpdate() {
            if (!DownLeft && !onSound) {
                Projectile.Kill();
            }

            ExecuteAdaptiveSwing(phase0SwingSpeed: -0.1f, phase1SwingSpeed: 5.2f, phase2SwingSpeed: 3f, phase2MeleeSizeIncrement: 0, drawSlash: false);
            return base.PreInOwnerUpdate();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);
            if (!onHitNpcs.Contains(target)) {
                MuraBreakerSlash.StrikeToFly(new Vector2(hit.HitDirection * 8, -6), target, Owner, 6);
                onHitNpcs.Add(target);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<AstralInfectionDebuff>(), 300);
        }
    }
}
