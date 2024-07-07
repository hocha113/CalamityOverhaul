using CalamityMod.Items;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    internal class TextItem : ModItem
    {
        public override string Texture => "CalamityOverhaul/icon";

        private bool old;
        public override bool IsLoadingEnabled(Mod mod) {
            return false;
        }

        public override void SetDefaults() {
            Item.width = 80;
            Item.height = 80;
            Item.damage = 9999;
            Item.DamageType = DamageClass.Default;
            Item.useAnimation = Item.useTime = 13;
            Item.useTurn = true;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 2.25f;
            Item.UseSound = SoundID.Item1;
            Item.autoReuse = true;
            Item.shootSpeed = 8f;
            Item.shoot = ProjectileID.PurificationPowder;
            Item.value = CalamityGlobalItem.RarityYellowBuyPrice;
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateInventory(Player player) {
            //player.velocity.Domp();
            bool news = player.PressKey(false);
            if (news && !old) {
                player.QuickSpawnItem(player.parent(), Main.HoverItem, Main.HoverItem.stack);
            }
            old = news;

        }

        public override void HoldItem(Player player) {
        }

        public override bool? UseItem(Player player) {
            ////if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
            ////    TungstenRiot.Instance.CloseEvent();
            ////}
            ////else {
            ////    TungstenRiot.Instance.TryStartEvent();
            ////}
            //Projectile.NewProjectile(player.GetSource_FromAI(), player.Center
            //        , new Vector2(13, 0), ModContent.ProjectileType<FrostcrushValariHeld>(), 2, 2, player.whoAmI);
            return true;
        }
    }
}
