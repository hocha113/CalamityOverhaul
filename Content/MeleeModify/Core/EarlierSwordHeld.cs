using CalamityMod.Projectiles.Rogue;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace CalamityOverhaul.Content.MeleeModify.Core
{
    internal class EarlierSwordHeld : BaseKnife
    {
        public override int TargetID => Item.type;
        internal static EarliersuitEnum earliersuit;
        internal enum EarliersuitEnum : byte
        {
            None,
            Cactus,//仙人掌，你等着奥，看我扎不扎你就完了
        }

        internal struct Suit
        {
            internal Item head; 
            internal Item body;
            internal Item legs;
            public Suit(Item head, Item body, Item legs) {
                this.head = head;
                this.body = body;
                this.legs = legs;
            }
            public readonly bool SuitIsThis(int[] ids) {
                return head != null && body != null && legs != null && ids.Length == 3 &&
                    head.type == ids[0] && body.type == ids[1] && legs.type == ids[2];
            }
        }

        public override void SetKnifeProperty() {
            //drawTrailHighlight = false;
            //canDrawSlashTrail = true;
            //distanceToOwner = 8;
            //drawTrailBtommWidth = 8;
            //drawTrailTopWidth = 22;
            //drawTrailCount = 4;
            SwingData.baseSwingSpeed = 4;
            Length = 30;
            Projectile.usesLocalNPCImmunity = false;
        }

        internal static void Set(Item item) {
            item.UseSound = null;
            item.SetKnifeHeld<EarlierSwordHeld>();
        }

        public override bool PreInOwnerUpdate() {
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.1f, phase1SwingSpeed: 3.2f, phase2SwingSpeed: 6f, drawSlash: false);
            return base.PreInOwnerUpdate();
        }

        public static EarliersuitEnum InspectionKit(Suit suit) {
            if (suit.SuitIsThis([ItemID.CactusHelmet, ItemID.CactusBreastplate, ItemID.CactusLeggings])) {
                return EarliersuitEnum.Cactus;
            }
            return EarliersuitEnum.None;
        }

        public override void Shoot() {
            earliersuit = InspectionKit(new Suit(Owner.armor[0], Owner.armor[1], Owner.armor[2]));
            if (earliersuit == EarliersuitEnum.Cactus && Item.type == ItemID.CactusSword) {
                int proj = Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity
                    , ModContent.ProjectileType<NastyChollaBol>(), Projectile.damage / 2, Projectile.knockBack, Owner.whoAmI);
                Main.projectile[proj].DamageType = DamageClass.Melee;
            }
        }

        public override void KnifeInitialize() {
            if (TargetID == ItemID.CactusSword) {
                Projectile.width = Projectile.height = 40;
                Length = 50;
                SwingData.minClampLength = 55;
                SwingData.maxClampLength = 60;
                unitOffsetDrawZkMode = -2;
            }
        }
    }
}
