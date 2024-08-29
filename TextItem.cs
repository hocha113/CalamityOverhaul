using CalamityMod.Items;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
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
            return true;
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
            //if (TungstenRiot.Instance.TungstenRiotIsOngoing) {
            //    TungstenRiot.Instance.CloseEvent();
            //}
            //else {
            //    TungstenRiot.Instance.TryStartEvent();
            //}

            //int maxProjSanShootNum = 22;
            //int type = ModContent.ProjectileType<Probe>();
            //for (int i = 0; i < maxProjSanShootNum; i++) {
            //    Projectile.NewProjectile(player.GetSource_FromAI()
            //            , player.Center, (MathHelper.TwoPi / maxProjSanShootNum * i).ToRotationVector2() * Main.rand.Next(3, 16)
            //            , type, 42, 0f, Main.myPlayer, 0, Main.rand.Next(30, 60));
            //}

            int maxSpanNum = 13 + 10;
            for (int i = 0; i < maxSpanNum; i++) {
                Vector2 spanPos = player.Center + CWRUtils.randVr(1380, 2200);
                Vector2 vr = spanPos.To(player.Center + CWRUtils.randVr(180, 320 + 10 * 12)).UnitVector() * 12;
                Projectile.NewProjectile(player.parent(), spanPos
                    , vr, ModContent.ProjectileType<MurasamaEndSkillOrbOnSpan>()
                    , (int)(100 * 0.7f), 0, player.whoAmI);
            }
            return true;
        }
    }
}
