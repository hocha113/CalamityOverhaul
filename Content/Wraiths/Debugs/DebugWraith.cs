using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.PRT;
using Microsoft.Xna.Framework;
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
    /// 无色试件：以零创意成本压测框架全环
    /// （注册 → 调度(环境/据点双通道) → 显形 → 行为 → 感知事件 → 死机窗口 → 仪式 →
    /// 赋力/戒律 → 反噬挣脱 → 消散 → 进度落档）。
    /// 主题厉鬼落地后它仍保留，当框架回归试金石
    /// </summary>
    internal sealed class DebugWraith : WraithDefinition
    {
        public override Type ActorType => typeof(DebugWraithActor);
        //调试件永远沉底且不进任何名录
        public override int SortOrder => int.MaxValue;
        public override bool HiddenFromCatalog => true;
        public override int PresentDurationLimit => 60 * 40;
        public override int HaltWindowTicks => 60 * 10;

        public override void BuildBehaviors(List<IWraithBehavior> behaviors) {
            //behaviors.Add(new HoverWanderBehavior(240f, 1.2f));
            //behaviors.Add(new KeepDistanceBehavior(300f, 90f, 1.6f));
            //behaviors.Add(new FreezeWhenGazedBehavior(0.78f));
        }

        public override WraithSpawnRule GetSpawnRule() => new() {
            Condition = _ => WraithDirector.DebugHauntEnabled,
            ChancePerCheck = 0.6f,
            CooldownTicks = 60 * 10,
            MaxAlive = 2,
        };

        /// <summary>调试据点：只能被调试器手工落锚，恒真条件短冷却，压测据点调度与存档全环</summary>
        public override WraithSitePlan GetSitePlan() => new() {
            AnchorPicker = null,
            ActivationCondition = null,
            TriggerRadius = 900f,
            CooldownTicks = 60 * 12,
        };

        public override WraithAbility CreateAbility() => new DebugPulseAbility();
    }

    /// <summary>
    /// 试件实体：事件钩子只做可听见的回执。常规态触碰即消散（验证事件闭环）；
    /// 挣脱态触碰改为进入死机窗口（压测"反噬 → 重收伏"链）
    /// </summary>
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
    /// 「占位」赋力：以持刀人为心的一记灵压脉冲，迟滞周遭凡类（boss 不吃迟滞）。
    /// 戒律「不得空唤」：身周无可慑之敌仍强行借力即犯戒——戒律管线的试金石
    /// </summary>
    internal sealed class DebugPulseAbility : WraithAbility
    {
        private const float Radius = 340f;

        public override int CooldownTicks => 60 * 4;
        public override float ErosionCost => 0.10f;
        public override float MasteryWear => 0.015f;

        public override WraithCastResult Cast(WraithAbilityContext ctx) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.CanBeChasedBy()
                    && Vector2.DistanceSquared(npc.Center, ctx.Player.Center) < Radius * Radius) {
                    return WraithCastResult.Success;
                }
            }
            return WraithCastResult.Taboo;
        }

        public override void ExecuteWorld(Player caster, Vector2 aim, float mastery) {
            //迟滞时长吃驾驭度:0.22 出厂位约 2.6s,认主 0.85 位约 4.1s
            int slowTicks = (int)(60f * (1.5f + 2.5f * mastery));
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || npc.boss) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, caster.Center) < Radius * Radius) {
                    npc.AddBuff(BuffID.Slow, slowTicks);
                }
            }
        }

        public override void PlayWorldFx(Player caster, Vector2 aim) {
            const int Motes = 26;
            for (int i = 0; i < Motes; i++) {
                float angle = MathHelper.TwoPi * i / Motes;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5.5f, 8f);
                PRTLoader.NewParticle<PRT_Smoke>(caster.Center, vel, new Color(150, 160, 185) * 0.55f
                    , Main.rand.NextFloat(0.18f, 0.26f))
                    ?.Configure(Main.rand.Next(20, 30), 0.4f);
            }
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = 0.35f, Volume = 0.5f }, caster.Center);
        }
    }

    /// <summary>
    /// 厉鬼框架多模调试器：右键轮换模式，左键执行（部分模式 Shift+左键为反操作）。
    /// 模式：显形 / 死机 / 据点 / 绑定 / 反噬 / 侵蚀 / 必死 / 闹鬼——覆盖框架全环的游戏内验证
    /// </summary>
    internal class WraithDebugTool : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.SpectreStaff;

        private enum DebugMode : byte { Materialize, Halt, Site, Bind, Backlash, Erosion, Omen, Haunt }
        private const int ModeCount = 8;
        //本端调试偏好,非玩法状态(同 DebugHauntEnabled 惯例,单人调试用)
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
        public static LocalizedText HaltNoTarget { get; private set; }
        public static LocalizedText SitePlanted { get; private set; }
        public static LocalizedText SiteCleared { get; private set; }
        public static LocalizedText BindDone { get; private set; }
        public static LocalizedText BacklashNeedBound { get; private set; }
        public static LocalizedText ErosionValue { get; private set; }
        public static LocalizedText OmenStarted { get; private set; }

        public override void SetStaticDefaults() {
            HauntOn = this.GetLocalization(nameof(HauntOn), () => "调试闹鬼已开启");
            HauntOff = this.GetLocalization(nameof(HauntOff), () => "调试闹鬼已关闭");
            ModePrefix = this.GetLocalization(nameof(ModePrefix), () => "模式：{0}");
            ModeMaterialize = this.GetLocalization(nameof(ModeMaterialize), () => "显形（左键在光标处显形试件）");
            ModeHalt = this.GetLocalization(nameof(ModeHalt), () => "死机（左键点鬼逼入死机，再点解除）");
            ModeSite = this.GetLocalization(nameof(ModeSite), () => "据点（左键落锚调试据点，Shift+左键拔锚）");
            ModeBind = this.GetLocalization(nameof(ModeBind), () => "绑定（左键把试件低驾驭上簿，Shift+左键高驾驭）");
            ModeBacklash = this.GetLocalization(nameof(ModeBacklash), () => "反噬（左键强制簿上试件挣脱）");
            ModeErosion = this.GetLocalization(nameof(ModeErosion), () => "侵蚀（左键+0.2，Shift+左键清零）");
            ModeOmen = this.GetLocalization(nameof(ModeOmen), () => "必死（左键对自己起三息预警拍）");
            ModeHaunt = this.GetLocalization(nameof(ModeHaunt), () => "闹鬼（左键翻转环境自动闹鬼闸门）");
            HaltNoTarget = this.GetLocalization(nameof(HaltNoTarget), () => "光标下没有鬼");
            SitePlanted = this.GetLocalization(nameof(SitePlanted), () => "调试据点已落锚——走近它等它显形");
            SiteCleared = this.GetLocalization(nameof(SiteCleared), () => "调试据点已拔锚");
            BindDone = this.GetLocalization(nameof(BindDone), () => "试件已上簿，驾驭度 {0}");
            BacklashNeedBound = this.GetLocalization(nameof(BacklashNeedBound), () => "簿上没有试件——先用绑定模式请它上簿");
            ErosionValue = this.GetLocalization(nameof(ErosionValue), () => "侵蚀 {0}");
            OmenStarted = this.GetLocalization(nameof(OmenStarted), () => "三息之后，命数当尽");
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
            _ => ModeHaunt,
        };

        private static bool ShiftHeld
            => Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
            || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);

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
                    if (ShiftHeld) {
                        if (VaultUtils.isClient) {
                            //拔锚走不了现成通道,调试拔锚只在单人受理(多人下重新落锚即可覆盖)
                            VaultUtils.Text(SiteCleared.Value, Color.Gray);
                        }
                        else {
                            WraithSiteSystem.Unanchor(definition.Key);
                            VaultUtils.Text(SiteCleared.Value, Color.Gray);
                        }
                        break;
                    }
                    if (VaultUtils.isClient) {
                        WraithNet.SendPlantSite(definition, Main.MouseWorld);
                    }
                    else {
                        WraithSiteSystem.Plant(definition.Key, Main.MouseWorld);
                    }
                    VaultUtils.Text(SitePlanted.Value, Color.LightGreen);
                    break;
                }
                case DebugMode.Bind: {
                    WraithVesselHandle vessel = WraithVessels.ResolveCarried(player);
                    if (!vessel.IsValid) {
                        VaultUtils.Text(WraithSystemText.PowerDeniedNoVessel.Value, Color.DarkGray);
                        break;
                    }
                    WraithProgressRecord record = vessel.Store.GetOrCreate(definition.Key);
                    record.State = WraithBindState.Bound;
                    record.Mastery = ShiftHeld ? 0.9f : 0.2f;
                    vessel.Store.BumpVersion();
                    VaultUtils.Text(BindDone.Format(record.Mastery.ToString("0.00")), Color.LightGreen);
                    break;
                }
                case DebugMode.Backlash: {
                    WraithVesselHandle vessel = WraithVessels.ResolveCarried(player);
                    if (!vessel.IsValid || !vessel.Store.TryGet(definition.Key, out WraithProgressRecord record)
                        || record.State != WraithBindState.Bound) {
                        VaultUtils.Text(BacklashNeedBound.Value, Color.DarkGray);
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
                    player.GetModPlayer<WraithPlayer>().StartOmen(definition, 180
                        , () => WraithLethality.Kill(player, definition));
                    VaultUtils.Text(OmenStarted.Value, new Color(190, 60, 70));
                    break;
                }
                case DebugMode.Haunt: {
                    //静态开关只翻本端,多人下服务器不受影响(单人调试用)
                    WraithDirector.DebugHauntEnabled = !WraithDirector.DebugHauntEnabled;
                    VaultUtils.Text(WraithDirector.DebugHauntEnabled ? HauntOn.Value : HauntOff.Value,
                        WraithDirector.DebugHauntEnabled ? Color.LightGreen : Color.Gray);
                    break;
                }
            }
            return true;
        }

        /// <summary>光标点选厉鬼：命中箱含光标优先，否则 140px 内最近者</summary>
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
