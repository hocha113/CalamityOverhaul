using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using CalamityOverhaul.Content.Wraiths.Runtime.Behaviors;
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
    /// （注册 → 调度 → 显形 → 行为 → 感知事件 → 消散 → 世界进度落档）。
    /// 主题厉鬼落地后它仍保留，当框架回归试金石
    /// </summary>
    internal sealed class DebugWraith : WraithDefinition
    {
        public override Type ActorType => typeof(DebugWraithActor);
        //调试件永远沉底且不进任何名录
        public override int SortOrder => int.MaxValue;
        public override bool HiddenFromCatalog => true;
        public override int PresentDurationLimit => 60 * 40;

        public override void BuildBehaviors(List<IWraithBehavior> behaviors) {
            behaviors.Add(new HoverWanderBehavior(240f, 1.2f));
            behaviors.Add(new KeepDistanceBehavior(300f, 90f, 1.6f));
            behaviors.Add(new FreezeWhenGazedBehavior(0.78f));
        }

        public override WraithSpawnRule GetSpawnRule() => new() {
            Condition = _ => WraithDirector.DebugHauntEnabled,
            ChancePerCheck = 0.6f,
            CooldownTicks = 60 * 10,
            MaxAlive = 2,
        };
    }

    /// <summary>试件实体：事件钩子只做可听见的回执，触碰即消散验证事件链闭环</summary>
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

        protected override void OnTouch(Player player) => BeginDematerialize();
    }

    /// <summary>厉鬼框架调试物品：左键光标处显形试件，右键翻转自动闹鬼闸门</summary>
    internal class WraithDebugTool : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.SpectreStaff;

        public static LocalizedText HauntOn { get; private set; }
        public static LocalizedText HauntOff { get; private set; }

        public override void SetStaticDefaults() {
            HauntOn = this.GetLocalization(nameof(HauntOn), () => "调试闹鬼已开启");
            HauntOff = this.GetLocalization(nameof(HauntOff), () => "调试闹鬼已关闭");
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

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return true;
            }
            if (player.altFunctionUse == 2) {
                //右键:自动闹鬼闸门,静态开关只翻本端,多人下服务器不受影响(单人调试用)
                WraithDirector.DebugHauntEnabled = !WraithDirector.DebugHauntEnabled;
                VaultUtils.Text(WraithDirector.DebugHauntEnabled ? HauntOn.Value : HauntOff.Value,
                    WraithDirector.DebugHauntEnabled ? Color.LightGreen : Color.Gray);
            }
            else if (WraithRegistry.TryGet(nameof(DebugWraith), out WraithDefinition definition)) {
                //左键:点名显形,多人客户端经 NewActor 内建请求转发
                WraithDirector.Materialize(definition, Main.MouseWorld);
            }
            return true;
        }
    }
}
