using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RBasher : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Basher>();
        public override void SetDefaults(Item item) => SetDefaultsFunc(item);
        public static void SetDefaultsFunc(Item Item) {
            Item.UseSound = null;
            Item.useTime = 90;
            Item.damage = 65;
            Item.SetKnifeHeld<BasherHeld>();
        }
    }

    internal class BasherHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<Basher>();
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

        public override bool PreInOwner() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: -0.3f, phase1SwingSpeed: 5.2f, phase2SwingSpeed: 3f, phase2MeleeSizeIncrement: 0, drawSlash: false);
            return base.PreInOwner();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Irradiated>(), 300);
            target.AddBuff(BuffID.Poisoned, 60);
        }
    }
}
