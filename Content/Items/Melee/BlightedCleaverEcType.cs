using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Mono.Cecil;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 暴君之刃
    /// </summary>
    internal class BlightedCleaverEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "BlightedCleaver";

        public const float BlightedCleaverMaxRageEnergy = 5000;
        private float rageEnergy {
            get => Item.CWR().MeleeCharge;
            set => Item.CWR().MeleeCharge = value;
        }

        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 78;
            Item.damage = 60;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 26;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 26;
            Item.useTurn = true;
            Item.knockBack = 5.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 88;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
            Item.shoot = ModContent.ProjectileType<BlazingPhantomBlade>();
            Item.shootSpeed = 12f;
            Item.CWR().heldProjType = ModContent.ProjectileType<DefiledGreatswordHeld>();
            Item.SetKnifeHeld<BlightedCleaverHeld>();
        }
    }

    internal class BlightedCleaverHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<BlightedCleaver>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail2";
        public override string gradientTexturePath => CWRConstant.ColorBar + "BlightedCleaver_Bar";
        private float rageEnergy {
            get => Item.CWR().MeleeCharge;
            set => Item.CWR().MeleeCharge = value;
        }
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 66;
            drawTrailHighlight = false;
            canDrawSlashTrail = true;
            distanceToOwner = 40;
            drawTrailBtommWidth = 70;
            drawTrailTopWidth = 20;
            drawTrailCount = 16;
            Length = 110;
            SwingData.starArg = 60;
            SwingData.baseSwingSpeed = 4.2f;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 110;
            SwingData.maxClampLength = 120;
            SwingData.ler1_UpSizeSengs = 0.026f;
            ShootSpeed = 12;
            IgnoreImpactBoxSize = false;
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(5)) {
                Dust.NewDust(Projectile.position, Projectile.width
                    , Projectile.height, DustID.RuneWizard);
            }
        }

        public override bool PreInOwnerUpdate() {
            if (rageEnergy > 0) {
                SwingData.baseSwingSpeed = 12.45f;
            }
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            if (!Item.CWR().closeCombat) {
                if (rageEnergy > 0) {
                    rageEnergy -= Projectile.damage;
                    if (rageEnergy < 0) {
                        rageEnergy = 0;
                    }
                    float adjustedItemScale = Owner.GetAdjustedItemScale(Item);
                    Projectile.NewProjectile(Source, Owner.GetPlayerStabilityCenter()
                        , ShootVelocity, ModContent.ProjectileType<BlazingPhantomBlade>()
                        , (int)(Projectile.damage * 0.75), Projectile.knockBack * 0.5f, Owner.whoAmI
                        , Owner.direction * Owner.gravDir, 32f, adjustedItemScale);
                    NetMessage.SendData(MessageID.PlayerControls, -1, -1, null, Owner.whoAmI);
                }
            }

            Item.CWR().closeCombat = false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Item.CWR().closeCombat = true;
            float addnum = hit.Damage;
            if (addnum > target.lifeMax)
                addnum = 0;
            else {
                addnum *= 1.5f;
            }

            rageEnergy += addnum;
            if (rageEnergy > BlightedCleaverEcType.BlightedCleaverMaxRageEnergy) {
                rageEnergy = BlightedCleaverEcType.BlightedCleaverMaxRageEnergy;
            }
            target.AddBuff(70, 150);

            if (CWRLoad.WormBodys.Contains(target.type)) {
                return;
            }

            int type = ModContent.ProjectileType<HyperBlade>();
            for (int i = 0; i < 4; i++) {
                Vector2 offsetvr = CWRUtils.GetRandomVevtor(-127.5f, -52.5f, 360);
                Vector2 spanPos = target.Center + offsetvr;
                int proj = Projectile.NewProjectile(Source, spanPos,
                    CWRUtils.UnitVector(offsetvr) * -12, type, Item.damage / 2, 0, Owner.whoAmI);
                Main.projectile[proj].timeLeft = 50;
                Main.projectile[proj].usesLocalNPCImmunity = true;
                Main.projectile[proj].localNPCHitCooldown = 15;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(70, 150);
        }
    }
}
