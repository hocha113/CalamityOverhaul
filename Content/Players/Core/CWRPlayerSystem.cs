using CalamityMod.CalPlayer;
using CalamityOverhaul.Common;
using InnoVault;
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
    public delegate void On_ApplyRippersToDamage_Dalegate(CalamityPlayer mp, bool trueMelee, ref float damageMult);

    internal class CWRPlayerSystem : ModSystem
    {
        public static List<PlayerSet> PlayerSets = [];
        public static Type playerLoaderType;

        public static MethodBase onModifyHitNPCWithItemMethod;
        public static MethodBase onModifyHitNPCWithProjMethod;

        /* 使用IL钩子来阻止玩家切换武器，这样的手段粗暴但可以避免Clone物品bug的发生，这段注释内容是需要参考的Player.Update(int i)的一部分IL代码，最好不要删除，以避免下面的IL钩子没人可以看懂
		// if (itemAnimation == 0 && ItemTimeIsZero && reuseDelay == 0)
		IL_1059: ldarg.0
		IL_105a: ldfld int32 Terraria.Player::itemAnimation
		IL_105f: brtrue IL_15a5

		IL_1064: ldarg.0
		IL_1065: call instance bool Terraria.Player::get_ItemTimeIsZero()
		IL_106a: brfalse IL_15a5

		// dropItemCheck();
		IL_106f: ldarg.0
		IL_1070: ldfld int32 Terraria.Player::reuseDelay
		IL_1075: brtrue IL_15a5

		IL_107a: ldarg.0
		IL_107b: call instance void Terraria.Player::dropItemCheck()
		// int num6 = selectedItem;
		IL_1080: ldarg.0
		IL_1081: ldfld int32 Terraria.Player::selectedItem
		IL_1086: stloc.s 32
		// bool flag7 = false;
		IL_1088: ldc.i4.0
		IL_1089: stloc.s 33
		// if (!Main.drawingPlayerChat && selectedItem != 58 && !Main.editSign && !Main.editChest)
		IL_108b: ldsfld bool Terraria.Main::drawingPlayerChat
		IL_1090: brtrue IL_1336

		IL_1095: ldarg.0
		IL_1096: ldfld int32 Terraria.Player::selectedItem
		IL_109b: ldc.i4.s 58
		IL_109d: beq IL_1336

		IL_10a2: ldsfld bool Terraria.Main::editSign
		IL_10a7: brtrue IL_1336

		// if (PlayerInput.Triggers.Current.Hotbar1)
		IL_10ac: ldsfld bool Terraria.Main::editChest
		IL_10b1: brtrue IL_1336

		IL_10b6: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_10bb: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_10c0: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar1()
		IL_10c5: brfalse.s IL_10d1

		// selectedItem = 0;
		IL_10c7: ldarg.0
		IL_10c8: ldc.i4.0
		IL_10c9: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_10ce: ldc.i4.1
		IL_10cf: stloc.s 33

		// if (PlayerInput.Triggers.Current.Hotbar2)
		IL_10d1: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_10d6: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_10db: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar2()
		IL_10e0: brfalse.s IL_10ec

		// selectedItem = 1;
		IL_10e2: ldarg.0
		IL_10e3: ldc.i4.1
		IL_10e4: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_10e9: ldc.i4.1
		IL_10ea: stloc.s 33

		// if (PlayerInput.Triggers.Current.Hotbar3)
		IL_10ec: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_10f1: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_10f6: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar3()
		IL_10fb: brfalse.s IL_1107

		// selectedItem = 2;
		IL_10fd: ldarg.0
		IL_10fe: ldc.i4.2
		IL_10ff: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_1104: ldc.i4.1
		IL_1105: stloc.s 33

		// if (PlayerInput.Triggers.Current.Hotbar4)
		IL_1107: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_110c: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_1111: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar4()
		IL_1116: brfalse.s IL_1122

		// selectedItem = 3;
		IL_1118: ldarg.0
		IL_1119: ldc.i4.3
		IL_111a: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_111f: ldc.i4.1
		IL_1120: stloc.s 33

		// if (PlayerInput.Triggers.Current.Hotbar5)
		IL_1122: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_1127: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_112c: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar5()
		IL_1131: brfalse.s IL_113d

		// selectedItem = 4;
		IL_1133: ldarg.0
		IL_1134: ldc.i4.4
		IL_1135: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_113a: ldc.i4.1
		IL_113b: stloc.s 33

		// if (PlayerInput.Triggers.Current.Hotbar6)
		IL_113d: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_1142: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_1147: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar6()
		IL_114c: brfalse.s IL_1158

		// selectedItem = 5;
		IL_114e: ldarg.0
		IL_114f: ldc.i4.5
		IL_1150: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_1155: ldc.i4.1
		IL_1156: stloc.s 33

		// if (PlayerInput.Triggers.Current.Hotbar7)
		IL_1158: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_115d: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_1162: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar7()
		IL_1167: brfalse.s IL_1173

		// selectedItem = 6;
		IL_1169: ldarg.0
		IL_116a: ldc.i4.6
		IL_116b: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_1170: ldc.i4.1
		IL_1171: stloc.s 33

		// if (PlayerInput.Triggers.Current.Hotbar8)
		IL_1173: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_1178: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_117d: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar8()
		IL_1182: brfalse.s IL_118e

		// selectedItem = 7;
		IL_1184: ldarg.0
		IL_1185: ldc.i4.7
		IL_1186: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_118b: ldc.i4.1
		IL_118c: stloc.s 33

		// if (PlayerInput.Triggers.Current.Hotbar9)
		IL_118e: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_1193: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_1198: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar9()
		IL_119d: brfalse.s IL_11a9

		// selectedItem = 8;
		IL_119f: ldarg.0
		IL_11a0: ldc.i4.8
		IL_11a1: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_11a6: ldc.i4.1
		IL_11a7: stloc.s 33

		// if (PlayerInput.Triggers.Current.Hotbar10)
		IL_11a9: ldsfld class Terraria.GameInput.TriggersPack Terraria.GameInput.PlayerInput::Triggers
		IL_11ae: ldfld class Terraria.GameInput.TriggersSet Terraria.GameInput.TriggersPack::Current
		IL_11b3: callvirt instance bool Terraria.GameInput.TriggersSet::get_Hotbar10()
		IL_11b8: brfalse.s IL_11c5

		// selectedItem = 9;
		IL_11ba: ldarg.0
		IL_11bb: ldc.i4.s 9
		IL_11bd: stfld int32 Terraria.Player::selectedItem
		// flag7 = true;
		IL_11c2: ldc.i4.1
		IL_11c3: stloc.s 33
         */
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
