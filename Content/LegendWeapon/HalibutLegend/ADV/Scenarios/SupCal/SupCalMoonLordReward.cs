﻿using CalamityMod.Items.Weapons.Ranged;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.SupCal
{
    internal class SupCalMoonLordReward : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SupCalMoonLordReward);
        public string LocalizationCategory => "Legend.HalibutText.ADV";

        //角色名称本地化
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }

        //对话文本本地化
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }

        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "至尊灾厄");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");

            Line1 = this.GetLocalization(nameof(Line1), () => "呵呵呵");
            Line2 = this.GetLocalization(nameof(Line2), () => "那个家伙已经沦落到这种地步了吗");
            Line3 = this.GetLocalization(nameof(Line3), () => "你知道现在的地底是什么景象吗?");
            Line4 = this.GetLocalization(nameof(Line4), () => "......所以你这次来是?");
            Line5 = this.GetLocalization(nameof(Line5), () => "送你点小玩具，顺带有个委托交给你");
            Line6 = this.GetLocalization(nameof(Line6), () => "一把小巧的弩，我需要你拿它干掉下面的那个苟延残喘吸食地热的家伙，记住只能用这个弩");
            Line7 = this.GetLocalization(nameof(Line7), () => "如果你做到了，我们的合作还能继续");
        }

        protected override void OnScenarioStart() {
            //开始生成粒子
            SupCalSkyEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            //停止粒子生成
            SupCalSkyEffect.IsActive = false;
        }

        protected override void Build() {
            //注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);

            //添加对话（使用本地化文本）
            Add(Rolename1.Value, Line1.Value);
            Add(Rolename1.Value, Line2.Value);
            Add(Rolename1.Value, Line3.Value);
            if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer) && halibutPlayer.HasHalubut) {
                Add(Rolename2.Value, Line4.Value);
            }
            Add(Rolename1.Value, Line5.Value);
            Add(Rolename1.Value, Line6.Value);//奖励
            Add(Rolename1.Value, Line7.Value);
        }

        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 5) { //Line6 - 奖励物品
                ADVRewardPopup.ShowReward(ModContent.ItemType<Condemnation>(), 1, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (save.SupCalMoonLordReward) {
                return;
            }
            // 必须先触发过FirstMetSupCal场景
            if (!save.FirstMetSupCal) {
                return;
            }
            // 必须选择了Choice1（拔出武器）
            if (!save.SupCalChoseToFight) {
                return;
            }
            // 必须击败了月球领主
            if (!SupCalMoonLordRewardNPC.Spawned) {
                return;
            }
            if (--SupCalMoonLordRewardNPC.RandomTimer > 0) {
                return;
            }
            if (ScenarioManager.Start<SupCalMoonLordReward>()) {
                save.SupCalMoonLordReward = true;
            }
        }
    }

    internal class SupCalMoonLordRewardNPC : GlobalNPC
    {
        public static bool Spawned = false;
        public static int RandomTimer;
        public override bool SpecialOnKill(NPC npc) {
            if (npc.type == NPCID.MoonLordCore) {
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(3, 5);//给一个3到5秒的缓冲时间，打完立刻触发不太合适
            }
            return false;
        }
    }
}
