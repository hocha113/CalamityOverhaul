using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Buffs;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 惊惧魂刃
    /// </summary>
    internal class TerrorBladeEcType : EctypeItem, ILoader
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "TerrorBlade";
        public const float TerrorBladeMaxRageEnergy = 5000;
        private static Asset<Texture2D> rageEnergyTopAsset;
        private static Asset<Texture2D> rageEnergyBarAsset;
        private static Asset<Texture2D> rageEnergyBackAsset;
        void ILoader.SetupData() {
            if (!Main.dedServ) {
                rageEnergyTopAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "FrightEnergyChargeTop");
                rageEnergyBarAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "FrightEnergyChargeBar");
                rageEnergyBackAsset = CWRUtils.GetT2DAsset(CWRConstant.UI + "FrightEnergyChargeBack");
            }
        }
        void ILoader.UnLoadData() {
            rageEnergyTopAsset = null;
            rageEnergyBarAsset = null;
            rageEnergyBackAsset = null;
        }
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.width = 88;
            Item.damage = 560;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 18;
            Item.useTime = 20;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 8.5f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.height = 80;
            Item.shoot = ModContent.ProjectileType<RTerrorBeam>();
            Item.shootSpeed = 20f;
            Item.value = CalamityGlobalItem.RarityPureGreenBuyPrice;
            Item.rare = ModContent.RarityType<PureGreen>();
            Item.CWR().heldProjType = ModContent.ProjectileType<TerrorBladeHeld>();
            Item.SetKnifeHeld<TerrorBladeHeld2>();
        }

        public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI) {
            Item.DrawItemGlowmaskSingleFrame(spriteBatch, rotation, ModContent.Request<Texture2D>(CWRConstant.Cay_Wap_Melee + "TerrorBladeGlow", (AssetRequestMode)2).Value);
        }

        public static void DrawRageEnergyChargeBar(Player player, float alp) {
            Item item = player.ActiveItem();
            if (item.IsAir) {
                return;
            }

            Texture2D rageEnergyTop = rageEnergyTopAsset.Value;
            Texture2D rageEnergyBar = rageEnergyBarAsset.Value;
            Texture2D rageEnergyBack = rageEnergyBackAsset.Value;

            float slp = 3;
            int offsetwid = 4;
            Vector2 drawPos = CWRUtils.WDEpos(player.Center + new Vector2(rageEnergyBar.Width / -2 * slp, 135));
            Rectangle backRec = new Rectangle(offsetwid, 0, (int)((rageEnergyBar.Width - offsetwid * 2) * (item.CWR().MeleeCharge / TerrorBladeMaxRageEnergy)), rageEnergyBar.Height);

            Main.EntitySpriteDraw(rageEnergyBack, drawPos, null, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(rageEnergyBar, drawPos + new Vector2(offsetwid, 0) * slp, backRec, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(rageEnergyTop, drawPos, null, Color.White * alp, 0, Vector2.Zero, slp, SpriteEffects.None, 0);
        }
    }

    internal class TerrorBladeHeld2 : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<TerrorBlade>();
        public override string trailTexturePath => CWRConstant.Masking + "MotionTrail3";
        public override string gradientTexturePath => CWRConstant.ColorBar + "TerrorBlade_Bar";
        public override string GlowTexturePath => CWRConstant.Cay_Wap_Melee + "TerrorBladeGlow";
        private float rageEnergy { get => Item.CWR().MeleeCharge; set => Item.CWR().MeleeCharge = value; }
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 86;
            canDrawSlashTrail = true;
            distanceToOwner = -40;
            drawTrailBtommWidth = 30;
            drawTrailTopWidth = 80;
            drawTrailCount = 16;
            Length = 110;
            unitOffsetDrawZkMode = -8;
            overOffsetCachesRoting = MathHelper.ToRadians(8);
            SwingData.starArg = 80;
            SwingData.ler1_UpLengthSengs = 0.1f;
            SwingData.minClampLength = 120;
            SwingData.maxClampLength = 130;
            SwingData.ler1_UpSizeSengs = 0.056f;
            ShootSpeed = 20;
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.RedTorch);
            }
        }

        public override void Shoot() {
            if (Item.CWR().closeCombat) {
                Item.CWR().closeCombat = false;
                return;
            }
            rageEnergy -= Projectile.damage / 10;
            if (rageEnergy < 0) {
                rageEnergy = 0;
            }
            Projectile.NewProjectile(Source, ShootSpanPos, ShootVelocity, ModContent.ProjectileType<RTerrorBeam>()
                , Projectile.damage, Projectile.knockBack, Owner.whoAmI, (rageEnergy > 0) ? 1 : 0);
        }

        public override bool PreInOwnerUpdate() {

            return base.PreInOwnerUpdate();
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Item.CWR().closeCombat = true;
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 600);

            bool oldcharge = rageEnergy > 0;//与OnHitPvp一致，用于判断能量出现的那一刻
            rageEnergy += hit.Damage / 5;
            bool charge = rageEnergy > 0;
            if (charge != oldcharge) {
                SoundEngine.PlaySound(CWRSound.Pecharge with { Volume = 0.4f }, Owner.Center);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            Item.CWR().closeCombat = true;
            target.AddBuff(ModContent.BuffType<SoulBurning>(), 600);

            bool oldcharge = rageEnergy > 0;//与OnHitPvp一致，用于判断能量出现的那一刻
            rageEnergy += info.Damage / 5;
            bool charge = rageEnergy > 0;
            if (charge != oldcharge) {
                SoundEngine.PlaySound(CWRSound.Pecharge with { Volume = 0.4f }, Owner.Center);
            }
        }
    }
}
