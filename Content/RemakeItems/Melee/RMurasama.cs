using CalamityMod;
using CalamityMod.Items;
using CalamityMod.Rarities;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.MurasamaProj;
using CalamityOverhaul.Content.RemakeItems.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class RMurasama : BaseRItem
    {
        public override int TargetID => ModContent.ItemType<CalamityMod.Items.Weapons.Melee.Murasama>();
        public override int ProtogenesisID => ModContent.ItemType<MurasamaEcType>();
        public int frameCounter = 0;
        public int frame = 0;

        public override void SetStaticDefaults() => Main.RegisterItemAnimation(TargetID, new DrawAnimationVertical(5, 14));
        public override void SetDefaults(Item item) {
            item.height = 134;
            item.width = 90;
            item.damage = MurasamaEcType.GetStartDamage;
            item.DamageType = ModContent.GetInstance<TrueMeleeNoSpeedDamageClass>();
            item.noMelee = true;
            item.noUseGraphic = true;
            item.channel = true;
            item.useAnimation = 25;
            item.useStyle = ItemUseStyleID.Shoot;
            item.useTime = 5;
            item.knockBack = 6.5f;
            item.autoReuse = false;
            item.value = CalamityGlobalItem.Rarity15BuyPrice;
            item.shoot = ModContent.ProjectileType<MurasamaRSlash>();
            item.shootSpeed = 24f;
            item.rare = ModContent.RarityType<Violet>();
            item.CWR().isHeldItem = true;
            CWRUtils.EasySetLocalTextNameOverride(item, "MurasamaEcType");
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            // 创建一个新的集合以防修改 tooltips 集合时产生异常
            List<TooltipLine> newTooltips = new List<TooltipLine>(tooltips);
            List<TooltipLine> prefixTooltips = new List<TooltipLine>();
            // 遍历 tooltips 集合并隐藏特定的提示行
            foreach (TooltipLine line in newTooltips.ToList()) {
                for (int i = 0; i < 9; i++) {
                    if (line.Name == "Tooltip" + i) {
                        line.Hide();
                    }
                }
                if (line.Name.Contains("Prefix")) {
                    prefixTooltips.Add(line.Clone());
                    line.Hide();
                }
            }
            
            // 获取自定义的文本内容
            string textContent = Language.GetText("Mods.CalamityOverhaul.Items.MurasamaEcType.Tooltip").Value;
            // 拆分传奇提示行的文本内容
            string[] legendtopsList = textContent.Split("\n");
            // 遍历传奇提示行并添加新的提示行
            foreach (string legendtops in legendtopsList) {
                string text = legendtops;
                int index = InWorldBossPhase.Instance.Mura_Level();
                TooltipLine newLine = new TooltipLine(CWRMod.Instance, "CWRText", text);
                if (newLine.Text == "[Text]") {
                    if (index >= 0 && index <= 14) {
                        text = CWRLocText.GetTextValue($"Murasama_TextDictionary_Content_{index}");
                    }
                    else {
                        text = "ERROR";
                    }

                    if (!CWRServerConfig.Instance.WeaponEnhancementSystem) {
                        text = InWorldBossPhase.Instance.level11? CWRLocText.GetTextValue("Murasama_No_legend_Content_2") : CWRLocText.GetTextValue("Murasama_No_legend_Content_1");
                    }
                    newLine.Text = text;
                    // 使用颜色渐变以提高可读性
                    newLine.OverrideColor = Color.Lerp(Color.IndianRed, Color.White, 0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly) * 0.5f);
                }
                // 将新提示行添加到新集合中
                newTooltips.Add(newLine);
            }

            MurasamaEcType.SetTooltip(ref newTooltips, CWRMod.Instance.Name);
            // 清空原 tooltips 集合并添加修改后的新Tooltips集合
            tooltips.Clear();
            tooltips.AddRange(newTooltips);
            tooltips.AddRange(prefixTooltips);
        }

        //public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) => damage *= MurasamaEcType.GetOnDamage / (float)MurasamaEcType.GetStartDamage;
        public override bool On_ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            damage *= MurasamaEcType.GetOnDamage / (float)MurasamaEcType.GetStartDamage;
            return false;
        }

        public override void HoldItem(Item item, Player player) {
            player.CWR().HeldMurasamaBool = true;
            //这个代码实现了玩家手持时的动画，生成一个对玩家来说唯一的弹幕来实现这些
            if (player.ownedProjectileCounts[MurasamaEcType.heldProjType] == 0 && player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(item.GetSource_FromThis(), player.Center, Vector2.Zero, MurasamaEcType.heldProjType, item.damage, 0, player.whoAmI);
            }
        }

        public override bool? On_ModifyWeaponCrit(Item item, Player player, ref float crit) {
            crit += MurasamaEcType.GetOnCrit;
            return false;
        }

        public override bool On_PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            Texture2D texture;
            if (Main.LocalPlayer.CWR().HeldMurasamaBool) {
                return true;
            }
            else {
                texture = ModContent.Request<Texture2D>("CalamityMod/Items/Weapons/Melee/MurasamaSheathed").Value;
                spriteBatch.Draw(texture, position, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0);
            }
            return false;
        }

        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            item.initialize();
            if (!(item.CWR().ai[0] <= 0f)) {//这是一个通用的进度条绘制，用于判断充能进度
                Texture2D barBG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarBack", (AssetRequestMode)2).Value;
                Texture2D barFG = ModContent.Request<Texture2D>("CalamityMod/UI/MiscTextures/GenericBarFront", (AssetRequestMode)2).Value;
                float barScale = 3f;
                Vector2 barOrigin = barBG.Size() * 0.5f;
                float yOffset = 50f;
                Vector2 drawPos = position + Vector2.UnitY * scale * (frame.Height - yOffset);
                Rectangle frameCrop = new Rectangle(0, 0, (int)(item.CWR().ai[0] / 10f * barFG.Width), barFG.Height);
                Color color = Main.hslToRgb(Main.GlobalTimeWrappedHourly * 0.6f % 1f, 1f, 0.75f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.1f);
                spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
                spriteBatch.Draw(barFG, drawPos, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
            }
        }

        public override bool? On_CanUseItem(Item item, Player player) {
            //在升龙斩或者爆发弹幕存在时不能使用武器
            if (player.ownedProjectileCounts[ModContent.ProjectileType<MurasamaBreakSwing>()] > 0
                || player.ownedProjectileCounts[ModContent.ProjectileType<MurasamaBreakOut>()] > 0
                || player.PressKey(false)//如果玩家按下了右键，也要禁止武器的使用
                ) {
                return false;
            }
            if (!CWRServerConfig.Instance.WeaponEnhancementSystem && !InWorldBossPhase.Instance.level11) {
                return false;
            }
            return player.ownedProjectileCounts[item.shoot] == 0;
        }

        public override bool? Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            Projectile.NewProjectile(source, position, velocity, ModContent.ProjectileType<MurasamaRSlash>(), damage, knockback, player.whoAmI, 0f, 0f);
            return false;
        }
    }
}
