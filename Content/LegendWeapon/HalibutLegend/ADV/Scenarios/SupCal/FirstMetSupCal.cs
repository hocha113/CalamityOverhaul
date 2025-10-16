using CalamityMod.Items.Materials;
using CalamityMod.Items.Potions.Alcohol;
using CalamityMod.NPCs.CalClone;
using CalamityMod.Projectiles.Boss;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Content.SkyEffects;
using CalamityOverhaul.Content.TileModify;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.ADV.Scenarios.SupCal
{
    internal class FirstMetSupCal : ADVScenarioBase, ILocalizedModType
    {
        public override string Key => nameof(FirstMetSupCal);
        public string LocalizationCategory => "Legend.HalibutText.ADV";
        
        //角色名称本地化
        public static LocalizedText Rolename1 { get; private set; }
        public static LocalizedText Rolename2 { get; private set; }
        public static LocalizedText Rolename3 { get; private set; }

        //对话文本本地化
        public static LocalizedText Line1 { get; private set; }
        public static LocalizedText Line2 { get; private set; }
        public static LocalizedText Line3 { get; private set; }
        public static LocalizedText Line4 { get; private set; }
        public static LocalizedText Line5 { get; private set; }
        public static LocalizedText Line6 { get; private set; }
        public static LocalizedText Line7 { get; private set; }
        public static LocalizedText Line8 { get; private set; }
        public static LocalizedText Line9 { get; private set; }
        public static LocalizedText Line10 { get; private set; }
        public static LocalizedText QuestionLine { get; private set; }
        public static LocalizedText Choice1Text { get; private set; }
        public static LocalizedText Choice2Text { get; private set; }
        public static LocalizedText Choice1Response { get; private set; }
        public static LocalizedText Choice2Response { get; private set; }

        //设置场景默认使用硫磺火风格
        protected override Func<DialogueBoxBase> DefaultDialogueStyle => () => BrimstoneDialogueBox.Instance;
        
        public override void SetStaticDefaults() {
            Rolename1 = this.GetLocalization(nameof(Rolename1), () => "???");
            Rolename2 = this.GetLocalization(nameof(Rolename2), () => "至尊灾厄");
            Rolename3 = this.GetLocalization(nameof(Rolename3), () => "比目鱼");
            
            Line1 = this.GetLocalization(nameof(Line1), () => "没想到你这么快就杀掉了我的'妹妹'");
            Line2 = this.GetLocalization(nameof(Line2), () => "你的成长速度确实有些快了");
            Line3 = this.GetLocalization(nameof(Line3), () => "你是......我对你有印象");
            Line4 = this.GetLocalization(nameof(Line4), () => "你是那个焚烧掉了一半海域的女巫");
            Line5 = this.GetLocalization(nameof(Line5), () => "哈?!呵呵，竟然有人...或者鱼认得我，你们倒也算有趣");
            Line6 = this.GetLocalization(nameof(Line6), () => "......你，为什么还活着?我记得你已经在上世纪就已经死了");
            Line7 = this.GetLocalization(nameof(Line7), () => "呵呵呵，连这事都有听说过吗?和你这条有趣的鱼解释一下也无妨，我的意识早已经熔铸进硫磺火中，这具躯体只不过是被火焰操纵的尸体");
            Line8 = this.GetLocalization(nameof(Line8), () => "......活人的意识，非人的躯体，依靠媒介行走世间，你成为了异类?!");
            Line9 = this.GetLocalization(nameof(Line9), () => "你的层次太低，理解不了我现在的状态");
            Line10 = this.GetLocalization(nameof(Line10), () => "况且我来这里也不是为了这事儿的......");
            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "那么，你的选择是？");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "(拔出武器)");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "(保持沉默)");
            Choice1Response = this.GetLocalization(nameof(Choice1Response), () => "哈，那么便让我来称量称量你吧");
            Choice2Response = this.GetLocalization(nameof(Choice2Response), () => "......真是杂鱼呢，那么给你一个见面礼，我们下次见");
        }
        
        protected override void Build() {
            //注册立绘
            DialogueBoxBase.RegisterPortrait(Rolename1.Value, ADVAsset.SupCalADV);
            DialogueBoxBase.SetPortraitStyle(Rolename1.Value, silhouette: true);
            DialogueBoxBase.RegisterPortrait(Rolename2.Value, ADVAsset.SupCalADV);
            DialogueBoxBase.SetPortraitStyle(Rolename2.Value, silhouette: false);
            DialogueBoxBase.RegisterPortrait(Rolename3.Value, ADVAsset.HeadADV);
            DialogueBoxBase.SetPortraitStyle(Rolename3.Value, silhouette: false);

            //添加对话（使用本地化文本）
            Add(Rolename1.Value, Line1.Value);
            Add(Rolename1.Value, Line2.Value);
            Add(Rolename3.Value, Line3.Value);
            Add(Rolename3.Value, Line4.Value);
            Add(Rolename2.Value, Line5.Value);
            Add(Rolename3.Value, Line6.Value);
            Add(Rolename2.Value, Line7.Value);
            Add(Rolename3.Value, Line8.Value);
            Add(Rolename2.Value, Line9.Value);
            Add(Rolename2.Value, Line10.Value);

            AddWithChoices(Rolename2.Value, QuestionLine.Value, new List<Choice> {
                new Choice(Choice1Text.Value, () => {
                    //选择后继续对话
                    Add(Rolename2.Value, Choice1Response.Value);
                    //继续推进场景
                    DialogueUIRegistry.Current?.EnqueueDialogue(
                        Rolename2.Value,
                        Choice1Response.Value,
                        onFinish: () => Choice1()
                    );
                }),
                new Choice(Choice2Text.Value, () => {
                    Add(Rolename2.Value, Choice2Response.Value);
                    DialogueUIRegistry.Current?.EnqueueDialogue(
                        Rolename2.Value,
                        Choice2Response.Value,
                        onFinish: () => Choice2()
                    );
                }),
            });
        }

        public void Choice1() {
            Vector2 spawnPos = Main.LocalPlayer.Center;
            SoundEngine.PlaySound(SCalAltar.SummonSound, spawnPos);
            Projectile.NewProjectile(new EntitySource_WorldEvent(), spawnPos, Vector2.Zero
                , ModContent.ProjectileType<SCalRitualDrama>(), 0, 0f, Main.myPlayer, 0, 0);
            Complete();
        }

        public void Choice2() {
            ADVRewardPopup.ShowReward(ModContent.ItemType<AshesofCalamity>(), 999, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero);
            Complete();
        }

        public override void Update(ADVSave save, HalibutPlayer halibutPlayer) {
            if (save.FirstMetSupCal) {
                return;
            }
            if (!FirstMetSupCalNPC.Spawned) {
                return;
            }

            if (ScenarioManager.Start<FirstMetSupCal>()) {
                save.FirstMetSupCal = true;
                FirstMetSupCalNPC.Spawned = false;
            }
        }
    }

    internal class FirstMetSupCalNPC : GlobalNPC
    {
        public static bool Spawned = false;
        public override bool SpecialOnKill(NPC npc) {
            if (npc.type == ModContent.NPCType<CalamitasClone>()) {
                Spawned = true;
            }
            return false;
        }
    }

    internal class SupCalSky : CustomSky
    {
        public static string Name;

        public override void Activate(Vector2 position, params object[] args) {
            throw new NotImplementedException();
        }

        public override void Deactivate(params object[] args) {
            throw new NotImplementedException();
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            throw new NotImplementedException();
        }

        public override bool IsActive() {
            throw new NotImplementedException();
        }

        public override void Reset() {
            throw new NotImplementedException();
        }

        public override void Update(GameTime gameTime) {
            throw new NotImplementedException();
        }
    }

    internal class SupCalSceneEffect : ModSceneEffect
    {
        public override int Music => -1;
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(NPCID.SkeletronPrime);
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(SupCalSky.Name, isActive);
    }
}
