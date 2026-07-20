using CalamityOverhaul.Content.Wraiths.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 焦黑枯手：首只毕业的正典厉鬼（规则卡 v1 见 WRAITHS-GHOSTHAND-PLAN.md）。
    /// 规则：看着它，它不动；移开视线，它在爬。触到你=「攥」，拖进裂隙即死；
    /// 火烫它必松手，一场只肯被烫一次。反制主线=把「焦黑的长命锁」递到它面前。
    /// Key 即类型名"GhostHand"，存档锚不可改（鬼律 15）
    /// </summary>
    internal sealed class GhostHand : WraithDefinition
    {
        public override Type ActorType => typeof(GhostHandActor);
        public override int SortOrder => 60;

        //爬行的手,扁宽命中箱
        public override int HitboxWidth => 52;
        public override int HitboxHeight => 40;

        //显形 255t=180 潜壁+75 破壁;死机窗口 10 秒;在场硬顶 4 分钟
        public override int MaterializeFrames => 255;
        public override int DematerializeFrames => 45;
        public override int PresentDurationLimit => 60 * 240;
        public override int HaltWindowTicks => 600;

        //焦炭主色与余烬橙(死机浮字/仪式提示用色同源)
        public override Color BaseColor => new(30, 26, 24);
        public override Color EyeColor => new(214, 92, 32);

        //====规则专属文案（LoadExtraLocalization 装载，各件静态取用）====
        /// <summary>NPC 残句 1~5（公平"可先学"，权重见 <see cref="GhostHandRumors"/>）</summary>
        public static LocalizedText Rumor1 { get; private set; }
        public static LocalizedText Rumor2 { get; private set; }
        public static LocalizedText Rumor3 { get; private set; }
        public static LocalizedText Rumor4 { get; private set; }
        public static LocalizedText Rumor5 { get; private set; }
        /// <summary>烫退浮字（火的裁定生效）</summary>
        public static LocalizedText ScorchRelease { get; private set; }
        /// <summary>烫退已花浮字（一场一次的阀门已用尽，公平可读）</summary>
        public static LocalizedText ScorchSpent { get; private set; }
        /// <summary>借力超射程浮字</summary>
        public static LocalizedText GraspTooFar { get; private set; }
        /// <summary>犯戒「手不空回」浮字</summary>
        public static LocalizedText TabooEcho { get; private set; }
        /// <summary>反噬期回据点扒灰取锁浮字</summary>
        public static LocalizedText LockUnearthed { get; private set; }

        protected override void LoadExtraLocalization() {
            Rumor1 = this.GetLocalization(nameof(Rumor1), () => "岩层深处的矿道不干净。有人听见墙里在挠，一下，一下，像在数你的脚步。");
            Rumor2 = this.GetLocalization(nameof(Rumor2), () => "矿上的老人说：要是那只手爬出来，就盯着它。盯着它，它不动；移开眼，它在爬。");
            Rumor3 = this.GetLocalization(nameof(Rumor3), () => "别让墙里的手碰到你。叫它攥住的人，全都进了墙，一个都没回来。");
            Rumor4 = this.GetLocalization(nameof(Rumor4), () => "火烫得它松手。可一场里，火只救得了你一回。");
            Rumor5 = this.GetLocalization(nameof(Rumor5), () => "它在灰里刨了几十年，像在找什么。塌方那天它没抓住的东西，兴许还埋在裂缝底下的灰里。");
            ScorchRelease = this.GetLocalization(nameof(ScorchRelease), () => "它松手了——焦黑的五指在火光里蜷成一团");
            ScorchSpent = this.GetLocalization(nameof(ScorchSpent), () => "皮肉在火上滋响。这一次，它没有松手");
            GraspTooFar = this.GetLocalization(nameof(GraspTooFar), () => "太远了——它的手够不着");
            TabooEcho = this.GetLocalization(nameof(TabooEcho), () => "空手而回——五指在簿页上抓出五道焦痕");
            LockUnearthed = this.GetLocalization(nameof(LockUnearthed), () => "灰堆里翻出了一样东西");
        }

        //BuildBehaviors 刻意留空:游荡/保距语义与"贴壁爬行"不符,全部逻辑自持于 GhostHandActor

        protected override WraithSitePlan GetSitePlan() => GhostHandSite.BuildPlan();

        public override WraithAbility CreateAbility() => new GhostHandGripAbility();
    }
}
