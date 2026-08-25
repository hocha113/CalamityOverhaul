using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 崩牙獠刃，克眼牙齿阔剑。三段常规挥舞接一记崩坏剑身的震撼斩击<br/>
    /// 半刃态轻快但减伤且不再发射碎片；右键修补，闲置缓慢愈合
    /// </summary>
    internal class Shatterfang : ModItem
    {
        public override string Texture => CWRConstant.Item_Melee + "Shatterfang";

        public override void SetDefaults() {
            Item.width = 48;
            Item.height = 64;
            Item.damage = 24;
            Item.DamageType = DamageClass.Melee;
            //节奏由手持弹幕存活期接管(轻拍24f/终结43f/半刃16f)
            Item.useAnimation = Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.knockBack = 6f;
            Item.value = Item.buyPrice(0, 1, 50, 0);
            Item.rare = ItemRarityID.Blue;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.shoot = ModContent.ProjectileType<ShatterfangHeld>();
            Item.shootSpeed = 1f;
            Item.UseSound = null;
        }

        //noMelee 会丢近战词缀，强行标回
        public override bool MeleePrefix() => true;

        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<ShatterfangHeld>()] > 0
                || player.ownedProjectileCounts[ModContent.ProjectileType<ShatterfangRepairHeld>()] > 0) {
                return false;
            }
            if (player.altFunctionUse == 2) {
                //完好无缺时无需修补
                ShatterfangPlayer sp = player.GetModPlayer<ShatterfangPlayer>();
                return sp.Broken || sp.Stability < 1f;
            }
            return true;
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            ShatterfangPlayer sp = player.GetModPlayer<ShatterfangPlayer>();

            if (player.altFunctionUse == 2) {
                Projectile.NewProjectile(source, player.Center, Vector2.Zero,
                    ModContent.ProjectileType<ShatterfangRepairHeld>(), 0, 0f, player.whoAmI,
                    ai0: sp.Broken ? 1f : 0f, ai1: sp.Stability);
                return false;
            }

            //拍号与状态码：state 0=完整 1=半刃 2=完整但此挥收势时疲劳碎裂
            int beat;
            float state;
            if (sp.Broken) {
                beat = sp.ComboCounter % 3;
                state = 1f;
                sp.ConsumeSwing();
            }
            else {
                beat = sp.ComboCounter % 4;
                if (beat == 3) {
                    //终结拍不吃稳固度，崩坏由挥砍模组在爆发末端接管
                    state = 0f;
                    sp.ComboResetTimer = 75;
                    sp.RegenDelay = 55;
                }
                else {
                    state = sp.ConsumeSwing() ? 2f : 0f;
                }
            }
            float swingSign = sp.ComboCounter % 2 == 0 ? 1f : -1f;
            sp.ComboCounter++;

            Vector2 dir = (Main.MouseWorld - player.Center).SafeNormalize(Vector2.UnitX);
            Projectile.NewProjectile(source, player.Center, dir, type, damage, knockback,
                player.whoAmI, beat, swingSign, state);
            return false;
        }

        public override void ModifyWeaponDamage(Player player, ref StatModifier damage) {
            //半刃态基础伤害削弱
            if (player.GetModPlayer<ShatterfangPlayer>().Broken) {
                damage *= 0.62f;
            }
        }

        public override void ModifyWeaponCrit(Player player, ref float crit) => crit += 4;

        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame,
            Color drawColor, Color itemColor, Vector2 origin, float scale) {
            //本地玩家剑身崩坏时物品图标同步换成半刃
            if (!Main.LocalPlayer.GetModPlayer<ShatterfangPlayer>().Broken) {
                return true;
            }
            Texture2D broken = ShatterfangAssets.BrokenBlade?.Value;
            if (broken == null) {
                return true;
            }
            Vector2 center = position + (frame.Size() * 0.5f - origin) * scale;
            spriteBatch.Draw(broken, center, null, drawColor, 0f, broken.Size() * 0.5f, scale, SpriteEffects.None, 0f);
            return false;
        }
    }
}
