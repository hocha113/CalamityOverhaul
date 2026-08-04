using InnoVault.GameSystem;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Reflection;
using System.Threading;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.MainMenus.Themes.Himayo
{
    internal enum HimayoMenuAction
    {
        SinglePlayer,
        Multiplayer,
        Achievements,
        Workshop,
        Settings,
        Credits,
        Exit
    }

    [Autoload(Side = ModSide.Client)]
    internal sealed class HimayoMenuVanillaBridge : MenuOverride
    {
        private const int NoPendingAction = -1;
        private static readonly BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        private static ILHook mainDrawMenuHook;
        private static ILHook modMenuInnerHook;
        private static FieldInfo selectedMenuField;
        private static MethodInfo interfaceAddMenuButtonsMethod;
        private static MethodInfo offsetModMenuMethod;
        private static MethodInfo drawSocialMediaButtonsMethod;
        private static MethodInfo drawTmlSocialMediaButtonsMethod;
        private static MethodInfo drawVersionNumberMethod;
        private static volatile bool bridgeOperational;
        private static volatile bool themeActive;
        private static volatile bool titleFrameActive;
        private static bool mainPatchReady;
        private static bool modMenuPatchReady;
        private static int pendingAction = NoPendingAction;
        private static int failureLogged;

        internal static event Action<GameTime> FrameUpdate;

        internal static bool BridgeOperational => bridgeOperational;
        internal static bool ThemeActive => themeActive;
        internal static bool TitleFrameActive => titleFrameActive;
        internal static Rectangle CustomSwitchRect { get; set; } = Rectangle.Empty;

        public override bool CanLoad() => !Main.dedServ;

        public override bool? DrawMenu(GameTime gameTime) {
            CaptureTitleFrame();
            if (themeActive) {
                InvokeFrameUpdate(gameTime);
            }
            return null;
        }

        internal static void SetThemeActive(bool active) {
            themeActive = active;
            if (active) {
                if (bridgeOperational && Main.gameMenu && Main.menuMode == 0) {
                    titleFrameActive = true;
                }
                return;
            }

            Interlocked.Exchange(ref pendingAction, NoPendingAction);
            CustomSwitchRect = Rectangle.Empty;
        }

        internal static void SetCustomSwitchRect(Rectangle rectangle) => CustomSwitchRect = rectangle;

        internal static bool TryEnqueue(HimayoMenuAction action) => TryEnqueueAction(action);

        internal static bool TryEnqueueAction(HimayoMenuAction action) {
            int value = (int)action;
            if (!CanUseCustomControls() || (uint)value > (uint)HimayoMenuAction.Exit) {
                return false;
            }

            return Interlocked.CompareExchange(ref pendingAction, value, NoPendingAction) == NoPendingAction;
        }

        internal static bool EnqueueAction(HimayoMenuAction action) => TryEnqueueAction(action);

        internal static bool RequestNextTheme() => RequestThemeOffset(1);

        internal static bool RequestPreviousTheme() => RequestThemeOffset(-1);

        internal static bool RequestThemeOffset(int offset) {
            if (!CanUseCustomControls() || offset == 0 || offsetModMenuMethod == null) {
                return false;
            }

            try {
                offsetModMenuMethod.Invoke(null, [offset > 0 ? 1 : -1]);
                SoundEngine.PlaySound(SoundID.MenuTick);
                return true;
            } catch (Exception ex) {
                DisableBridge("主题切换调用失效", ex);
                return false;
            }
        }

        internal static void LoadHooks() {
            ResetState(false);
            try {
                Type interfaceType = typeof(MenuLoader).Assembly.GetType("Terraria.ModLoader.UI.Interface")
                    ?? throw new MissingMemberException("Terraria.ModLoader.UI.Interface");
                Type intRef = typeof(int).MakeByRefType();

                MethodInfo drawMenuMethod = RequireMethod(typeof(Main).GetMethod("DrawMenu", InstancePrivate, null, [typeof(GameTime)], null), "Main.DrawMenu");
                MethodInfo modMenuInnerMethod = RequireMethod(typeof(MenuLoader).GetMethod("UpdateAndDrawModMenuInner", StaticPrivate, null,
                    [typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch), typeof(GameTime), typeof(Color), typeof(float), typeof(float)], null),
                    "MenuLoader.UpdateAndDrawModMenuInner");
                selectedMenuField = RequireField(typeof(Main).GetField("selectedMenu", InstancePrivate), "Main.selectedMenu", typeof(int));
                interfaceAddMenuButtonsMethod = RequireMethod(interfaceType.GetMethod("AddMenuButtons", StaticPrivate, null,
                    [typeof(Main), typeof(int), typeof(string[]), typeof(float[]), intRef, intRef, intRef, intRef], null),
                    "Interface.AddMenuButtons");
                offsetModMenuMethod = RequireMethod(typeof(MenuLoader).GetMethod("OffsetModMenu", StaticPrivate, null, [typeof(int)], null),
                    "MenuLoader.OffsetModMenu");
                drawSocialMediaButtonsMethod = RequireMethod(typeof(Main).GetMethod("DrawSocialMediaButtons", StaticPrivate, null,
                    [typeof(Color), typeof(float)], null), "Main.DrawSocialMediaButtons");
                drawTmlSocialMediaButtonsMethod = RequireMethod(typeof(Main).GetMethod("DrawtModLoaderSocialMediaButtons", StaticPrivate, null,
                    [typeof(Color), typeof(float)], null), "Main.DrawtModLoaderSocialMediaButtons");
                drawVersionNumberMethod = RequireMethod(typeof(Main).GetMethod("DrawVersionNumber", StaticPrivate, null,
                    [typeof(Color), typeof(float)], null), "Main.DrawVersionNumber");

                mainDrawMenuHook = new ILHook(drawMenuMethod, PatchMainDrawMenu, false);
                modMenuInnerHook = new ILHook(modMenuInnerMethod, PatchModMenuInner, false);
                mainDrawMenuHook.Apply();
                modMenuInnerHook.Apply();
                if (!mainPatchReady || !modMenuPatchReady) {
                    throw new InvalidOperationException("Himayo 菜单桥补丁未完成");
                }

                bridgeOperational = true;
            } catch (Exception ex) {
                bridgeOperational = false;
                DisposeHooks();
                LogFailure("Himayo 菜单桥已回退到原版菜单", ex);
            }
        }

        internal static void UnloadHooks() {
            bridgeOperational = false;
            DisposeHooks();
            ResetState(true);
        }

        private static void CaptureTitleFrame() {
            bool titleAtEntry = themeActive && Main.gameMenu && Main.menuMode == 0;
            titleFrameActive = bridgeOperational && titleAtEntry;
            if (!titleAtEntry) {
                Interlocked.Exchange(ref pendingAction, NoPendingAction);
                CustomSwitchRect = Rectangle.Empty;
            }
        }

        private static void InvokeFrameUpdate(GameTime gameTime) {
            Delegate[] callbacks = FrameUpdate?.GetInvocationList();
            if (callbacks == null) {
                return;
            }

            foreach (Action<GameTime> callback in callbacks) {
                try {
                    callback(gameTime);
                } catch (Exception ex) {
                    LogFailure("Himayo 菜单帧更新异常", ex);
                }
            }
        }

        private static bool ShouldSuppressNativeTitleUi() => bridgeOperational && titleFrameActive;

        private static bool CanUseCustomControls() => themeActive && ShouldSuppressNativeTitleUi();

        private static void PrepareNativeAction(Main main) {
            if (!ShouldSuppressNativeTitleUi()) {
                return;
            }

            int action = Volatile.Read(ref pendingAction);
            int selected = NoPendingAction;
            if ((uint)action <= (uint)HimayoMenuAction.Workshop) {
                selected = action;
                Interlocked.CompareExchange(ref pendingAction, NoPendingAction, action);
            }
            else if (action != NoPendingAction && action < (int)HimayoMenuAction.Settings) {
                Interlocked.Exchange(ref pendingAction, NoPendingAction);
            }

            SetSelectedMenu(main, selected);
        }

        private static void ResolveTailAction(Main main, int buttonIndex) {
            if (!ShouldSuppressNativeTitleUi()) {
                return;
            }

            int action = Volatile.Read(ref pendingAction);
            if (action < (int)HimayoMenuAction.Settings || action > (int)HimayoMenuAction.Exit) {
                return;
            }

            SetSelectedMenu(main, buttonIndex + action - (int)HimayoMenuAction.Settings);
            Interlocked.CompareExchange(ref pendingAction, NoPendingAction, action);
        }

        private static int SuppressNativeButtonCount(int originalCount) => ShouldSuppressNativeTitleUi() ? 0 : originalCount;

        private static void SetSelectedMenu(Main main, int selected) {
            try {
                selectedMenuField.SetValue(main, selected);
            } catch (Exception ex) {
                DisableBridge("Main.selectedMenu 写入失效", ex);
            }
        }

        private static void PatchMainDrawMenu(ILContext il) {
            Instruction addButtonsCall = FindSingleCall(il, interfaceAddMenuButtonsMethod, "Interface.AddMenuButtons");
            ILCursor addButtons = new(il);
            int offYLocal = -1;
            int spacingLocal = -1;
            int buttonIndexLocal = -1;
            int numButtonsLocal = -1;
            if (!addButtons.TryGotoNext(MoveType.After,
                i => i.MatchLdarg(0),
                i => i.MatchLdarg(0),
                i => i.MatchLdfld(selectedMenuField),
                i => i.MatchLdloc(out _),
                i => i.MatchLdloc(out _),
                i => i.MatchLdloca(out offYLocal),
                i => i.MatchLdloca(out spacingLocal),
                i => i.MatchLdloca(out buttonIndexLocal),
                i => i.MatchLdloca(out numButtonsLocal),
                i => ReferenceEquals(i, addButtonsCall))) {
                throw new InvalidOperationException("Main.DrawMenu AddMenuButtons 锚点失配");
            }
            Instruction afterAddButtons = addButtons.Next
                ?? throw new InvalidOperationException("Main.DrawMenu AddMenuButtons 尾部缺失");

            ILCursor titleStart = new(il);
            if (!titleStart.TryGotoNext(MoveType.After,
                i => i.MatchLdcI4(0),
                i => i.MatchStloc(buttonIndexLocal),
                i => i.MatchLdcI4(220),
                i => i.MatchStloc(offYLocal),
                i => i.MatchLdcI4(7),
                i => i.MatchStloc(numButtonsLocal),
                i => i.MatchLdcI4(52),
                i => i.MatchStloc(spacingLocal),
                i => i.MatchLdloc(out _),
                i => i.MatchLdloc(buttonIndexLocal),
                i => i.MatchLdsfld(typeof(Lang), "menu"),
                i => i.MatchLdcI4(12),
                i => i.MatchLdelemRef(),
                i => i.MatchCallvirt(typeof(LocalizedText), "get_Value"),
                i => i.MatchStelemRef())) {
                throw new InvalidOperationException("Main.DrawMenu 标题动作锚点失配");
            }
            Instruction afterFirstLabel = titleStart.Next
                ?? throw new InvalidOperationException("Main.DrawMenu 标题动作尾部缺失");

            Instruction socialCall = FindSingleCall(il, drawSocialMediaButtonsMethod, "DrawSocialMediaButtons");
            Instruction tmlSocialCall = FindSingleCall(il, drawTmlSocialMediaButtonsMethod, "DrawtModLoaderSocialMediaButtons");
            Instruction versionCall = FindSingleCall(il, drawVersionNumberMethod, "DrawVersionNumber");
            ValidateTwoArgumentCall(socialCall, "DrawSocialMediaButtons");
            ValidateTwoArgumentCall(tmlSocialCall, "DrawtModLoaderSocialMediaButtons");
            ValidateTwoArgumentCall(versionCall, "DrawVersionNumber");

            ILCursor edit = new(il);
            edit.Goto(afterFirstLabel, MoveType.Before);
            edit.Emit(OpCodes.Ldarg_0);
            edit.EmitDelegate(PrepareNativeAction);

            edit.Goto(afterAddButtons, MoveType.Before);
            edit.Emit(OpCodes.Ldarg_0);
            edit.Emit(OpCodes.Ldloc, buttonIndexLocal);
            edit.EmitDelegate(ResolveTailAction);
            edit.Emit(OpCodes.Ldloc, numButtonsLocal);
            edit.EmitDelegate(SuppressNativeButtonCount);
            edit.Emit(OpCodes.Stloc, numButtonsLocal);

            EmitCallSkip(il, socialCall);
            EmitCallSkip(il, tmlSocialCall);
            EmitCallSkip(il, versionCall);
            mainPatchReady = true;
        }

        private static void PatchModMenuInner(ILContext il) {
            Instruction switchTextAnchor = null;
            Instruction returnInstruction = null;
            int anchorCount = 0;
            int returnCount = 0;
            foreach (Instruction instruction in il.Body.Instructions) {
                if (instruction.MatchLdstr("tModLoader.ModMenuSwap")
                    && instruction.Next?.MatchCall(typeof(Language), nameof(Language.GetTextValue)) == true) {
                    switchTextAnchor = instruction;
                    anchorCount++;
                }
                if (instruction.OpCode == OpCodes.Ret) {
                    returnInstruction = instruction;
                    returnCount++;
                }
            }

            if (anchorCount != 1 || returnCount != 1 || switchTextAnchor == null || returnInstruction == null
                || switchTextAnchor.Offset >= returnInstruction.Offset) {
                throw new InvalidOperationException("MenuLoader 主题切换器锚点失配");
            }

            ILCursor cursor = new(il);
            cursor.Goto(switchTextAnchor, MoveType.AfterLabel);
            cursor.EmitDelegate(ShouldSuppressNativeTitleUi);
            cursor.Emit(OpCodes.Brtrue, returnInstruction);
            modMenuPatchReady = true;
        }

        private static Instruction FindSingleCall(ILContext il, MethodInfo method, string name) {
            Instruction found = null;
            int count = 0;
            foreach (Instruction instruction in il.Body.Instructions) {
                if (!instruction.MatchCall(method)) {
                    continue;
                }
                found = instruction;
                count++;
            }

            if (count != 1 || found == null) {
                throw new InvalidOperationException($"Main.DrawMenu {name} 锚点失配");
            }
            return found;
        }

        private static void ValidateTwoArgumentCall(Instruction call, string name) {
            if (call.Previous?.Previous == null
                || !call.Previous.MatchLdloc(out _)
                || !call.Previous.Previous.MatchLdloc(out _)
                || call.Next == null) {
                throw new InvalidOperationException($"Main.DrawMenu {name} 参数锚点失配");
            }
        }

        private static void EmitCallSkip(ILContext il, Instruction call) {
            ILCursor cursor = new(il);
            cursor.Goto(call.Previous.Previous, MoveType.Before);
            cursor.EmitDelegate(ShouldSuppressNativeTitleUi);
            cursor.Emit(OpCodes.Brtrue, call.Next);
        }

        private static FieldInfo RequireField(FieldInfo field, string name, Type fieldType) {
            if (field == null || field.FieldType != fieldType) {
                throw new MissingFieldException(name);
            }
            return field;
        }

        private static MethodInfo RequireMethod(MethodInfo method, string name) {
            if (method == null || method.ReturnType != typeof(void)) {
                throw new MissingMethodException(name);
            }
            return method;
        }

        private static void DisableBridge(string reason, Exception ex) {
            bridgeOperational = false;
            Interlocked.Exchange(ref pendingAction, NoPendingAction);
            LogFailure(reason, ex);
        }

        private static void LogFailure(string message, Exception ex) {
            if (Interlocked.Exchange(ref failureLogged, 1) != 0) {
                return;
            }

            try {
                ModLoader.GetMod("CalamityOverhaul")?.Logger.Warn($"{message}: {ex}");
            } catch {
            }
        }

        private static void DisposeHooks() {
            try {
                modMenuInnerHook?.Dispose();
            } catch {
            }
            try {
                mainDrawMenuHook?.Dispose();
            } catch {
            }
            modMenuInnerHook = null;
            mainDrawMenuHook = null;
        }

        private static void ResetState(bool clearSubscribers) {
            bridgeOperational = false;
            themeActive = false;
            titleFrameActive = false;
            mainPatchReady = false;
            modMenuPatchReady = false;
            selectedMenuField = null;
            interfaceAddMenuButtonsMethod = null;
            offsetModMenuMethod = null;
            drawSocialMediaButtonsMethod = null;
            drawTmlSocialMediaButtonsMethod = null;
            drawVersionNumberMethod = null;
            CustomSwitchRect = Rectangle.Empty;
            Interlocked.Exchange(ref pendingAction, NoPendingAction);
            Interlocked.Exchange(ref failureLogged, 0);
            if (clearSubscribers) {
                FrameUpdate = null;
            }
        }
    }

    [Autoload(Side = ModSide.Client)]
    internal sealed class HimayoMenuVanillaBridgeLoader : ModSystem
    {
        public override void Load() => HimayoMenuVanillaBridge.LoadHooks();

        public override void Unload() => HimayoMenuVanillaBridge.UnloadHooks();
    }
}
