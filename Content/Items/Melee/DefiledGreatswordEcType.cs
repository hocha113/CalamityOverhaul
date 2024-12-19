using CalamityMod.Buffs.StatBuffs;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 神殇巨剑
    /// </summary>
    internal class DefiledGreatswordEcType : EctypeItem, ICWRLoader
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "DefiledGreatsword";

        public const float DefiledGreatswordMaxRageEnergy = 15000;

        private static Asset<Texture2D> rageEnergyTopAsset;
        private static Asset<Texture2D> rageEnergyBarAsset;
        private static Asset<Texture2D> rageEnergyBackAsset;
        void ICWRLoader.SetupData() {
            if (!Main.dedServ) {
                rageEnergyTopAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "RageEnergyTop");
                rageEnergyBarAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "RageEnergyBar");
                rageEnergyBackAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "RageEnergyBack");
            }
        }
        void ICWRLoader.UnLoadData() {
            rageEnergyTopAsset = null;
            rageEnergyBarAsset = null;
            rageEnergyBackAsset = null;
        }

        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 102;
            Item.damage = 112;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 18;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = 18;
            Item.useTurn = true;
            Item.knockBack = 7.5f;
            Item.UseSound = null;
            Item.autoReuse = true;
            Item.height = 102;
            Item.value = CalamityGlobalItem.RarityTurquoiseBuyPrice;
            Item.rare = ModContent.RarityType<Turquoise>();
            Item.shoot = ModContent.ProjectileType<BlazingPhantomBlade>();
            Item.shootSpeed = 12f;
            Item.CWR().heldProjType = ModContent.ProjectileType<DefiledGreatswordHeld>();
            Item.SetKnifeHeld<DefiledGreatswordHeld2>();
        }

        public static void DrawRageEnergyChargeBar(Player player, float alp, float meleeCharge, float maxCharge) {
            Item item = player.GetItem();
            if (item.IsAir) {
                return;
            }
            Texture2D rageEnergyTop = rageEnergyTopAsset.Value;
            Texture2D rageEnergyBar = rageEnergyBarAsset.Value;
            Texture2D rageEnergyBack = rageEnergyBackAsset.Value;
            float slp = 3;
            int offsetwid = 4;
            if (item.type == ModContent.ItemType<BlightedCleaverEcType>() || item.type == ModContent.ItemType<BlightedCleaver>()) {
                maxCharge = BlightedCleaverEcType.BlightedCleaverMaxRageEnergy;
            }
            Vector2 drawPos = player.GetPlayerStabilityCenter() + new Vector2(rageEnergyBar.Width / -2 * slp, 135) - Main.screenPosition;
            Rectangle backRec = new(offsetwid, 0, (int)((rageEnergyBar.Width - (offsetwid * 2)) * (meleeCharge / maxCharge)), rageEnergyBar.Height);

            Main.EntitySpriteDraw(rageEnergyBack, drawPos, null, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(rageEnergyBar, drawPos + (new Vector2(offsetwid, 0) * slp)
                , backRec, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(rageEnergyTop, drawPos, null, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);
        }
    }

    internal class DefiledGreatswordHeld2 : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<DefiledGreatsword>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail4";
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
            drawTrailTopWidth = 70;
            drawTrailCount = 16;
            Length = 130;
            unitOffsetDrawZkMode = -6;
            SwingData.starArg = 60;
            SwingData.baseSwingSpeed = 4.2f;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 130;
            SwingData.maxClampLength = 140;
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
            ExecuteAdaptiveSwing(phase0SwingSpeed: 0.3f, phase1SwingSpeed: 3.2f, phase2SwingSpeed: 5f);
            return base.PreInOwnerUpdate();
        }

        public override void Shoot() {
            int damage = Projectile.damage;
            Player player = Owner;

            if (!Item.CWR().closeCombat) {
                rageEnergy -= damage * 1.25f;
                if (rageEnergy < 0) {
                    rageEnergy = 0;
                }

                if (rageEnergy == 0) {
                    float adjustedItemScale = player.GetAdjustedItemScale(Item);
                    float ai1 = 40;
                    float velocityMultiplier = 2;
                    Projectile.NewProjectile(Source, player.GetPlayerStabilityCenter(), ShootVelocity * velocityMultiplier
                        , ModContent.ProjectileType<BlazingPhantomBlade>(), (int)(damage * 0.75)
                        , Projectile.knockBack * 0.5f, player.whoAmI, player.direction * player.gravDir, ai1, adjustedItemScale);
                }
                else {
                    float adjustedItemScale = player.GetAdjustedItemScale(Item);
                    for (int i = 0; i < 3; i++) {
                        float ai1 = 40 + i * 8;
                        float velocityMultiplier = 1f - i / (float)3;
                        Projectile.NewProjectile(Source, player.GetPlayerStabilityCenter()
                            , ShootVelocity.RotatedByRandom(MathHelper.TwoPi) * velocityMultiplier
                            , ModContent.ProjectileType<BlazingPhantomBlade>(), damage
                            , Projectile.knockBack * 0.5f, player.whoAmI, player.direction * player.gravDir, ai1, adjustedItemScale);
                    }
                }
            }
            Item.CWR().closeCombat = false;
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player player = Owner;
            Item.CWR().closeCombat = true;
            float addnum = hit.Damage;
            if (addnum > target.lifeMax) {
                addnum = 0;
            }
            else {
                addnum *= 2;
            }

            rageEnergy += addnum;
            if (rageEnergy > DefiledGreatswordEcType.DefiledGreatswordMaxRageEnergy) {
                rageEnergy = DefiledGreatswordEcType.DefiledGreatswordMaxRageEnergy;
            }

            player.AddBuff(ModContent.BuffType<BrutalCarnage>(), 300);
            target.AddBuff(70, 150);

            if (CWRLoad.WormBodys.Contains(target.type) && !Main.rand.NextBool(3)) {
                return;
            }
            int type = ModContent.ProjectileType<SunlightBlades>();
            int randomLengs = Main.rand.Next(150);
            for (int i = 0; i < 3; i++) {
                Vector2 offsetvr = CWRUtils.GetRandomVevtor(-15, 15, 900 + randomLengs);
                Vector2 spanPos = target.Center + offsetvr;
                int proj1 = Projectile.NewProjectile(
                    player.FromObjectGetParent(), spanPos,
                    offsetvr.UnitVector() * -13,
                    type, Item.damage - 50, 0, player.whoAmI);
                Vector2 offsetvr2 = CWRUtils.GetRandomVevtor(165, 195, 900 + randomLengs);
                Vector2 spanPos2 = target.Center + offsetvr2;
                int proj2 = Projectile.NewProjectile(
                    player.FromObjectGetParent(), spanPos2,
                    offsetvr2.UnitVector() * -13, type,
                    Item.damage - 50, 0, player.whoAmI);
                Main.projectile[proj1].extraUpdates += 1;
                Main.projectile[proj2].extraUpdates += 1;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Player player = Owner;
            Item.CWR().closeCombat = true;
            int type = ModContent.ProjectileType<SunlightBlades>();
            int offsety = 180;
            for (int i = 0; i < 16; i++) {
                Vector2 offsetvr = new(-600, offsety);
                Vector2 spanPos = offsetvr + player.Center;
                _ = Projectile.NewProjectile(
                    player.FromObjectGetParent(),
                    spanPos,
                    offsetvr.UnitVector() * -13,
                    type,
                    Item.damage / 3,
                    0,
                    player.whoAmI
                    );
                Vector2 offsetvr2 = new(600, offsety);
                Vector2 spanPos2 = offsetvr + player.Center;
                _ = Projectile.NewProjectile(
                    player.FromObjectGetParent(),
                    spanPos2,
                    offsetvr2.UnitVector() * -13,
                    type,
                    Item.damage / 3,
                    0,
                    player.whoAmI
                    );

                offsety -= 20;
            }
            player.AddBuff(ModContent.BuffType<BrutalCarnage>(), 300);
            target.AddBuff(70, 150);
        }
    }
}
