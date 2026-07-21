using CalamityOverhaul.Content.Wraiths.Core;
using Microsoft.Xna.Framework;
using System;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 焦黑枯手。规则卡见 WRAITHS-GHOSTHAND-PLAN.md；Key 不可改
    /// </summary>
    internal sealed class GhostHand : WraithDefinition
    {
        public override Type ActorType => typeof(GhostHandActor);
        public override int SortOrder => 60;

        //扁宽命中箱
        public override int HitboxWidth => 52;
        public override int HitboxHeight => 40;

        //显形 255t，死机 10s，在场顶 4min
        public override int MaterializeFrames => 255;
        public override int DematerializeFrames => 45;
        public override int PresentDurationLimit => 60 * 240;
        public override int HaltWindowTicks => 600;

        //焦炭/余烬色
        public override Color BaseColor => new(30, 26, 24);
        public override Color EyeColor => new(214, 92, 32);

        //====规则专属文案（LoadExtraLocalization 装载，各件静态取用）====
        /// <summary>NPC 残句 1~5</summary>
        public static LocalizedText Rumor1 { get; private set; }
        public static LocalizedText Rumor2 { get; private set; }
        public static LocalizedText Rumor3 { get; private set; }
        public static LocalizedText Rumor4 { get; private set; }
        public static LocalizedText Rumor5 { get; private set; }
        /// <summary>烫退浮字</summary>
        public static LocalizedText ScorchRelease { get; private set; }
        /// <summary>烫退已花浮字</summary>
        public static LocalizedText ScorchSpent { get; private set; }
        /// <summary>借力超射程浮字</summary>
        public static LocalizedText GraspTooFar { get; private set; }
        /// <summary>犯戒浮字</summary>
        public static LocalizedText TabooEcho { get; private set; }
        /// <summary>反噬取锁浮字</summary>
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

        //BuildBehaviors 留空，逻辑自持于 Actor

        protected override WraithSitePlan GetSitePlan() => GhostHandSite.BuildPlan();

        public override WraithAbility CreateAbility() => new GhostHandGripAbility();
    }
}
