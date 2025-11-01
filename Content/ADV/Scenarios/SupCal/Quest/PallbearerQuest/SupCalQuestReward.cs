using CalamityMod.Items.Materials;
using CalamityMod.NPCs.Providence;
using CalamityOverhaul.Content.Items.Melee;
using CalamityOverhaul.Content.Items.Ranged;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.GameContent;
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

        public static bool Spawned = false;
        public static int RandomTimer;

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

            if (!Spawned) {
                return;
            }

            if (--RandomTimer > 0) {
                return;
            }

            if (ScenarioManager.Start<SupCalQuestReward>()) {
                save.SupCalQuestRewardSceneComplete = true;
                Spawned = false;
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
            SupCalQuestReward.Spawned = false;
            SupCalQuestReward.RandomTimer = 0;
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
            SupCalQuestReward.Spawned = true;
            SupCalQuestReward.RandomTimer = 60 * Main.rand.Next(3, 5);

            //显示成功提示
            CombatText.NewText(player.Hitbox, Color.Gold,
                $"任务完成! 扶柩者伤害占比: {contribution:P0}");
        }

        //获取当前伤害追踪数据供UI使用
        public static (float pallbearerDamage, float totalDamage, bool isActive) GetDamageTrackingData() {
            return (pallbearerDamageDealt, totalBossDamage, isBossFightActive);
        }
    }

    /// <summary>
    /// 扶柩者任务追踪UI，显示伤害贡献度
    /// </summary>
    internal class PallbearerQuestTrackerUI : UIHandle, ILocalizedModType
    {
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        public static PallbearerQuestTrackerUI Instance => UIHandleLoader.GetUIHandleOfType<PallbearerQuestTrackerUI>();

        //本地化文本
        public static LocalizedText QuestTitle { get; private set; }
        public static LocalizedText DamageContribution { get; private set; }
        public static LocalizedText RequiredContribution { get; private set; }
        public static LocalizedText TargetBoss { get; private set; }

        //UI参数
        private const float PanelWidth = 280f;
        private const float PanelHeight = 120f;
        private static float ScreenX => 30f; //屏幕左侧
        private static float ScreenY => Main.screenHeight / 2f - PanelHeight / 2f; //垂直居中

        //动画参数
        private float slideProgress = 0f;
        private float pulseTimer = 0f;
        private float borderGlow = 1f;
        private float warningPulse = 0f;

        //伤害数据缓存
        private float cachedContribution = 0f;
        private const float UpdateInterval = 0.5f; //每0.5秒更新一次显示
        private float updateTimer = 0f;

        public override bool Active {
            get {
                if (!Main.LocalPlayer.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    return false;
                }

                //检查任务是否激活
                if (!halibutPlayer.ADCSave.SupCalQuestAccepted || halibutPlayer.ADCSave.SupCalQuestDeclined) {
                    return false;
                }

                //检查是否已完成
                if (halibutPlayer.ADCSave.SupCalQuestReward) {
                    return false;
                }

                //获取战斗状态
                return PallbearerQuestTracker.GetDamageTrackingData().isActive;
            }
        }

        public override void SetStaticDefaults() {
            QuestTitle = this.GetLocalization(nameof(QuestTitle), () => "委托：亵渎天神");
            DamageContribution = this.GetLocalization(nameof(DamageContribution), () => "扶柩者伤害占比");
            RequiredContribution = this.GetLocalization(nameof(RequiredContribution), () => "需求: 80%");
            TargetBoss = this.GetLocalization(nameof(TargetBoss), () => "目标: 亵渎天神");
        }

        public override void Update() {
            //展开/收起动画
            float targetSlide = Active ? 1f : 0f;
            slideProgress = MathHelper.Lerp(slideProgress, targetSlide, 0.15f);

            if (slideProgress < 0.01f) {
                return;
            }

            //动画更新
            pulseTimer += 0.03f;
            borderGlow = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2f) * 0.3f + 0.7f;

            //更新伤害数据显示
            updateTimer += 0.016f; //假设60fps
            if (updateTimer >= UpdateInterval) {
                updateTimer = 0f;
                var trackingData = PallbearerQuestTracker.GetDamageTrackingData();
                if (trackingData.totalDamage > 0) {
                    cachedContribution = trackingData.pallbearerDamage / trackingData.totalDamage;
                }
            }

            //如果贡献度低，闪烁警告
            if (cachedContribution < 0.3f) {
                warningPulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f;
            }
            else {
                warningPulse = 0f;
            }

            //设置UI位置
            float offsetX = MathHelper.Lerp(-PanelWidth - 50f, ScreenX, CWRUtils.EaseOutCubic(slideProgress));
            DrawPosition = new Vector2(offsetX, ScreenY);
            Size = new Vector2(PanelWidth, PanelHeight);
            UIHitBox = DrawPosition.GetRectangle((int)PanelWidth, (int)PanelHeight);
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (slideProgress < 0.01f) {
                return;
            }

            float alpha = Math.Min(slideProgress * 2f, 1f);
            DrawPanel(spriteBatch, alpha);
            DrawContent(spriteBatch, alpha);
        }

        private void DrawPanel(SpriteBatch spriteBatch, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //阴影
            Rectangle shadowRect = UIHitBox;
            shadowRect.Offset(4, 4);
            spriteBatch.Draw(pixel, shadowRect, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.5f));

            //背景渐变 (硫磺火风格)
            int segments = 20;
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                float t2 = (i + 1) / (float)segments;
                int y1 = (int)(DrawPosition.Y + t * PanelHeight);
                int y2 = (int)(DrawPosition.Y + t2 * PanelHeight);
                Rectangle r = new Rectangle((int)DrawPosition.X, y1, (int)PanelWidth, Math.Max(1, y2 - y1));

                Color deep = new Color(30, 15, 15);
                Color mid = new Color(70, 30, 25);
                Color hot = new Color(120, 50, 35);

                float wave = (float)Math.Sin(pulseTimer * 1.2f + t * 2f) * 0.5f + 0.5f;
                Color c = Color.Lerp(Color.Lerp(deep, mid, wave), hot, t * 0.5f);
                c *= alpha;

                spriteBatch.Draw(pixel, r, new Rectangle(0, 0, 1, 1), c);
            }

            //火焰脉冲效果
            float pulse = (float)Math.Sin(pulseTimer * 2f) * 0.5f + 0.5f;
            Color pulseColor = new Color(140, 40, 25) * (alpha * 0.15f * pulse);
            spriteBatch.Draw(pixel, UIHitBox, new Rectangle(0, 0, 1, 1), pulseColor);

            //边框
            DrawBrimstoneFrame(spriteBatch, UIHitBox, alpha, borderGlow);
        }

        private void DrawContent(SpriteBatch spriteBatch, float alpha) {
            var font = FontAssets.MouseText.Value;

            //标题
            Vector2 titlePos = DrawPosition + new Vector2(12, 10);
            Color titleColor = new Color(255, 220, 180) * alpha;
            Utils.DrawBorderString(spriteBatch, QuestTitle.Value, titlePos, titleColor, 0.85f);

            //分割线
            Vector2 dividerStart = titlePos + new Vector2(0, 28);
            Vector2 dividerEnd = dividerStart + new Vector2(PanelWidth - 24, 0);
            DrawGradientLine(spriteBatch, dividerStart, dividerEnd,
                Color.OrangeRed * alpha * 0.8f, Color.OrangeRed * alpha * 0.1f, 1.5f);

            //伤害贡献度文本
            Vector2 contributionTextPos = dividerStart + new Vector2(0, 12);
            string contributionText = $"{DamageContribution.Value}: ";
            Utils.DrawBorderString(spriteBatch, contributionText, contributionTextPos,
                Color.White * alpha, 0.75f);

            //百分比显示
            Vector2 percentPos = contributionTextPos + new Vector2(font.MeasureString(contributionText).X * 0.75f, 0);
            string percentText = $"{cachedContribution:P1}";

            //根据进度改变颜色
            Color percentColor;
            if (cachedContribution >= 0.3f) {
                percentColor = Color.Lerp(Color.Yellow, Color.LimeGreen, (cachedContribution - 0.3f) / 0.7f);
            }
            else {
                percentColor = Color.Lerp(Color.Red, Color.Orange, cachedContribution / 0.3f);
                //低于要求时闪烁警告
                percentColor = Color.Lerp(percentColor, Color.Red, warningPulse * 0.5f);
            }

            Utils.DrawBorderString(spriteBatch, percentText, percentPos, percentColor * alpha, 0.85f);

            //需求文本
            Vector2 requirementPos = contributionTextPos + new Vector2(0, 22);
            Utils.DrawBorderString(spriteBatch, RequiredContribution.Value, requirementPos,
                Color.Gray * alpha, 0.7f);

            //进度条
            DrawProgressBar(spriteBatch, requirementPos + new Vector2(0, 18), alpha);
        }

        private void DrawProgressBar(SpriteBatch spriteBatch, Vector2 position, float alpha) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            float barWidth = PanelWidth - 24;
            float barHeight = 8;

            //背景
            Rectangle barBg = new Rectangle((int)position.X, (int)position.Y, (int)barWidth, (int)barHeight);
            spriteBatch.Draw(pixel, barBg, new Rectangle(0, 0, 1, 1), Color.Black * (alpha * 0.6f));

            //填充进度
            float fillWidth = barWidth * Math.Min(cachedContribution, 1f);
            if (fillWidth > 0) {
                Rectangle barFill = new Rectangle((int)position.X, (int)position.Y, (int)fillWidth, (int)barHeight);

                Color fillColor;
                if (cachedContribution >= 0.3f) {
                    fillColor = Color.Lerp(new Color(255, 180, 80), new Color(80, 255, 120), (cachedContribution - 0.3f) / 0.7f);
                }
                else {
                    fillColor = Color.Lerp(new Color(180, 50, 50), new Color(255, 140, 60), cachedContribution / 0.3f);
                }

                spriteBatch.Draw(pixel, barFill, new Rectangle(0, 0, 1, 1), fillColor * alpha);
            }

            //需求标记线 (30%位置)
            float requirementX = position.X + barWidth * 0.3f;
            Rectangle requirementLine = new Rectangle((int)requirementX - 1, (int)position.Y, 2, (int)barHeight);
            spriteBatch.Draw(pixel, requirementLine, new Rectangle(0, 0, 1, 1), Color.White * (alpha * 0.8f));

            //边框
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, barBg.Width, 1),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Bottom - 1, barBg.Width, 1),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
            spriteBatch.Draw(pixel, new Rectangle(barBg.X, barBg.Y, 1, barBg.Height),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
            spriteBatch.Draw(pixel, new Rectangle(barBg.Right - 1, barBg.Y, 1, barBg.Height),
                new Rectangle(0, 0, 1, 1), Color.OrangeRed * (alpha * 0.6f));
        }

        private static void DrawBrimstoneFrame(SpriteBatch sb, Rectangle rect, float alpha, float pulse) {
            Texture2D pixel = VaultAsset.placeholder2.Value;

            //外框
            Color outerEdge = Color.Lerp(new Color(180, 60, 30), new Color(255, 140, 70), pulse) * (alpha * 0.85f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), new Rectangle(0, 0, 1, 1), outerEdge * 0.75f);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);
            sb.Draw(pixel, new Rectangle(rect.Right - 3, rect.Y, 3, rect.Height), new Rectangle(0, 0, 1, 1), outerEdge * 0.9f);

            //内框发光
            Rectangle inner = rect;
            inner.Inflate(-6, -6);
            Color innerGlow = new Color(220, 100, 50) * (alpha * 0.22f * pulse);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Bottom - 1, inner.Width, 1), new Rectangle(0, 0, 1, 1), innerGlow * 0.7f);
            sb.Draw(pixel, new Rectangle(inner.X, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
            sb.Draw(pixel, new Rectangle(inner.Right - 1, inner.Y, 1, inner.Height), new Rectangle(0, 0, 1, 1), innerGlow * 0.85f);
        }

        private static void DrawGradientLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color startColor, Color endColor, float thickness) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 edge = end - start;
            float length = edge.Length();
            if (length < 1f) {
                return;
            }
            edge.Normalize();
            float rotation = (float)Math.Atan2(edge.Y, edge.X);
            int segments = Math.Max(1, (int)(length / 10f));
            for (int i = 0; i < segments; i++) {
                float t = i / (float)segments;
                Vector2 segPos = start + edge * (length * t);
                float segLength = length / segments;
                Color color = Color.Lerp(startColor, endColor, t);
                spriteBatch.Draw(pixel, segPos, new Rectangle(0, 0, 1, 1), color, rotation, new Vector2(0, 0.5f), new Vector2(segLength, thickness), SpriteEffects.None, 0);
            }
        }
    }
}
