using CalamityOverhaul.Content;
using CalamityOverhaul.Content.ADV;
using CalamityOverhaul.Content.ADV.Scenarios;
using CalamityOverhaul.Content.ADV.Scenarios.SupCal.End.EternalBlazingNows;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
#if DEBUG
    internal class TestProj : ModProjectile
    {
        public override string Texture => "CalamityOverhaul/icon";
        public override LocalizedText DisplayName => ItemLoader.GetItem(ModContent.ItemType<TestItem>()).DisplayName;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 66;
            Projectile.timeLeft = 400;
        }

        public override void AI() {
            Projectile.ai[0]++;
            if (Projectile.ai[0] == 90) {
                ScenarioManager.Reset<EternalBlazingNow>();
                ScenarioManager.Start<EternalBlazingNow>();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            return false;
        }
    }

    internal class TestItem : ModItem
    {
        public override string Texture => "CalamityOverhaul/icon";
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
            Item.value = 7;
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateInventory(Player player) {
            
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI) {
            return false;
        }

        public override bool AltFunctionUse(Player player) {
            return true;
        }

        public override void HoldItem(Player player) {
        }

        public override bool? UseItem(Player player) {
            return true;
        }
    }
#endif
}
