using CalamityMod;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Projectiles.Weapons.Ranged;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using static CalamityOverhaul.Content.RemakeItems.Core.BaseRItem;

namespace CalamityOverhaul.Content.RemakeItems.Core
{
    internal class GCWRItems : GlobalItem
    {
        public static void ProcessRemakeAction(Item item, Action<BaseRItem> action) {
            if (CWRConstant.ForceReplaceResetContent && CWRMod.RItemIndsDict.ContainsKey(item.type)) {
                action(CWRMod.RItemIndsDict[item.type]);
            }
        }

        public static bool? ProcessRemakeAction(Item item, Func<BaseRItem, bool?> action) {
            bool? result = null;
            if (CWRConstant.ForceReplaceResetContent && CWRMod.RItemIndsDict.ContainsKey(item.type)) {
                result = action(CWRMod.RItemIndsDict[item.type]);
            }
            return result;
        }

        public override void PostDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            ProcessRemakeAction(item, (inds) => inds.PostDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale));
        }

        public override bool PreDrawInInventory(Item item, SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale));
            return rest ?? base.PreDrawInInventory(item, spriteBatch, position, frame, drawColor, itemColor, origin, scale);
        }

        public override bool AllowPrefix(Item item, int pre) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.AllowPrefix(item, pre));
            return rest ?? base.AllowPrefix(item, pre);
        }

        public override bool AltFunctionUse(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.AltFunctionUse(item, player));
            return rest ?? base.AltFunctionUse(item, player);
        }

        public override bool CanAccessoryBeEquippedWith(Item equippedItem, Item incomingItem, Player player) {
            bool? rest = ProcessRemakeAction(equippedItem, (inds) => inds.CanAccessoryBeEquippedWith(equippedItem, incomingItem, player));
            return rest ?? base.CanAccessoryBeEquippedWith(equippedItem, incomingItem, player);
        }

        public override bool? CanBeChosenAsAmmo(Item ammo, Item weapon, Player player) {
            bool? rest = ProcessRemakeAction(ammo, (inds) => inds.CanBeChosenAsAmmo(ammo, weapon, player));
            return rest ?? base.CanBeChosenAsAmmo(ammo, weapon, player);
        }

        public override bool CanBeConsumedAsAmmo(Item ammo, Item weapon, Player player) {
            bool? rest = ProcessRemakeAction(ammo, (inds) => inds.CanBeConsumedAsAmmo(ammo, weapon, player));
            return rest ?? base.CanBeConsumedAsAmmo(ammo, weapon, player);
        }

        public override bool? CanCatchNPC(Item item, NPC target, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanCatchNPC(item, target, player));
            return rest ?? base.CanCatchNPC(item, target, player);
        }

        public override bool CanEquipAccessory(Item item, Player player, int slot, bool modded) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanEquipAccessory(item, player, slot, modded));
            return rest ?? base.CanEquipAccessory(item, player, slot, modded);
        }

        public override bool? CanHitNPC(Item item, Player player, NPC target) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanHitNPC(item, player, target));
            return rest ?? base.CanHitNPC(item, player, target);
        }

        public override bool CanHitPvp(Item item, Player player, Player target) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanHitPvp(item, player, target));
            return rest ?? base.CanHitPvp(item, player, target);
        }

        public override bool? CanMeleeAttackCollideWithNPC(Item item, Rectangle meleeAttackHitbox, Player player, NPC target) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanMeleeAttackCollideWithNPC(item, meleeAttackHitbox, player, target));
            return rest ?? base.CanMeleeAttackCollideWithNPC(item, meleeAttackHitbox, player, target);
        }

        public override bool CanPickup(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanPickup(item, player));
            return rest ?? base.CanPickup(item, player);
        }

        public override bool CanReforge(Item item) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanReforge(item));
            return rest ?? base.CanReforge(item);
        }

        public override bool CanResearch(Item item) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanResearch(item));
            return rest ?? base.CanResearch(item);
        }

        public override bool CanRightClick(Item item) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanRightClick(item));
            return rest ?? base.CanRightClick(item);
        }

        public override bool CanShoot(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanShoot(item, player));
            return rest ?? base.CanShoot(item, player);
        }

        public override bool CanStack(Item destination, Item source) {
            bool? rest = ProcessRemakeAction(destination, (inds) => inds.CanStack(destination, source));
            return rest ?? base.CanStack(destination, source);
        }

        public override bool CanStackInWorld(Item destination, Item source) {
            bool? rest = ProcessRemakeAction(destination, (inds) => inds.CanStackInWorld(destination, source));
            return rest ?? base.CanStackInWorld(destination, source);
        }

        public override bool CanUseItem(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.CanUseItem(item, player));
            return rest ?? base.CanUseItem(item, player);
        }

        public override bool ConsumeItem(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.ConsumeItem(item, player));
            return rest ?? base.ConsumeItem(item, player);
        }

        public override void HoldItem(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.HoldItem(item, player));
        }

        public override void HoldItemFrame(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.HoldItemFrame(item, player));
        }

        public override void LoadData(Item item, TagCompound tag) {
            ProcessRemakeAction(item, (inds) => inds.LoadData(item, tag));
        }

        public override void MeleeEffects(Item item, Player player, Rectangle hitbox) {
            ProcessRemakeAction(item, (inds) => inds.MeleeEffects(item, player, hitbox));
        }

        public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers) {
            NPC.HitModifiers hitNPCModifier = modifiers;
            ProcessRemakeAction(item, (inds) => inds.ModifyHitNPC(item, player, target, ref hitNPCModifier));
            modifiers = hitNPCModifier;
        }

        public override void ModifyHitPvp(Item item, Player player, Player target, ref Player.HurtModifiers modifiers) {
            Player.HurtModifiers hitPlayerModifier = modifiers;
            ProcessRemakeAction(item, (inds) => inds.ModifyHitPvp(item, player, target, ref hitPlayerModifier));
            modifiers = hitPlayerModifier;
        }

        public override void ModifyItemLoot(Item item, ItemLoot itemLoot) {
            ProcessRemakeAction(item, (inds) => inds.ModifyItemLoot(item, itemLoot));
        }

        public override void ModifyItemScale(Item item, Player player, ref float scale) {
            float slp = scale;
            ProcessRemakeAction(item, (inds) => inds.ModifyItemScale(item, player, ref slp));
            scale = slp;
        }

        public override void ModifyManaCost(Item item, Player player, ref float reduce, ref float mult) {
            float newReduce = reduce;
            float newMult = mult;
            ProcessRemakeAction(item, (inds) => inds.ModifyManaCost(item, player, ref newReduce, ref newMult));
            reduce = newReduce;
            mult = newMult;
        }

        public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            ShootStats stats = new() {
                Position = position,
                Velocity = velocity,
                Type = type,
                Damage = damage,
                Knockback = knockback
            };
            ProcessRemakeAction(item, (inds) => inds.ModifyShootStats(item, player, ref stats));
            position = stats.Position;
            velocity = stats.Velocity;
            type = stats.Type;
            damage = stats.Damage;
            knockback = stats.Knockback;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            if (CWRIDs.ItemToBaseGun.TryGetValue(item.type, out BaseGun gun)) {
                if (gun.MustConsumeAmmunition && CWRServerConfig.Instance.MagazineSystem) {
                    tooltips.Add(new TooltipLine(CWRMod.Instance, "CWRGun_MustCA", CWRLocText.GetTextValue("CWRGun_MustCA_Text")));
                }
                if (item.CWR().HasCartridgeHolder && CWRServerConfig.Instance.MagazineSystem) {
                    string newText = CWRLocText.GetTextValue("CWRGun_KL_Text").Replace("[KL]", CWRKeySystem.KreLoad_Key.TooltipHotkeyString());
                    tooltips.Add(new TooltipLine(CWRMod.Instance, "CWRGun_KL", newText));
                }
                if (item.CWR().Scope) {
                    string newText = CWRLocText.GetTextValue("CWRGun_Scope_Text").Replace("[Scope]", CWRKeySystem.ADS_Key.TooltipHotkeyString());
                    tooltips.Add(new TooltipLine(CWRMod.Instance, "CWRGun_Scope", newText));
                }
                string newText3 = CWRLocText.GetTextValue("CWRGun_Recoil_Text").Replace("[Recoil]", CWRLocText.GetTextValue(gun.GetLckRecoilKey()));
                tooltips.Add(new TooltipLine(CWRMod.Instance, "CWRGun_Recoil", newText3));
            }
            if (CWRConstant.ForceReplaceResetContent && CWRMod.RItemIndsDict.ContainsKey(item.type)) {
                string key = CWRMod.RItemIndsDict[item.type].TargetToolTipItemName;
                if (key != "") {
                    if (CWRMod.RItemIndsDict[item.type].IsVanilla) {
                        CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, CWRLocText.GetText(key));
                    } else {
                        CWRUtils.OnModifyTooltips(CWRMod.Instance, tooltips, key);
                    }
                }
            }
            ProcessRemakeAction(item, (inds) => inds.ModifyTooltips(item, tooltips));
        }

        public override void ModifyWeaponCrit(Item item, Player player, ref float crit) {
            float safeCrit = crit;
            ProcessRemakeAction(item, (inds) => inds.ModifyWeaponCrit(item, player, ref safeCrit));
            crit = safeCrit;
        }

        public override void ModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            StatModifier safeDamage = damage;
            ProcessRemakeAction(item, (inds) => inds.ModifyWeaponDamage(item, player, ref safeDamage));
            damage = safeDamage;
        }

        public override void ModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback) {
            StatModifier safeKnockback = knockback;
            ProcessRemakeAction(item, (inds) => inds.ModifyWeaponKnockback(item, player, ref safeKnockback));
            knockback = safeKnockback;
        }

        public override void OnConsumeAmmo(Item weapon, Item ammo, Player player) {
            ProcessRemakeAction(ammo, (inds) => inds.OnConsumeAmmo(weapon, ammo, player));
        }

        public override void OnConsumedAsAmmo(Item ammo, Item weapon, Player player) {
            ProcessRemakeAction(ammo, (inds) => inds.OnConsumedAsAmmo(ammo, weapon, player));
        }

        public override void OnConsumeItem(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.OnConsumeItem(item, player));
        }

        public override void OnConsumeMana(Item item, Player player, int manaConsumed) {
            ProcessRemakeAction(item, (inds) => inds.OnConsumeMana(item, player, manaConsumed));
        }

        public override void OnHitNPC(Item item, Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            ProcessRemakeAction(item, (inds) => inds.OnHitNPC(item, player, target, hit, damageDone));
        }

        public override void OnHitPvp(Item item, Player player, Player target, Player.HurtInfo hurtInfo) {
            ProcessRemakeAction(item, (inds) => inds.OnHitPvp(item, player, target, hurtInfo));
        }

        public override void OnMissingMana(Item item, Player player, int neededMana) {
            ProcessRemakeAction(item, (inds) => inds.OnMissingMana(item, player, neededMana));
        }

        public override bool OnPickup(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.OnPickup(item, player));
            return rest ?? base.OnPickup(item, player);
        }

        public override void OnSpawn(Item item, IEntitySource source) {
            ProcessRemakeAction(item, (inds) => inds.OnSpawn(item, source));
        }

        public override void OnStack(Item destination, Item source, int numToTransfer) {
            ProcessRemakeAction(destination, (inds) => inds.OnStack(destination, source, numToTransfer));
        }

        public override void PickAmmo(Item weapon, Item ammo, Player player, ref int type, ref float speed, ref StatModifier damage, ref float knockback) {
            int safeType = type;
            float safeSpeed = speed;
            float safeKnockback = knockback;
            StatModifier safeDamage = damage;
            ProcessRemakeAction(weapon, (inds) => inds.PickAmmo(weapon, ammo, player, ref safeType, ref safeSpeed, ref safeDamage, ref safeKnockback));
            type = safeType;
            speed = safeSpeed;
            knockback = safeKnockback;
            safeDamage = damage;
        }

        public override void RightClick(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.RightClick(item, player));
        }

        public override void SaveData(Item item, TagCompound tag) {
            ProcessRemakeAction(item, (inds) => inds.SaveData(item, tag));
        }

        public override void SetDefaults(Item entity) {
            ProcessRemakeAction(entity, (inds) => inds.SetDefaults(entity));
        }

        public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.Shoot(item, player, source, position, velocity, type, damage, knockback));
            return rest ?? base.Shoot(item, player, source, position, velocity, type, damage, knockback);
        }

        public override void SplitStack(Item destination, Item source, int numToTransfer) {
            ProcessRemakeAction(destination, (inds) => inds.SplitStack(destination, source, numToTransfer));
        }

        public override void Update(Item item, ref float gravity, ref float maxFallSpeed) {
            float safeGravity = gravity;
            float safeMaxFallSpeed = maxFallSpeed;
            ProcessRemakeAction(item, (inds) => inds.Update(item, ref safeGravity, ref safeMaxFallSpeed));
            gravity = safeGravity;
            maxFallSpeed = safeMaxFallSpeed;
        }

        public override void UpdateAccessory(Item item, Player player, bool hideVisual) {
            ProcessRemakeAction(item, (inds) => inds.UpdateAccessory(item, player, hideVisual));
        }

        public override void UpdateInventory(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.UpdateInventory(item, player));
        }

        public override void UseAnimation(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.UseAnimation(item, player));
        }

        public override bool? UseItem(Item item, Player player) {
            bool? rest = ProcessRemakeAction(item, (inds) => inds.UseItem(item, player));
            return rest ?? base.UseItem(item, player);
        }

        public override void UseItemFrame(Item item, Player player) {
            ProcessRemakeAction(item, (inds) => inds.UseItemFrame(item, player));
        }

        public override void UseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox) {
            Rectangle safeHitbox = hitbox;
            bool safeNoHitbox = noHitbox;
            ProcessRemakeAction(item, (inds) => inds.UseItemFrame(item, player));
            hitbox = safeHitbox;
            noHitbox = safeNoHitbox;
        }

        public override void UseStyle(Item item, Player player, Rectangle heldItemFrame) {
            ProcessRemakeAction(item, (inds) => inds.UseStyle(item, player, heldItemFrame));
        }
    }
}
