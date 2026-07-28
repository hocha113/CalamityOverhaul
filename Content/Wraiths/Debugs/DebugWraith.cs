using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using CalamityOverhaul.Content.Wraiths.Runtime.Behaviors;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Debugs
{
    /// <summary>
    /// 无色试件，框架回归试金石
    /// </summary>
    internal sealed class DebugWraith : WraithDefinition
    {
        /// <summary>调试据点武装闸，会话级</summary>
        internal static bool DebugSiteArmed;

        /// <summary>调试必死死因，{0}=玩家名</summary>
        public LocalizedText OmenDeath { get; private set; }

        public override Type ActorType => typeof(DebugWraithActor);
        //调试件沉底；闹鬼闸开临时上目录
        public override int SortOrder => int.MaxValue;
        public override bool HiddenFromCatalog => !WraithDirector.DebugHauntEnabled;
        public override int PresentDurationLimit => 60 * 40;
        public override int HaltWindowTicks => 60 * 10;
        //外部死机白名单，仅试件
        public override bool AllowExternalHaltRequest => true;
        //豁免上线闸，自持调试闸
        public override bool IsDebugContent => true;

        protected override void LoadExtraLocalization() {
            OmenDeath = this.GetLocalization(nameof(OmenDeath), () => "{0}没有在三息之内挣脱试件的注视");
        }

        public override void BuildBehaviors(List<IWraithBehavior> behaviors) {
            //三块积木全挂；冻结阻尼须挡住游荡+保距同向推
            behaviors.Add(new HoverWanderBehavior(240f, 1.2f));
            behaviors.Add(new KeepDistanceBehavior(300f, 90f, 1.6f));
            behaviors.Add(new FreezeWhenGazedBehavior());
        }

        protected override WraithSpawnRule GetSpawnRule() => new() {
            Condition = _ => WraithDirector.DebugHauntEnabled,
            ChancePerCheck = 0.6f,
            CooldownTicks = 60 * 10,
            //同屏一鬼，互斥在 Materialize
            MaxAlive = 1,
        };

        /// <summary>调试据点，锚定与活化吃 DebugSiteArmed 闸</summary>
        protected override WraithSitePlan GetSitePlan() => new() {
            AnchorPicker = PickDebugAnchor,
            ActivationCondition = _ => DebugSiteArmed,
            TriggerRadius = 900f,
            CooldownTicks = 60 * 12,
            AnchorRetryTicks = 60 * 5,
        };

        /// <summary>周边 260~520px 环带选锚，未武装返回 null</summary>
        private Vector2? PickDebugAnchor(WraithSiteContext ctx) {
            if (!DebugSiteArmed || ctx.Candidate == null) {
                return null;
            }
            for (int attempt = 0; attempt < 12; attempt++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 center = ctx.Candidate.Center + angle.ToRotationVector2() * Main.rand.NextFloat(260f, 520f);
                Vector2 topLeft = center - new Vector2(HitboxWidth * 0.5f, HitboxHeight * 0.5f);
                if (!Collision.SolidCollision(topLeft, HitboxWidth, HitboxHeight)) {
                    return center;
                }
            }
            return null;
        }
    }

    /// <summary>试件实体。触碰即消散；挣脱态触碰进死机</summary>
    internal sealed class DebugWraithActor : WraithActor
    {
        protected override void OnGazeStart(Player player) {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = 0.4f, Volume = 0.5f }, Center);
            }
        }

        protected override void OnPlayerApproach(Player player) {
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.3f, Volume = 0.5f }, Center);
            }
        }

        protected override void OnTouch(Player player) {
            if (IsEscaped) {
                BeginHalt();
            }
            else {
                BeginDematerialize();
            }
        }
    }

    /// <summary>
    /// 多模调试器。右键换模式，左键执行；权威通道不可用时明示不受理
    /// </summary>
    internal class WraithDebugTool : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.SpectreStaff;

        //上线闸关不加载
        public override bool IsLoadingEnabled(Mod mod) => WraithDirector.LiveContentEnabled;

        private enum DebugMode : byte { Materialize, Halt, Site, Bind, Backlash, Erosion, Omen, Haunt, Register }
        private const int ModeCount = 9;
        //本端调试偏好
        private static int mode;

        public static LocalizedText HauntOn { get; private set; }
        public static LocalizedText HauntOff { get; private set; }
        public static LocalizedText ModePrefix { get; private set; }
        public static LocalizedText ModeMaterialize { get; private set; }
        public static LocalizedText ModeHalt { get; private set; }
        public static LocalizedText ModeSite { get; private set; }
        public static LocalizedText ModeBind { get; private set; }
        public static LocalizedText ModeBacklash { get; private set; }
        public static LocalizedText ModeErosion { get; private set; }
        public static LocalizedText ModeOmen { get; private set; }
        public static LocalizedText ModeHaunt { get; private set; }
        public static LocalizedText ModeRegister { get; private set; }
        public static LocalizedText HaltNoTarget { get; private set; }
        public static LocalizedText SitePlanted { get; private set; }
        public static LocalizedText SiteCleared { get; private set; }
        public static LocalizedText SiteArmed { get; private set; }
        public static LocalizedText SiteDisarmed { get; private set; }
        public static LocalizedText BindDone { get; private set; }
        public static LocalizedText BacklashNeedBound { get; private set; }
        public static LocalizedText BacklashNeedRestless { get; private set; }
        public static LocalizedText EncounterBusy { get; private set; }
        public static LocalizedText ErosionValue { get; private set; }
        public static LocalizedText OmenStarted { get; private set; }
        public static LocalizedText MultiplayerDenied { get; private set; }
        public static LocalizedText RegisterHeader { get; private set; }
        public static LocalizedText RegisterLine { get; private set; }
        public static LocalizedText RegisterEmpty { get; private set; }

        public override void SetStaticDefaults() {
            HauntOn = this.GetLocalization(nameof(HauntOn), () => "调试闹鬼已开启（试件临时上目录）");
            HauntOff = this.GetLocalization(nameof(HauntOff), () => "调试闹鬼已关闭（试件恢复隐藏）");
            ModePrefix = this.GetLocalization(nameof(ModePrefix), () => "模式：{0}");
            ModeMaterialize = this.GetLocalization(nameof(ModeMaterialize), () => "显形（左键在光标处显形试件）");
            ModeHalt = this.GetLocalization(nameof(ModeHalt), () => "死机（左键点鬼逼入死机，再点解除）");
            ModeSite = this.GetLocalization(nameof(ModeSite), () => "据点（左键落锚，Shift+左键拔锚，Ctrl+左键开关武装闸）");
            ModeBind = this.GetLocalization(nameof(ModeBind), () => "绑定（左键低驾驭上簿，Shift+左键高驾驭+续契）");
            ModeBacklash = this.GetLocalization(nameof(ModeBacklash), () => "反噬（左键强制簿上试件挣脱，须躁动）");
            ModeErosion = this.GetLocalization(nameof(ModeErosion), () => "侵蚀（左键+0.2，Shift+左键清零）");
            ModeOmen = this.GetLocalization(nameof(ModeOmen), () => "必死（左键对自己起三息预警拍）");
            ModeHaunt = this.GetLocalization(nameof(ModeHaunt), () => "闹鬼（左键翻转环境自动闹鬼闸门）");
            ModeRegister = this.GetLocalization(nameof(ModeRegister), () => "读簿（左键打印随身簿面进度）");
            HaltNoTarget = this.GetLocalization(nameof(HaltNoTarget), () => "光标下没有鬼");
            SitePlanted = this.GetLocalization(nameof(SitePlanted), () => "调试据点已落锚——走近它等它显形");
            SiteCleared = this.GetLocalization(nameof(SiteCleared), () => "调试据点已拔锚");
            SiteArmed = this.GetLocalization(nameof(SiteArmed), () => "据点武装闸已开——动态锚定与活化条件放行");
            SiteDisarmed = this.GetLocalization(nameof(SiteDisarmed), () => "据点武装闸已关——它不会应门");
            BindDone = this.GetLocalization(nameof(BindDone), () => "试件已上簿，驾驭度 {0}，续签 {1}");
            BacklashNeedBound = this.GetLocalization(nameof(BacklashNeedBound), () => "簿上没有试件——先用绑定模式请它上簿");
            BacklashNeedRestless = this.GetLocalization(nameof(BacklashNeedRestless), () => "试件不躁动，挣不出来——先低驾驭上簿");
            EncounterBusy = this.GetLocalization(nameof(EncounterBusy), () => "遭遇进行中——同屏只容一鬼");
            ErosionValue = this.GetLocalization(nameof(ErosionValue), () => "侵蚀 {0}");
            OmenStarted = this.GetLocalization(nameof(OmenStarted), () => "三息之后，命数当尽");
            MultiplayerDenied = this.GetLocalization(nameof(MultiplayerDenied), () => "该调试操作多人下不受理");
            RegisterHeader = this.GetLocalization(nameof(RegisterHeader), () => "—— 随身簿面 ——");
            RegisterLine = this.GetLocalization(nameof(RegisterLine), () => "{0}：{1}，驾驭 {2}，续签 {3}，遭遇 {4}");
            RegisterEmpty = this.GetLocalization(nameof(RegisterEmpty), () => "随身没有载体");
        }

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 40;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 20;
            Item.noMelee = true;
            Item.rare = ItemRarityID.Red;
            Item.value = 0;
            Item.UseSound = SoundID.Item8;
        }

        public override bool AltFunctionUse(Player player) => true;

        private static LocalizedText ModeName(int value) => (DebugMode)value switch {
            DebugMode.Materialize => ModeMaterialize,
            DebugMode.Halt => ModeHalt,
            DebugMode.Site => ModeSite,
            DebugMode.Bind => ModeBind,
            DebugMode.Backlash => ModeBacklash,
            DebugMode.Erosion => ModeErosion,
            DebugMode.Omen => ModeOmen,
            DebugMode.Haunt => ModeHaunt,
            _ => ModeRegister,
        };

        private static bool ShiftHeld
            => Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
            || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);

        private static bool CtrlHeld
            => Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl)
            || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightControl);

        /// <summary>多人权威不可用则明示不受理</summary>
        private static bool DenyInMultiplayer() {
            if (!VaultUtils.isClient) {
                return false;
            }
            VaultUtils.Text(MultiplayerDenied.Value, Color.Gray);
            return true;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }
            if (!WraithRegistry.TryGet(nameof(DebugWraith), out WraithDefinition definition)) {
                return true;
            }

            if (player.altFunctionUse == 2) {
                mode = (mode + 1) % ModeCount;
                VaultUtils.Text(ModePrefix.Format(ModeName(mode).Value), Color.LightGray);
                return true;
            }

            switch ((DebugMode)mode) {
                case DebugMode.Materialize: {
                    //多人明示不受理
                    if (DenyInMultiplayer()) {
                        break;
                    }
                    if (WraithDirector.EncounterInProgress()) {
                        VaultUtils.Text(EncounterBusy.Value, Color.Gray);
                        break;
                    }
                    Vector2 topLeft = Main.MouseWorld
                        - new Vector2(definition.HitboxWidth * 0.5f, definition.HitboxHeight * 0.5f);
                    WraithDirector.Materialize(definition, topLeft);
                    break;
                }
                case DebugMode.Halt: {
                    WraithActor target = PickWraithAtCursor();
                    if (target == null) {
                        VaultUtils.Text(HaltNoTarget.Value, Color.Gray);
                        break;
                    }
                    bool halt = !target.IsHalted;
                    if (VaultUtils.isClient) {
                        //服复核存活/持刀/判距
                        WraithNet.SendHaltRequest(target, halt);
                    }
                    else if (halt) {
                        target.BeginHalt();
                    }
                    else {
                        target.EndHalt();
                    }
                    break;
                }
                case DebugMode.Site: {
                    if (DenyInMultiplayer()) {
                        break;
                    }
                    if (CtrlHeld) {
                        DebugWraith.DebugSiteArmed = !DebugWraith.DebugSiteArmed;
                        VaultUtils.Text(DebugWraith.DebugSiteArmed ? SiteArmed.Value : SiteDisarmed.Value,
                            DebugWraith.DebugSiteArmed ? Color.LightGreen : Color.Gray);
                        break;
                    }
                    if (ShiftHeld) {
                        WraithSiteSystem.Unanchor(definition.Key);
                        VaultUtils.Text(SiteCleared.Value, Color.Gray);
                        break;
                    }
                    //落锚自动武装并清冷却
                    WraithSiteSystem.Plant(definition.Key, Main.MouseWorld);
                    WraithSiteSystem.ResetCooldown(definition.Key);
                    DebugWraith.DebugSiteArmed = true;
                    VaultUtils.Text(SitePlanted.Value, Color.LightGreen);
                    break;
                }
                case DebugMode.Bind: {
                    WraithVesselHandle vessel = WraithVessels.ResolveCarried(player);
                    if (!vessel.IsValid) {
                        VaultUtils.Text(WraithSystemText.VesselRequired.Value, Color.DarkGray);
                        break;
                    }
                    WraithProgressRecord record = vessel.Store.GetOrCreate(definition.Key);
                    record.State = WraithBindState.Bound;
                    record.Mastery = ShiftHeld ? 0.9f : 0.2f;
                    //Shift=续契位
                    record.PactRenewed = ShiftHeld;
                    vessel.Store.BumpVersion();
                    WraithVessels.SyncSlot(player, vessel.Item);
                    VaultUtils.Text(BindDone.Format(record.Mastery.ToString("0.00"), record.PactRenewed ? "✓" : "✗"), Color.LightGreen);
                    break;
                }
                case DebugMode.Backlash: {
                    WraithVesselHandle vessel = WraithVessels.ResolveCarried(player);
                    if (!vessel.IsValid || !vessel.Store.TryGet(definition.Key, out WraithProgressRecord record)
                        || record.State != WraithBindState.Bound) {
                        VaultUtils.Text(BacklashNeedBound.Value, Color.DarkGray);
                        break;
                    }
                    //不躁动则明说
                    if (record.Mastery >= WraithDefinition.RestlessThreshold) {
                        VaultUtils.Text(BacklashNeedRestless.Value, Color.DarkGray);
                        break;
                    }
                    if (WraithDirector.EncounterInProgress()) {
                        VaultUtils.Text(EncounterBusy.Value, Color.Gray);
                        break;
                    }
                    WraithBacklash.Trigger(player, definition);
                    break;
                }
                case DebugMode.Erosion: {
                    WraithPlayer wraithPlayer = player.GetModPlayer<WraithPlayer>();
                    if (ShiftHeld) {
                        wraithPlayer.SetErosion(0f);
                    }
                    else {
                        wraithPlayer.AddErosion(0.2f);
                    }
                    VaultUtils.Text(ErosionValue.Format(wraithPlayer.Erosion.ToString("0.00")), Color.MediumPurple);
                    break;
                }
                case DebugMode.Omen: {
                    //预警仅权威
                    if (DenyInMultiplayer()) {
                        break;
                    }
                    LocalizedText reason = (definition as DebugWraith)?.OmenDeath;
                    WraithLethality.StartOmen(player, definition, 180, reason);
                    VaultUtils.Text(OmenStarted.Value, new Color(190, 60, 70));
                    break;
                }
                case DebugMode.Haunt: {
                    //闸门仅权威
                    if (DenyInMultiplayer()) {
                        break;
                    }
                    WraithDirector.DebugHauntEnabled = !WraithDirector.DebugHauntEnabled;
                    VaultUtils.Text(WraithDirector.DebugHauntEnabled ? HauntOn.Value : HauntOff.Value,
                        WraithDirector.DebugHauntEnabled ? Color.LightGreen : Color.Gray);
                    break;
                }
                case DebugMode.Register: {
                    PrintRegister(player);
                    break;
                }
            }
            return true;
        }

        /// <summary>把随身簿面打进聊天</summary>
        private static void PrintRegister(Player player) {
            WraithVesselHandle vessel = WraithVessels.ResolveCarried(player);
            if (!vessel.IsValid) {
                VaultUtils.Text(RegisterEmpty.Value, Color.DarkGray);
                return;
            }
            VaultUtils.Text(RegisterHeader.Value, Color.LightGray);
            foreach ((string key, WraithProgressRecord record) in vessel.Store.Records) {
                string name = WraithRegistry.TryGet(key, out WraithDefinition definition)
                    ? definition.DisplayName.Value : key;
                VaultUtils.Text(RegisterLine.Format(name, record.State, record.Mastery.ToString("0.00"),
                    record.PactRenewed ? "✓" : "✗", record.EncounterCount), Color.LightGray);
            }
        }

        /// <summary>光标点选厉鬼，140px 内最近</summary>
        private static WraithActor PickWraithAtCursor() {
            Vector2 mouse = Main.MouseWorld;
            WraithActor best = null;
            float bestSq = 140f * 140f;
            foreach (WraithActor wraith in InnoVault.Actors.ActorLoader.GetActiveActors<WraithActor>()) {
                if (wraith.HitBox.Contains(mouse.ToPoint())) {
                    return wraith;
                }
                float distSq = Vector2.DistanceSquared(mouse, wraith.Center);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    best = wraith;
                }
            }
            return best;
        }
    }
}
