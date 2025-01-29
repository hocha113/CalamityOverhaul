using CalamityMod.Buffs.StatDebuffs;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.RemakeItems.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 痛击者
    /// </summary>
    internal class BasherEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "Basher";
        public override void SetDefaults() {
            Item.SetItemCopySD<Basher>();
            SetDefaultsFunc(Item);
        }
        public static void SetDefaultsFunc(Item Item) {
            Item.UseSound = null;
            Item.useTime = 90;
            Item.damage = 65;
            Item.SetKnifeHeld<BasherHeld>();
        }
    }

    internal class RBasher : ItemOverride
    {
        public override int TargetID => ModContent.ItemType<Basher>();
        public override int ProtogenesisID => ModContent.ItemType<BasherEcType>();
        public override void SetDefaults(Item item) => BasherEcType.SetDefaultsFunc(item);
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

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: -0.3f, phase1SwingSpeed: 5.2f, phase2SwingSpeed: 3f, phase2MeleeSizeIncrement: 0, drawSlash: false);
            return base.PreInOwnerUpdate();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(ModContent.BuffType<Irradiated>(), 300);
            target.AddBuff(BuffID.Poisoned, 60);
        }
    }
}
