using CalamityMod.NPCs.SupremeCalamitas;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.SupCal
{
    /// <summary>
    /// 选择迎战但被至尊灾厄击败后的场景
    /// </summary>
    internal class SupCalPlayerDefeat : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(SupCalPlayerDefeat);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
        
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }
        
        private const string expressionDespise = " ";
        private const string expressionCloseEye = " " + " ";
        
        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "至尊灾厄");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "比目鱼");
            
            Line1 = this.GetLocalization(nameof(Line1), () => "哈?!就这?");
            Line2 = this.GetLocalization(nameof(Line2), () => "呵，我还以为你有多强呢");
            Line3 = this.GetLocalization(nameof(Line3), () => "现在的你还太弱了，连让我认真的资格都没有");
            Line4 = this.GetLocalization(nameof(Line4), () => "等你真正强大起来再来找我吧");
            Line5 = this.GetLocalization(nameof(Line5), () => "不过......我倒是有些好奇");
            Line6 = this.GetLocalization(nameof(Line6), () => "你究竟能成长到什么地步呢?");
            Line7 = this.GetLocalization(nameof(Line7), () => "那么，就让我拭目以待吧");
            Line8 = this.GetLocalization(nameof(Line8), () => "下次一定要变得更强!");
        }
        
        protected override void Build() {
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV[0]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: false);
            
            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionDespise, ADVAsset.SupCalADV[5]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionDespise, silhouette: false);
            
            DialogueBoxBase.RegisterPortrait(Rolename1.Value + expressionCloseEye, ADVAsset.SupCalADV[4]);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value + expressionCloseEye, silhouette: false);
            
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.HelenADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);
            
            Add(Rolename1.Value + expressionDespise, Line1.Value);
            Add(Rolename1.Value + expressionDespise, Line2.Value);
            Add(Rolename1.Value, Line3.Value);
            Add(Rolename1.Value, Line4.Value);
            Add(Rolename1.Value + expressionCloseEye, Line5.Value);
            Add(Rolename1.Value + expressionCloseEye, Line6.Value);
            Add(Rolename1.Value, Line7.Value);
            Add(Rolename2.Value, Line8.Value);
        }
        
        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            //这个场景可以重复触发，因为玩家可能会多次尝试
            if (!save.SupCalChoseToFight) {
                return;//玩家没有选择战斗
            }
            
            if (save.SupCalDefeat) {
                return;//如果已经击败过至尊灾厄，就不再触发此场景
            }
            
            if (!SupCalPlayerDefeatTracker.Spawned) {
                return;
            }
            
            if (--SupCalPlayerDefeatTracker.RandomTimer > 0) {
                return;
            }
            
            if (ScenarioManager.Start<SupCalPlayerDefeat>()) {
                SupCalPlayerDefeatTracker.Spawned = false;
            }
        }
    }
    
    internal class SupCalPlayerDefeatTracker : GlobalNPC
    {
        public static bool Spawned = false;
        public static int RandomTimer;
        
        public override bool InstancePerEntity => true;
        
        private bool hasRecordedDeath = false;
        
        public override void AI(NPC npc) {
            if (npc.type != ModContent.NPCType<SupremeCalamitas>()) {
                return;
            }
            
            if (!npc.active || hasRecordedDeath) {
                return;
            }
            
            //检查是否有玩家死亡
            foreach (Player player in Main.player) {
                if (!player.active || !player.dead) {
                    continue;
                }
                
                if (!player.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                    continue;
                }
                
                if (!halibutPlayer.ADCSave.SupCalChoseToFight) {
                    continue;
                }
                
                if (halibutPlayer.ADCSave.SupCalDefeat) {
                    continue;//已经击败过，不再触发
                }
                
                //记录玩家死亡
                Spawned = true;
                RandomTimer = 60 * Main.rand.Next(15, 25);//15-25秒后触发，给玩家时间复活
                hasRecordedDeath = true;
                break;
            }
        }
        
        public override void OnKill(NPC npc) {
            if (npc.type == ModContent.NPCType<SupremeCalamitas>()) {
                //Boss被击杀时重置状态
                hasRecordedDeath = false;
                Spawned = false;
            }
        }
    }
}
