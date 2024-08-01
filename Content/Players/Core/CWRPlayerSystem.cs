using CalamityMod.CalPlayer;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Players.Core
{
    public delegate void On_ModifyHitNPCWithItem_Dalegate(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers);
    public delegate void On_ModifyHitNPCWithProj_Dalegate(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers);
    public delegate void On_ApplyRippersToDamage_Dalegate(CalamityPlayer mp, bool trueMelee, ref float damageMult);

    internal class CWRPlayerSystem : ModSystem
    {
        public static List<PlayerSet> PlayerSets = [];
        public static Type playerLoaderType;

        public static MethodBase onModifyHitNPCWithItemMethod;
        public static MethodBase onModifyHitNPCWithProjMethod;

        public override void Load() {
            CWRUtils.HanderInstance(ref PlayerSets, CWRUtils.GetSubclasses(typeof(PlayerSet)));
            playerLoaderType = typeof(PlayerLoader);

            MethodBase getPublicStaticMethod(string key) => playerLoaderType.GetMethod(key, BindingFlags.Public | BindingFlags.Static);

            onModifyHitNPCWithItemMethod = getPublicStaticMethod("ModifyHitNPCWithItem");
            onModifyHitNPCWithProjMethod = getPublicStaticMethod("ModifyHitNPCWithProj");

            if (onModifyHitNPCWithItemMethod != null) {
                MonoModHooks.Add(onModifyHitNPCWithItemMethod, OnModifyHitNPCWithItemHook);
            }
            if (onModifyHitNPCWithProjMethod != null) {
                MonoModHooks.Add(onModifyHitNPCWithProjMethod, OnModifyHitNPCWithProjHook);
            }
        }

        public override void PostSetupContent() {
            foreach (var playerSet in PlayerSets) {
                playerSet.Load();
            }
        }

        private static void OnModifyHitNPCWithItemHook(On_ModifyHitNPCWithItem_Dalegate orig, Player player, Item item, NPC target, ref NPC.HitModifiers modifiers) {
            foreach (var pset in PlayerSets) {
                bool reset = pset.On_ModifyHitNPCWithItem(player, item, target, ref modifiers);
                if (!reset) {
                    return;
                }
            }
            orig.Invoke(player, item, target, ref modifiers);
        }

        private static void OnModifyHitNPCWithProjHook(On_ModifyHitNPCWithProj_Dalegate orig, Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            foreach (var pset in PlayerSets) {
                bool reset = pset.On_ModifyHitNPCWithProj(player, proj, target, ref modifiers);
                if (!reset) {
                    return;
                }
            }
            orig.Invoke(player, proj, target, ref modifiers);
        }
    }
}
