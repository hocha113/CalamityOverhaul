using CalamityMod.Items.Weapons.Magic;
using CalamityMod.Items.Weapons.Ranged;
using CalamityMod.World;
using System;
using System.Reflection;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Structures
{
    internal static class StructuresBehavior
    {
        internal delegate void SetChest_Delegate(Chest chest, int type, bool hasPlacedLogAndSchematic);
        public static void Load() {
            Type draedonStructures = typeof(DraedonStructures);
            MethodInfo getMethod(string key) => draedonStructures.GetMethod(key, BindingFlags.Public | BindingFlags.Static);
            MonoModHooks.Add(getMethod("FillPlanetoidLaboratoryChest"), OnPlanetoidChest);
            MonoModHooks.Add(getMethod("FillPlagueLaboratoryChest"), OnPlagueChest);
        }

        public static void UnLoad() { }

        private static void AddChestContent(Chest chest, int type, int stack, string text) {
            foreach (var item in chest.item) {
                if (item.type != ItemID.None) {
                    continue;
                }
                text.DompInConsole();
                item.SetDefaults(type);
                item.stack = stack;
                break;
            }
        }

        private static void OnPlanetoidChest(SetChest_Delegate orig, Chest chest, int type, bool hasPlacedLogAndSchematic) {
            orig.Invoke(chest, type, hasPlacedLogAndSchematic);
            if (hasPlacedLogAndSchematic) {
                AddChestContent(chest, ModContent.ItemType<SHPC>(), 1
                    , CWRUtils.Translation("正在将 SHPC 塞入箱子", "Shoving SHPC into the chest."));
            }
        }

        private static void OnPlagueChest(SetChest_Delegate orig, Chest chest, int type, bool hasPlacedLogAndSchematic) {
            orig.Invoke(chest, type, hasPlacedLogAndSchematic);
            if (hasPlacedLogAndSchematic) {
                AddChestContent(chest, ModContent.ItemType<BlossomFlux>(), 1
                    , CWRUtils.Translation("正在将 Blossom Flux 塞入箱子", "Stuffing Blossom Flux into chest."));
            }
        }
    }
}
