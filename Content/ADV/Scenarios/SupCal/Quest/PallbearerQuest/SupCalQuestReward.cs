using CalamityMod.Items.Materials;
using CalamityMod.NPCs.Providence;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using System;
using System.IO;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.SupCal.Quest.PallbearerQuest
{
    /// <summary>
    /// 完成扶柩者任务后的奖励场景
    /// </summary>
    internal class SupCalQuestReward : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SupCalQuestReward);
        public string LocalizationCategory => "Legend.HalibutText.ADV";

        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;

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

        private const string expressionCloseEye = " ";
        private const string expressionSmile = " " + " ";

        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "至尊灾厄");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");

            Line1 = this.GetLocalization(nameof(Line1), () => "做的不错");
            Line2 = this.GetLocalization(nameof(Line2), () => "这把弩挺适合你的");
            Line3 = this.GetLocalization(nameof(Line3), () => "你帮我解决了一个麻烦");
            Line4 = this.GetLocalization(nameof(Line4), () => "作为奖励，这些就归你了");
            Line5 = this.GetLocalization(nameof(Line5), () => "还有这把刀，我需要你拿着它去干掉那只虫子，放心，报酬会更丰富");
            Line6 = this.GetLocalization(nameof(Line6), () => "我施加了一部分硫火灵异在上面，可以让你轻松一些");
            Line7 = this.GetLocalization(nameof(Line7), () => "......");
        }

        protected override void OnScenarioStart() {
            SupCalSkyEffect.IsActive = true;
        }

        protected override void OnScenarioComplete() {
            SupCalSkyEffect.IsActive = false;
        }

        protected override void Build() {
            //注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionCloseEye, ADVAsset.SupCalADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionCloseEye, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionSmile, ADVAsset.SupCalADV[1]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionSmile, silhouette: false);

            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.Helen_solemnADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);

            bool hasHalibut = false;
            try {
                if (Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    hasHalibut = halibutPlayer.HasHalubut;
                }
            } catch {
                hasHalibut = false;
            }

            //添加对话
            Add(Rolename1.Value + expressionSmile, Line1.Value);
            Add(Rolename1.Value, Line2.Value);
            Add(Rolename1.Value, Line3.Value);
            Add(Rolename1.Value + expressionSmile, Line4.Value);//奖励
            Add(Rolename1.Value + expressionCloseEye, Line5.Value);
            Add(Rolename1.Value, Line6.Value);
            if (hasHalibut) {
                Add(Rolename2.Value, Line7.Value);
            }
        }

        public override void PreProcessSegment(DialogueBoxBase.DialoguePreProcessArgs args) {
            if (args.Index == 3) {//在Line4时发放奖励
                ADVRewardPopup.ShowReward(ModContent.ItemType<AshesofAnnihilation>(), 199, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.Brimstone);
            }
            if (args.Index == 4) {//在Line5时发放新的任务武器
                ADVRewardPopup.ShowReward(ModContent.ItemType<Heartcarver>(), 1, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f - 60);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f - 60);//高一点点
                    }, offset: Vector2.Zero, styleProvider: () => ADVRewardPopup.RewardStyle.Brimstone);
            }
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            PallbearerQuestTracker.Update();

            if (!save.SupCalQuestReward) {
                return;
            }

            if (save.SupCalQuestRewardSceneComplete) {
                return;
            }

            if (!PallbearerQuestRewardTrigger.Spawned) {
                return;
            }

            if (--PallbearerQuestRewardTrigger.RandomTimer > 0) {
                return;
            }

            if (ScenarioManager.Start<SupCalQuestReward>()) {
                save.SupCalQuestRewardSceneComplete = true;
                PallbearerQuestRewardTrigger.Spawned = false;
            }
        }
    }

    /// <summary>
    /// 追踪玩家使用扶柩者击杀亵渎天神
    /// </summary>
    internal class PallbearerQuestTracker : GlobalNPC, IWorldInfo
    {
        //伤害追踪数据
        private static float pallbearerDamageDealt = 0f;
        private static float totalBossDamage = 0f;
        private const float REQUIRED_CONTRIBUTION = 0.8f; //80%伤害贡献度要求
        private static bool isBossFightActive = false;
        
        void IWorldInfo.OnWorldLoad() {
            PallbearerQuestRewardTrigger.Spawned = false;
            PallbearerQuestRewardTrigger.RandomTimer = 0;
            ResetDamageTracking();
        }

        private static void ResetDamageTracking() {
            pallbearerDamageDealt = 0f;
            totalBossDamage = 0f;
            isBossFightActive = false;
        }

        public static void Update() {
            if (isBossFightActive) {
                if (!NPC.AnyNPCs(ModContent.NPCType<Providence>())) {
                    isBossFightActive = false;
                    ResetDamageTracking();
                }
            }
        }

        public override void AI(NPC npc) {
            if (npc.type != ModContent.NPCType<Providence>()) {
                return;
            }

            if (Main.LocalPlayer.GetItem().type != ModContent.ItemType<Pallbearer>()) {
                return;
            }

            //Boss存在时标记战斗激活
            isBossFightActive = npc.active;
            totalBossDamage = npc.lifeMax;
        }

        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers) {
            if (npc.type != ModContent.NPCType<Providence>()) {
                return;
            }

            //使用回调来追踪实际造成的伤害
            modifiers.ModifyHitInfo += Modifiers_ModifyHitInfo;

            void Modifiers_ModifyHitInfo(ref NPC.HitInfo info) {
                totalBossDamage += info.Damage;
                if (item.type == ModContent.ItemType<Pallbearer>()) {
                    pallbearerDamageDealt += info.Damage;
                }
            }
        }

        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers) {
            if (npc.type != ModContent.NPCType<Providence>()) {
                return;
            }

            modifiers.ModifyHitInfo += Modifiers_ModifyHitInfo;

            void Modifiers_ModifyHitInfo(ref NPC.HitInfo info) {
                totalBossDamage += info.Damage;
                //检测弹幕是否来自扶柩者
                if (IsFromPallbearer(projectile)) {
                    pallbearerDamageDealt += info.Damage;
                }
            }
        }

        private static bool IsFromPallbearer(Projectile projectile) {
            //检测弹幕类型是否属于扶柩者系列
            return projectile.type == ModContent.ProjectileType<PallbearerHeld>() ||
                   projectile.type == ModContent.ProjectileType<PallbearerArrow>() ||
                   projectile.type == ModContent.ProjectileType<PallbearerBoomerang>();
        }

        public override void OnKill(NPC npc) {
            if (npc.type != ModContent.NPCType<Providence>()) {
                return;
            }

            Check();

            //仅服务器发送
            if (VaultUtils.isServer) {
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.PallbearerQuestTracker);
                packet.Write(pallbearerDamageDealt);
                packet.Write(totalBossDamage);
                packet.Send();
            }

            //重置追踪数据
            ResetDamageTracking();
        }

        internal static void NetHandle(CWRMessageType type, BinaryReader reader, int whoAmI) {
            if (!VaultUtils.isClient) {
                return; //仅客户端处理
            }
            if (type == CWRMessageType.PallbearerQuestTracker) {
                pallbearerDamageDealt = reader.ReadSingle();
                totalBossDamage = reader.ReadSingle();
                Check();
                ResetDamageTracking();
            }
        }

        public static void Check() {
            Player player = Main.LocalPlayer;
            if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                return;
            }

            //检查是否接受了任务
            if (!halibutPlayer.ADCSave.SupCalQuestAccepted || halibutPlayer.ADCSave.SupCalQuestDeclined) {
                return;
            }

            //必须最后一击使用扶柩者
            Item heldItem = player.GetItem();
            if (heldItem.type != ModContent.ItemType<Pallbearer>()) {
                //显示失败提示
                CombatText.NewText(player.Hitbox, Color.Red, "任务失败: 未使用扶柩者完成最后一击");
                return;
            }

            //并且造成足够的伤害贡献
            float contribution = totalBossDamage > 0 ? pallbearerDamageDealt / totalBossDamage : 0f;
            if (contribution < REQUIRED_CONTRIBUTION) {
                CombatText.NewText(player.Hitbox, Color.Orange,
                    $"任务失败: 扶柩者伤害占比不足 ({contribution:P0}/{REQUIRED_CONTRIBUTION:P0})");
                return;
            }

            //任务完成
            halibutPlayer.ADCSave.SupCalQuestReward = true;

            //延迟触发奖励场景
            PallbearerQuestRewardTrigger.Spawned = true;
            PallbearerQuestRewardTrigger.RandomTimer = 60 * Main.rand.Next(3, 5);

            //显示成功提示
            CombatText.NewText(player.Hitbox, Color.Gold,
                $"任务完成! 扶柩者伤害占比: {contribution:P0}");
        }

        //获取当前伤害追踪数据供UI使用
        public static (float pallbearerDamage, float totalDamage, bool isActive) GetDamageTrackingData() {
            return (pallbearerDamageDealt, totalBossDamage, isBossFightActive);
        }
    }

    internal static class PallbearerQuestRewardTrigger
    {
        public static bool Spawned = false;
        public static int RandomTimer;
    }
}
