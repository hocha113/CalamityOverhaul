using CalamityOverhaul.Common;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Players.Core
{
    public delegate void On_ModifyHitNPCWithItem_Dalegate(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers);
    public delegate void On_ModifyHitNPCWithProj_Dalegate(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers);
    internal class CWRPlayerSystem : ModSystem
    {
        public static List<PlayerSet> PlayerSets = [];
        public static Type playerLoaderType;

        public static MethodBase onModifyHitNPCWithItemMethod;
        public static MethodBase onModifyHitNPCWithProjMethod;

        private void Player_Update_Hook(ILContext il) {
            ILCursor c = new ILCursor(il);
            ILLabel LabelKey = null;
            Type playerType = typeof(Player);
            FieldInfo itemAnimation = playerType.GetField("itemAnimation", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo ItemTimeIsZero = playerType.GetProperty("ItemTimeIsZero", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo reuseDelay = playerType.GetField("reuseDelay", BindingFlags.Instance | BindingFlags.Public);
            Type mainType = typeof(Main);
            FieldInfo drawingPlayerChat = mainType.GetField("drawingPlayerChat", BindingFlags.Static | BindingFlags.Public);
            FieldInfo selectedItem = playerType.GetField("selectedItem", BindingFlags.Instance | BindingFlags.Public);
            FieldInfo editSign = mainType.GetField("editSign", BindingFlags.Static | BindingFlags.Public);
            FieldInfo editChest = mainType.GetField("editChest", BindingFlags.Static | BindingFlags.Public);

            if (c.TryGotoNext(
                    MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld(itemAnimation),
                    x => x.MatchBrtrue(out LabelKey),
                    x => x.MatchLdarg(0),
                    x => x.MatchCall(ItemTimeIsZero.GetMethod),
                    x => x.MatchBrfalse(out LabelKey),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld(reuseDelay),
                    x => x.MatchBrtrue(out LabelKey)
                    )) {
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate(static (Player self) => CanSwitchWeaponHook(self));
                c.Emit(OpCodes.Brfalse, LabelKey);
                if (c.TryGotoNext(
                    MoveType.After,
                    x => x.MatchLdcI4(0),
                    x => x.MatchStloc(49),
                    x => x.MatchLdsfld(drawingPlayerChat),
                    x => x.MatchBrtrue(out LabelKey),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld(selectedItem),
                    x => x.MatchLdcI4(58),
                    x => x.MatchBeq(out LabelKey),
                    x => x.MatchLdsfld(editSign),
                    x => x.MatchBrtrue(out LabelKey),
                    x => x.MatchLdsfld(editChest),
                    x => x.MatchBrtrue(out LabelKey)
                    )) {
                    c.Emit(OpCodes.Ldarg_0);
                    c.EmitDelegate(static (Player self) => CanSwitchWeaponHook(self));
                    c.Emit(OpCodes.Brfalse, LabelKey);
                }
            }
        }

        public static bool CanSwitchWeaponHook(Player player) {
            foreach (PlayerSet set in PlayerSets) {
                bool? reset = set.CanSwitchWeapon(player);
                if (reset.HasValue) {
                    return reset.Value;
                }
            }
            return true;
        }

        public override void Load() {
            IL_Player.Update += Player_Update_Hook;

            PlayerSets = VaultUtils.GetSubclassInstances<PlayerSet>();
            playerLoaderType = typeof(PlayerLoader);

            MethodBase getPublicStaticMethod(string key) => playerLoaderType.GetMethod(key, BindingFlags.Public | BindingFlags.Static);

            onModifyHitNPCWithItemMethod = getPublicStaticMethod("ModifyHitNPCWithItem");
            onModifyHitNPCWithProjMethod = getPublicStaticMethod("ModifyHitNPCWithProj");

            if (onModifyHitNPCWithItemMethod != null) {
                CWRHook.Add(onModifyHitNPCWithItemMethod, OnModifyHitNPCWithItemHook);
            }
            if (onModifyHitNPCWithProjMethod != null) {
                CWRHook.Add(onModifyHitNPCWithProjMethod, OnModifyHitNPCWithProjHook);
            }
        }

        public override void Unload() {
            IL_Player.Update -= Player_Update_Hook;
            PlayerSets = null;
            playerLoaderType = null;
            onModifyHitNPCWithItemMethod = null;
            onModifyHitNPCWithProjMethod = null;
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
