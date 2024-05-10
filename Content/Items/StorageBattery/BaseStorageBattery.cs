using CalamityMod.Items;
using CalamityMod;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ID;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using ReLogic.Content;
using System;
using System.Runtime.InteropServices;

namespace CalamityOverhaul.Content.Items.StorageBattery
{
    internal abstract class BaseStorageBattery : ModItem
    {
        public override string Texture => CWRConstant.Item + "StorageBattery/StorageBatteryI";
        protected int ChargeCoolingTimeValue;
        protected int SingleChargePower = 1;
        protected int SingleChargeCooling = 30;
        public override void SetDefaults() {
            Item.width = 56;
            Item.height = 24;
            Item.value = CalamityGlobalItem.RarityPinkBuyPrice;
            Item.rare = ItemRarityID.Pink;
            CalamityGlobalItem modItem = Item.Calamity();
            CWRItems cwrItem = Item.CWR();
            modItem.UsesCharge = true;
            modItem.MaxCharge = 50f;
            modItem.ChargePerUse = 0f;
            cwrItem.IsStorageBattery = true;
            SetStorageBattery(modItem, cwrItem);
        }

        public virtual void SetStorageBattery(CalamityGlobalItem calStb, CWRItems cwrStb) {

        }

        public override void UpdateInventory(Player player) {
            if (ChargeCoolingTimeValue > 0) {
                ChargeCoolingTimeValue--;
                return;
            }
            CalamityGlobalItem storageBattery = Item.Calamity();
            CWRItems storageBatteryCWR = Item.CWR();
            if (storageBattery.Charge <= 0) {
                return;
            }
            for (int j = 0; j < 50; j++) {
                Item i = player.inventory[j];
                if (i.type == ItemID.None) {
                    continue;
                }
                CalamityGlobalItem modItem = i.Calamity();
                CWRItems cwrItem = i.CWR();
                if (!modItem.UsesCharge || modItem.Charge >= modItem.MaxCharge || cwrItem.IsStorageBattery) {
                    continue;
                }
                float stbChargeValue = SingleChargePower;
                if (stbChargeValue > storageBattery.Charge) {
                    stbChargeValue = storageBattery.Charge;
                }
                modItem.Charge += stbChargeValue;
                if (modItem.Charge > modItem.MaxCharge) {
                    modItem.Charge = modItem.MaxCharge;
                }
                storageBatteryCWR.PowerInteractionValue = cwrItem.PowerInteractionValue = 30;
                storageBattery.Charge -= stbChargeValue;
                ExtraOperation(Item, i, storageBattery, storageBatteryCWR, modItem, cwrItem);
                if (storageBattery.Charge <= 0) {
                    storageBattery.Charge = 0;
                    break;
                }
            }
            ChargeCoolingTimeValue += SingleChargeCooling;
        }

        public virtual void ExtraOperation(Item storageBattery, Item targetChargeItem, CalamityGlobalItem calStb, CWRItems cwrStb, CalamityGlobalItem calTargetCharge, CWRItems cwrTargetCharge) {

        }

        public override void PostDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
        }

        public static void DrawPowerInteractionValue(CalamityGlobalItem item, SpriteBatch spriteBatch, Vector2 position, float scale, float powerInteractionValue) {
            //Texture2D value1 = CWRUtils.GetT2DValue("CalamityMod/UI/DraedonsArsenal/ChargeMeterBorder");
            //Texture2D value2 = CWRUtils.GetT2DValue("CalamityMod/UI/DraedonsArsenal/ChargeMeter");
            //float sengs = powerInteractionValue / 10f;
            //if (sengs > 1f) {
            //    sengs = 1f;
            //}
            //Color drawColor = Color.White * sengs;
            //float uiScale = scale;
            //float offset = (value1.Width - value2.Width) * 0.5f;
            //position.Y += 12;
            //spriteBatch.Draw(value1, position, null, drawColor, 0f, value1.Size() * 0.5f, uiScale, SpriteEffects.None, 0);
            //Rectangle barRectangle = new Rectangle(0, 0, (int)(value2.Width * item.ChargeRatio), value2.Width);
            //spriteBatch.Draw(value2, position + new Vector2(offset * uiScale, 0), barRectangle, drawColor, 0f, value2.Size() * 0.5f, uiScale, SpriteEffects.None, 0);
            Texture2D barBG = CWRUtils.GetT2DValue("CalamityMod/UI/DraedonsArsenal/ChargeMeterBorder");
            Texture2D barFG = CWRUtils.GetT2DValue("CalamityMod/UI/DraedonsArsenal/ChargeMeter");
            float barScale = 1f;
            Vector2 barOrigin = barBG.Size() * 0.5f;
            Vector2 drawPos = position + Vector2.UnitY * scale + new Vector2(0, 10);
            Rectangle frameCrop = new Rectangle(0, 0, (int)(item.ChargeRatio * barFG.Width), barFG.Height);
            float sengs = powerInteractionValue / 10f;
            if (sengs > 1f) {
                sengs = 1f;
            }
            Color color = Color.White * sengs;
            spriteBatch.Draw(barBG, drawPos, null, color, 0f, barOrigin, scale * barScale, 0, 0f);
            spriteBatch.Draw(barFG, drawPos + new Vector2(8, 0) * scale, frameCrop, color * 0.8f, 0f, barOrigin, scale * barScale, 0, 0f);
        }
    }
}
