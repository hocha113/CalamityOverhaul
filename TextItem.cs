using CalamityMod.Items;
using CalamityOverhaul.Content.UIs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul
{
    internal class TextItem : ModItem
    {
        public override string Texture => "CalamityOverhaul/icon";

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
            Item.value = CalamityGlobalItem.Rarity8BuyPrice;
            Item.rare = ItemRarityID.Yellow;
        }

        public override void UpdateInventory(Player player) {
            base.UpdateInventory(player);
        }

        public override void HoldItem(Player player) {
            CWRMod.GameLoadCount.Domp();
        }

        public override bool? UseItem(Player player) {
            //if (player.ownedProjectileCounts[ModContent.ProjectileType<MurasamaEndSkillOrbOnSpan>()] == 0) {
            //    SoundEngine.PlaySound(ModSound.EndSilkOrbSpanSound with { Volume = 0.7f }, player.Center);
            //    if (player.whoAmI == Main.myPlayer) {//同样的，释放衍生弹幕和进行自我充能清零的操作只能交由主人玩家执行
            //        int maxSpanNum = 26;
            //        for (int i = 0; i < maxSpanNum; i++) {
            //            Vector2 spanPos = player.Center + CWRUtils.randVr(1380, 2200);
            //            Vector2 vr = spanPos.To(player.Center + CWRUtils.randVr(180, 320 + 16 * 12)).UnitVector() * 12;
            //            Projectile.NewProjectile(player.parent(), spanPos, vr, ModContent.ProjectileType<MurasamaEndSkillOrbOnSpan>(), 1000, 0, player.whoAmI);
            //        }
            //        //生成一个制造终结技核心效果的弹幕，这样的程序设计是为了减少耦合度
            //        Projectile.NewProjectile(player.parent(), player.Center, Vector2.Zero,
            //        ModContent.ProjectileType<EndSkillEffectStart>(), 1000, 0, player.whoAmI, 0, player.Center.X, player.Center.Y);

            //        CombatText.NewText(player.Hitbox, Color.Gold, "Finishing Blow!!!", true);
            //    }
            //}
            OverhaulTheBible.Instance.Active = true;
            
            return false;
        }
    }
}
