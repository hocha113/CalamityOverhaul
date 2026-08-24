using CalamityOverhaul.Content.QuestLogs.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    //异闻篇：装了灾厄后出现的西南扇区，枢纽挂在冒险家群落南侧
    //生物群落见闻走 CWRRef 的 CalamityPlayer 区域反射，全部只在灾厄在场时加载

    internal class CalamityLoreHub : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRRef.Has;

        public override bool IsChapterHub => true;

        public override int ChapterOrder => 40;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "异变调查");
            Description = this.GetLocalization(nameof(Description), () => "这个世界不太对劲");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "海是酸的，崖在烧，星星砸出的坑里长着不认识的东西。把见闻记下来，这个世界的暗面比想象中大得多。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_LoreCynosure;
            Position = new Vector2(0, 450);
            AddParent<AdventurerQuests>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "点击领取"),
                RequiredProgress = 1
            });

            AddReward(ItemID.GoldCoin, 2);
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = 1;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreSulphurSea : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRRef.Has;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "硫磺之海");
            Description = this.GetLocalization(nameof(Description), () => "探索硫磺海");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "世界的一侧没有蓝色的海，只有泛黄的酸水。雨落进去会冒泡，鱼游上来带着毒。捂住口鼻，别喝水。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_SulphurousSand;
            Position = new Vector2(-150, 100);
            AddParent<CalamityLoreHub>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达硫磺海"),
                RequiredProgress = 1
            });

            AddReward(ItemID.GillsPotion, 5);
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.GetPlayerZoneSulphur()) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreAbyss : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRRef.Has;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "深渊入口");
            Description = this.GetLocalization(nameof(Description), () => "潜入硫磺海下的深渊");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "硫磺海底裂着一道往下的口子。光下不去，呼吸也下不去，越深的地方住着越古老的东西。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_Voidstone;
            Position = new Vector2(0, 150);
            AddParent<ExploreSulphurSea>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "进入深渊"),
                RequiredProgress = 1
            });

            AddReward(ItemID.ShinePotion, 5);
            AddReward(ItemID.GillsPotion, 5);
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.GetPlayerZoneAbyss()) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class AbyssDeepQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod)
            && CWRID.Item_Lumenyl > 0 && CWRID.Item_Voidstone > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "深渊之底");
            Description = this.GetLocalization(nameof(Description), () => "从深渊带回流明晶与虚空石");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "浅层的矿灯照不到的地方，岩壁上长着发蓝光的结晶。挖到它们再活着浮上来，才算真正下过深渊。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_Lumenyl;
            Position = new Vector2(0, 150);
            AddParent<ExploreAbyss>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Expert;

            AddCollectObjective(10, CWRID.Item_Lumenyl);
            AddCollectObjective(30, CWRID.Item_Voidstone);

            AddReward(ItemID.ObsidianSkinPotion, 5);
            AddReward(ItemID.LifeforcePotion, 3);
        }

        public override void UpdateByPlayer() {
            Player player = Main.LocalPlayer;
            Objectives[0].CurrentProgress = player.InquireItem(CWRID.Item_Lumenyl);
            Objectives[1].CurrentProgress = player.InquireItem(CWRID.Item_Voidstone);
            if (Objectives[0].IsCompleted && Objectives[1].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }
    }

    internal class ExploreSunkenSea : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRRef.Has;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "沉沦之海");
            Description = this.GetLocalization(nameof(Description), () => "找到地下荒漠深处的沉沦之海");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "荒漠地底藏着一片安静的蓝。棱晶在水里发光，蛤蜊比你的房子还大。这里大概是这个世界最温柔的角落。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_Navystone;
            Position = new Vector2(150, 100);
            AddParent<CalamityLoreHub>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达沉沦之海"),
                RequiredProgress = 1
            });

            AddReward(ItemID.WaterWalkingPotion, 3);
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.GetPlayerZoneSunkenSea()) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreBrimstoneCrag : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRRef.Has;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "硫磺火崖");
            Description = this.GetLocalization(nameof(Description), () => "踏入地狱边缘的硫磺火崖");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "地狱的一侧立着烧了不知多少年的崖壁。砖石的缝隙里渗出暗红的火，废墟的深处睡着一位元素之灵。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_BrimstoneSlag;
            Position = new Vector2(0, 150);
            AddParent<CalamityLoreHub>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达硫磺火崖"),
                RequiredProgress = 1
            });

            AddReward(ItemID.ObsidianSkinPotion, 5);
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.GetPlayerZoneCalamity()) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreAstral : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRRef.Has;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "星染之地");
            Description = this.GetLocalization(nameof(Description), () => "探索被星辉污染的土地");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "一颗星星砸进世界，坑里长出的东西不属于任何图鉴。土地在变色，生物在变形，而这只是个开始。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_AstralOre;
            Position = new Vector2(0, 300);
            AddParent<CalamityLoreHub>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达星染之地"),
                RequiredProgress = 1
            });

            AddReward(ItemID.RegenerationPotion, 5);
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.GetPlayerZoneAstral()) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class AcidRainQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRRef.Has;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "酸雨将至");
            Description = this.GetLocalization(nameof(Description), () => "经历一场酸雨");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "硫磺海上空的云是黄的。下雨的时候整片海都在沸腾，水里的东西会顺着雨爬上岸。带把伞，认真的。");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Umbrella;
            Position = new Vector2(-150, 0);
            AddParent<ExploreSulphurSea>();

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "经历酸雨事件"),
                RequiredProgress = 1
            });

            AddReward(ItemID.Umbrella, 1);
            AddReward(ItemID.GillsPotion, 3);
        }

        public override void UpdateByPlayer() {
            if (CWRRef.GetAcidRainEventIsOngoing()) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class DeliciousMeatQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_DeliciousMeat > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "美味肉排");
            Description = this.GetLocalization(nameof(Description), () => "获得一块美味肉排");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "没人知道它是什么动物身上的肉，也没人在乎。好吃，管饱，还能当建材。多囤点。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_DeliciousMeat;
            Position = new Vector2(300, 250);
            AddParent<CalamityLoreHub>();
            HiddenUntilUnlocked = true;

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Easy;

            AddObtainObjective();

            AddReward(CWRID.Item_DeliciousMeat, 5);
        }

        protected override bool HiddenTriggerMet() => Main.LocalPlayer.InquireItem(CWRID.Item_DeliciousMeat) > 0;

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_DeliciousMeat);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class BrimlishQuest : QuestNode
    {
        public override bool IsLoadingEnabled(Mod mod) => base.IsLoadingEnabled(mod) && CWRID.Item_Brimlish > 0;

        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "硫磺鱼");
            Description = this.GetLocalization(nameof(Description), () => "钓起一条硫磺鱼");
            DetailedDescription = this.GetLocalization(nameof(DetailedDescription), () => "连酸水里都有鱼活着，而且长得意外地精神。把鱼钩抛进硫磺海，看看会咬上来什么。");

            IconType = QuestIconType.Item;
            IconItemType = CWRID.Item_Brimlish;
            Position = new Vector2(0, -150);
            AddParent<ExploreSulphurSea>();
            HiddenUntilUnlocked = true;

            QuestType = QuestType.Side;
            Difficulty = QuestDifficulty.Normal;

            AddObtainObjective();

            AddReward(ItemID.FishingPotion, 5);
            AddReward(ItemID.SonarPotion, 5);
        }

        protected override bool HiddenTriggerMet() => Main.LocalPlayer.InquireItem(CWRID.Item_Brimlish) > 0;

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = Main.LocalPlayer.InquireItem(CWRID.Item_Brimlish);
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
