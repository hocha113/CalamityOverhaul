using InnoVault.GameSystem;
using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;
using ThoriumMod.Buffs;
using ThoriumMod.Utilities;

namespace CalamityOverhaul.OtherMods.Thorium
{
    [JITWhenModsEnabled("ThoriumMod")]
    internal static class ThoriumRef
    {
        public static void Load() {
            if (!ModLoader.TryGetMod("ThoriumMod", out Mod mod)) {
                return;
            }
            var type = CWRUtils.GetTargetTypeInStringKey(CWRUtils.GetModTypes(mod), "SheathData");
            if (type == null) {
                return;
            }
            var meth = type.GetMethod("MeleeButNotValidItem", BindingFlags.Static | BindingFlags.Public);
            VaultHook.Add(meth, On_MeleeButNotValidItem);
            meth = type.GetMethod("ValidItem", BindingFlags.Static | BindingFlags.Public);
            VaultHook.Add(meth, On_ValidItem);
        }

        public static bool On_ValidItem(Func<Item, bool> orig, Item item) {
            return item.damage > 0 && item.CountsAsClass(DamageClass.Melee);
        }

        public static bool On_MeleeButNotValidItem(Func<Item, bool> orig, Item item) {
            return item.damage == 0 || !item.CountsAsClass(DamageClass.Melee);
        }

        public static void ModifyProjBySheath(Player player, ref NPC.HitModifiers modifiers) {
            if (player.HasBuff<SheathBuff>()) {
                modifiers.SourceDamage *= 2f;
            }
        }

        public static void OnHitNPCWithProj(Player player, NPC target, NPC.HitInfo hit, int damageDone) {
            player.GetThoriumPlayer().sheathTracker.Strike(target, hit, damageDone);
        }
    }

    internal class ThoriumRefLoader : ModPlayer
    {
        public override void Load() {
            if (ModLoader.HasMod("ThoriumMod")) {
                try {
                    ThoriumRef.Load();
                } catch (Exception ex) { CWRMod.Instance.Logger.Error($"ThoriumRefLoader.Load An Error Has Cccurred: {ex.Message}"); }
            }
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            if (ModLoader.HasMod("ThoriumMod")) {
                ThoriumRef.ModifyProjBySheath(Player, ref modifiers);
            }
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone) {
            if (ModLoader.HasMod("ThoriumMod")) {
                ThoriumRef.OnHitNPCWithProj(Player, target, hit, damageDone);
            }
        }
    }
}
