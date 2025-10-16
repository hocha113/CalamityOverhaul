using CalamityMod.Items.Materials;
using CalamityMod.Items.Potions.Alcohol;
using CalamityMod.NPCs.CalClone;
using CalamityMod.Projectiles.Boss;
using CalamityMod.Tiles.Furniture.CraftingStations;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.SkyEffects;
using CalamityOverhaul.Content.TileModify;
using InnoVault.PRT;
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
using Terraria.Graphics.Shaders;

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
            Line7 = this.GetLocalization(nameof(Line7), () => "呵呵呵，连这事都有听说过吗?和你这条有趣的鱼解释一下也无妨，" +
            "我的意识早已经熔铸进硫磺火中，这具躯体只不过是被火焰操纵的尸体");
            Line8 = this.GetLocalization(nameof(Line8), () => "......活人的意识，非人的躯体，依靠媒介行走世间，你成为了异类?!");
            Line9 = this.GetLocalization(nameof(Line9), () => "你的层次太低，理解不了我现在的状态");
            Line10 = this.GetLocalization(nameof(Line10), () => "况且我来这里也不是为了这事儿的......");
            QuestionLine = this.GetLocalization(nameof(QuestionLine), () => "那么，你的选择是？");
            Choice1Text = this.GetLocalization(nameof(Choice1Text), () => "(拔出武器)");
            Choice2Text = this.GetLocalization(nameof(Choice2Text), () => "(保持沉默)");
            Choice1Response = this.GetLocalization(nameof(Choice1Response), () => "哈，那么便让我来称量称量你吧");
            Choice2Response = this.GetLocalization(nameof(Choice2Response), () => "......真是杂鱼呢，那么给你一个见面礼，我们下次见");
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

    internal class SupCalSkySceneEffect : ModSceneEffect
    {
        public override int Music => MusicLoader.GetMusicSlot("CalamityOverhaul/Assets/Sounds/Music/Crisis");
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;
        public override bool IsSceneEffectActive(Player player) => SupCalSkyEffect.IsActive;
        public override void SpecialVisuals(Player player, bool isActive) => player.ManageSpecialBiomeVisuals(SupCalSky.Name, isActive);
    }

    ///<summary>
    ///至尊灾厄天空效果
    ///</summary>
    internal class SupCalSky : CustomSky, ICWRLoader
    {
        internal static string Name => "CWRMod:SupCalSky";
        private bool active;
        private float intensity;

        void ICWRLoader.LoadData() {
            if (VaultUtils.isServer) {
                return;
            }
            SkyManager.Instance[Name] = this;
            //创建暗黑滤镜效果
            Filters.Scene[Name] = new Filter(new ScreenShaderData("FilterMiniTower")
                .UseColor(0.1f, 0.05f, 0.08f)  //深红暗色调
                .UseOpacity(0.6f), EffectPriority.High);
        }

        public override void Activate(Vector2 position, params object[] args) {
            active = true;
            intensity = 0f;
        }

        public override void Deactivate(params object[] args) {
            active = false;
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth) {
            if (intensity <= 0.01f) return;

            Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);
            
            //绘制深红暗黑背景
            spriteBatch.Draw(
                CWRUtils.GetT2DValue(CWRConstant.Placeholder2),
                screenRect,
                new Color(10, 5, 8) * intensity * 0.9f
            );
        }

        public override bool IsActive() {
            return active || intensity > 0;
        }

        public override void Reset() {
            active = false;
            intensity = 0f;
        }

        public override void Update(GameTime gameTime) {
            //根据对话场景状态调整强度
            if (SupCalSkyEffect.IsActive) {
                if (intensity < 1f) {
                    intensity += 0.015f;
                }
            }
            else {
                intensity -= 0.01f;
                if (intensity <= 0) {
                    Deactivate();
                }
            }
        }

        public override Color OnTileColor(Color inColor) {
            //应用暗红色调
            if (intensity > 0.1f) {
                float darkR = 0.8f;
                float darkG = 0.4f;
                float darkB = 0.5f;
                
                Color tintedColor = new Color(
                    (int)(inColor.R * darkR),
                    (int)(inColor.G * darkG),
                    (int)(inColor.B * darkB),
                    inColor.A
                );
                
                return Color.Lerp(inColor, tintedColor, intensity * 0.5f);
            }
            return inColor;
        }
    }

    ///<summary>
    ///至尊灾厄场景效果管理器（负责粒子生成）
    ///</summary>
    internal class SupCalSkyEffect : ModSystem
    {
        public static bool IsActive = false;
        private int particleTimer = 0;
        
        public override void PostUpdateEverything() {
            if (!IsActive || Main.gameMenu) {
                return;
            }

            particleTimer++;
            
            //更频繁地生成火焰粒子
            if (particleTimer % 1 == 0) {
                SpawnBrimstoneFlameParticles();
            }
            
            //生成灰烬粒子
            if (particleTimer % 2 == 0) {
                SpawnBrimstoneAshParticles();
            }
            
            //偶尔生成大型火焰团
            if (particleTimer % 30 == 0) {
                SpawnLargeFlameBurst();
            }
        }

        private static void SpawnBrimstoneFlameParticles() {
            //在屏幕下半部分随机位置生成多个火焰粒子
            for (int i = 0; i < 2; i++) {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-100, Main.screenWidth + 100),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-50, 30)
                );

                //创建硫磺火粒子
                PRT_LavaFire flamePRT = new PRT_LavaFire {
                    Position = spawnPos,
                    Velocity = new Vector2(
                        Main.rand.NextFloat(-1.5f, 1.5f),
                        Main.rand.NextFloat(-3.5f, -1.5f)  //更强的上升力
                    ),
                    Scale = Main.rand.NextFloat(0.8f, 1.4f),
                    ai = new float[] { 0, 0 },  //ai[1] = 0 表示使用标准漂浮模式
                    colors = new Color[] {
                        new Color(255, 140, 70),   //亮橙色
                        new Color(200, 80, 40),    //暗橙红
                        new Color(140, 40, 30)     //深红
                    },
                    minLifeTime = 120,
                    maxLifeTime = 200
                };
                
                PRTLoader.AddParticle(flamePRT);
            }
        }

        private static void SpawnBrimstoneAshParticles() {
            //生成灰烬粒子，覆盖更大范围
            for (int i = 0; i < 3; i++) {
                Vector2 spawnPos = new Vector2(
                    Main.screenPosition.X + Main.rand.Next(-100, Main.screenWidth + 100),
                    Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-30, 20)
                );

                //使用 LavaFire 的变体作为灰烬
                PRT_LavaFire ashPRT = new PRT_LavaFire {
                    Position = spawnPos,
                    Velocity = new Vector2(
                        Main.rand.NextFloat(-2f, 2f),
                        Main.rand.NextFloat(-2.5f, -0.8f)
                    ),
                    Scale = Main.rand.NextFloat(0.5f, 1f),
                    ai = new float[] { 0, 0 },
                    colors = new Color[] {
                        new Color(80, 70, 65),     //灰褐色
                        new Color(50, 45, 40),     //深灰
                        new Color(30, 25, 20)      //暗灰黑
                    },
                    minLifeTime = 140,
                    maxLifeTime = 220
                };
                
                PRTLoader.AddParticle(ashPRT);
            }
        }

        private static void SpawnLargeFlameBurst() {
            //在屏幕底部中间区域生成大型火焰爆发
            Vector2 burstCenter = new Vector2(
                Main.screenPosition.X + Main.screenWidth * Main.rand.NextFloat(0.3f, 0.7f),
                Main.screenPosition.Y + Main.screenHeight + Main.rand.Next(-20, 10)
            );

            //生成一组环绕的火焰粒子
            int flameCount = 8;
            for (int i = 0; i < flameCount; i++) {
                float angle = MathHelper.TwoPi * i / flameCount + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 offset = angle.ToRotationVector2() * Main.rand.NextFloat(20f, 40f);
                
                PRT_LavaFire burstFlame = new PRT_LavaFire {
                    Position = burstCenter + offset,
                    Velocity = new Vector2(
                        offset.X * 0.05f,
                        Main.rand.NextFloat(-4f, -2f)
                    ),
                    Scale = Main.rand.NextFloat(1.2f, 1.8f),
                    ai = new float[] { 0, 0 },
                    colors = new Color[] {
                        new Color(255, 180, 90),   //非常亮的橙黄
                        new Color(255, 120, 60),   //亮橙
                        new Color(180, 60, 40)     //深橙红
                    },
                    minLifeTime = 100,
                    maxLifeTime = 160
                };
                
                PRTLoader.AddParticle(burstFlame);
            }

            //额外生成一些快速上升的火星
            for (int i = 0; i < 12; i++) {
                Vector2 sparkVelocity = new Vector2(
                    Main.rand.NextFloat(-2f, 2f),
                    Main.rand.NextFloat(-5f, -3f)
                );

                PRT_Spark spark = new PRT_Spark(
                    burstCenter + Main.rand.NextVector2Circular(1130f, 130f),
                    sparkVelocity,
                    false,
                    Main.rand.Next(40, 80),
                    Main.rand.NextFloat(1f, 1.8f),
                    Color.Lerp(
                        new Color(255, 200, 100),
                        new Color(255, 140, 70),
                        Main.rand.NextFloat()
                    )
                );
                PRTLoader.AddParticle(spark);
            }
        }

        public override void Unload() {
            IsActive = false;
        }
    }
}
