using CalamityMod;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 邪恶粉碎者
    /// </summary>
    internal class EvilSmasherEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "EvilSmasher";
        public override void SetDefaults() {
            Item.SetItemCopySD<EvilSmasher>();
            Item.UseSound = null;
            Item.SetKnifeHeld<EvilSmasherHeld>();
        }

        public override float UseSpeedMultiplier(Player player) => 1f + (player.Calamity().evilSmasherBoost * 0.1f);
        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) => damage *= 1f + player.Calamity().evilSmasherBoost * 0.1f;
        public override void ModifyWeaponKnockback(Player player, ref StatModifier knockback) => knockback *= 1f + (player.Calamity().evilSmasherBoost * 0.1f);
    }

    internal class REvilSmasher : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<EvilSmasher>();
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<EvilSmasherHeld>();
        }
    }

    internal class EvilSmasherHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<EvilSmasher>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "Greentide_Bar";
        public override void SetKnifeProperty() {
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            drawTrailCount = 10;
            drawTrailTopWidth = 50;
            distanceToOwner = -22;
            drawTrailBtommWidth = 0;
            SwingData.baseSwingSpeed = 4f;
            Projectile.width = Projectile.height = 46;
            Length = 66;
        }

        public override bool PreInOwnerUpdate() {
            int evilSmasherBoost = Owner.Calamity().evilSmasherBoost;
            if (Time == 0) {
                SwingData.baseSwingSpeed = 4f + evilSmasherBoost * 0.02f;
            }
            ExecuteAdaptiveSwing(initialMeleeSize: 1 + (0.015f * evilSmasherBoost),
                phase0SwingSpeed: -0.3f, phase1SwingSpeed: 5.2f, phase2SwingSpeed: 3f);
            return base.PreInOwnerUpdate();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.life <= 0 && Owner.Calamity().evilSmasherBoost < 10) {
                Owner.Calamity().evilSmasherBoost += 1;
            }
        }
    }
}
