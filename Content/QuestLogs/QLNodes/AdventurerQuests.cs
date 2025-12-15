using CalamityOverhaul.Content.QuestLogs.Core;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    internal class AdventurerQuests : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "冒险家");
            Description = this.GetLocalization(nameof(Description), () => "世界任我走！");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.HermesBoots;
            Position = new Vector2(-800, 0);
            AddParent<FirstQuest>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Normal;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "点击领取"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.HermesBoots,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description", () => "跑靴")
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.LuckyHorseshoe,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description", () => "马蹄铁")
            });
        }

        public override void UpdateByPlayer() {
            bool first = false;
            var rewards = GetQuest<FirstQuest>().Rewards;
            foreach (var reward in rewards) {
                if (reward.Claimed) {
                    first = true;
                    break;
                }
            }
            Objectives[0].CurrentProgress = first ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    //地表探索分支
    internal class ExploreSnow : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "极寒之地");
            Description = this.GetLocalization(nameof(Description), () => "探索雪原生物群落");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.SnowBlock;
            Position = new Vector2(-150, -350);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达雪原"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.WarmthPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "暖身药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneSnow) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class IceLabSchematic : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "极地密码");
            Description = this.GetLocalization(nameof(Description), () => "在雪原嘉登实验室寻找加密图纸");

            IconType = QuestIconType.Item;
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity)) {
                if (calamity.TryFind("EncryptedSchematicIce", out ModItem item)) {
                    IconItemType = item.Type;
                }
            }

            Position = new Vector2(0, -150);
            AddParent<ExploreSnow>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获取冰冻图纸"),
                RequiredProgress = 1,
                TargetItemID = IconItemType
            });

            if (ModLoader.TryGetMod("CalamityMod", out Mod calamityMod)) {
                if (calamityMod.TryFind("DubiousPlating", out ModItem plating) && calamityMod.TryFind("MysteriousCircuitry", out ModItem circuit)) {
                    Rewards.Add(new QuestReward {
                        ItemType = plating.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Plating", () => "可疑镀层")
                    });
                    Rewards.Add(new QuestReward {
                        ItemType = circuit.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Circuitry", () => "神秘电路")
                    });
                }
            }

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Shiverthorn,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description.Shiverthorn", () => "寒颤棘")
            });
        }

        public override void UpdateByPlayer() {
            if (IconItemType > 0 && Main.LocalPlayer.InquireItem(IconItemType) > 0) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreDesert : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "炎热荒漠");
            Description = this.GetLocalization(nameof(Description), () => "踏入沙漠生物群落");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.SandBlock;
            Position = new Vector2(0, -200);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达沙漠"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.WaterWalkingPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "水上行走药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneDesert) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreOcean : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "世界尽头");
            Description = this.GetLocalization(nameof(Description), () => "到达海洋生物群落");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Seashell;
            Position = new Vector2(150, -350);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达海洋"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.GillsPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "鱼鳃药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneBeach) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class SunkenSeaLabSchematic : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "沉沦之海");
            Description = this.GetLocalization(nameof(Description), () => "在沉沦之海嘉登实验室寻找加密图纸");

            IconType = QuestIconType.Item;
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity)) {
                if (calamity.TryFind("EncryptedSchematicSunkenSea", out ModItem item)) {
                    IconItemType = item.Type;
                }
            }

            Position = new Vector2(0, -150);
            AddParent<ExploreDesert>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获取沉沦之海图纸"),
                RequiredProgress = 1,
                TargetItemID = IconItemType
            });

            if (ModLoader.TryGetMod("CalamityMod", out Mod calamityMod)) {
                if (calamityMod.TryFind("DubiousPlating", out ModItem plating) && calamityMod.TryFind("MysteriousCircuitry", out ModItem circuit)) {
                    Rewards.Add(new QuestReward {
                        ItemType = plating.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Plating", () => "可疑镀层")
                    });
                    Rewards.Add(new QuestReward {
                        ItemType = circuit.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Circuitry", () => "神秘电路")
                    });
                }
            }

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Waterleaf,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description.Waterleaf", () => "波浪叶")
            });
        }

        public override void UpdateByPlayer() {
            if (IconItemType > 0 && Main.LocalPlayer.InquireItem(IconItemType) > 0) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreJungle : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "丛林探险");
            Description = this.GetLocalization(nameof(Description), () => "深入丛林生物群落");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.JungleSpores;
            Position = new Vector2(150, -200);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达丛林"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.SummoningPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "召唤药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneJungle) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class JungleLabSchematic : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "丛林原案");
            Description = this.GetLocalization(nameof(Description), () => "在丛林嘉登实验室寻找加密图纸");

            IconType = QuestIconType.Item;
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity)) {
                if (calamity.TryFind("EncryptedSchematicJungle", out ModItem item)) {
                    IconItemType = item.Type;
                }
            }

            Position = new Vector2(150, 0);
            AddParent<ExploreJungle>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获取丛林图纸"),
                RequiredProgress = 1,
                TargetItemID = IconItemType
            });

            if (ModLoader.TryGetMod("CalamityMod", out Mod calamityMod)) {
                if (calamityMod.TryFind("DubiousPlating", out ModItem plating) && calamityMod.TryFind("MysteriousCircuitry", out ModItem circuit)) {
                    Rewards.Add(new QuestReward {
                        ItemType = plating.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Plating", () => "可疑镀层")
                    });
                    Rewards.Add(new QuestReward {
                        ItemType = circuit.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Circuitry", () => "神秘电路")
                    });
                }
            }

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Moonglow,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description.Moonglow", () => "月光草")
            });
        }

        public override void UpdateByPlayer() {
            if (IconItemType > 0 && Main.LocalPlayer.InquireItem(IconItemType) > 0) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreCorruption : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "腐化之地");
            Description = this.GetLocalization(nameof(Description), () => "探索腐化或猩红之地");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.DemoniteOre;
            Position = new Vector2(0, 150);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "进入腐化或猩红"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.BattlePotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "战斗药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneCorrupt || Main.LocalPlayer.ZoneCrimson) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class CollectEvilOres : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "邪恶矿藏");
            Description = this.GetLocalization(nameof(Description), () => "收集邪恶矿石");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.DemoniteOre;
            Position = new Vector2(0, 150);
            AddParent<ExploreCorruption>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集魔矿或猩红矿"),
                RequiredProgress = 30,
                TargetItemID = ItemID.DemoniteOre
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Torch,
                Amount = 99,
                Description = this.GetLocalization("QuestReward.Description", () => "火把")
            });
        }

        public override void UpdateByPlayer() {
            int demoniteCount = Main.LocalPlayer.InquireItem(ItemID.DemoniteOre);
            int crimtaneCount = Main.LocalPlayer.InquireItem(ItemID.CrimtaneOre);
            Objectives[0].CurrentProgress = demoniteCount + crimtaneCount;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class FindShadowOrb : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "邪恶核心");
            Description = this.GetLocalization(nameof(Description), () => "在邪恶之地深处寻找暗影珠或猩红之心");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.ShadowOrb;
            Position = new Vector2(0, 150);
            AddParent<CollectEvilOres>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "摧毁暗影珠或猩红之心"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Dynamite,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "炸药")
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Bomb,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description.Bomb", () => "炸弹")
            });
        }

        public override void UpdateByPlayer() {
            if (WorldGen.shadowOrbCount > 0) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreDungeon : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "阴森地牢");
            Description = this.GetLocalization(nameof(Description), () => "发现地牢入口");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.GreenBrick;
            Position = new Vector2(-150, 250);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达地牢"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.TrapsightPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "危险感应药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneDungeon) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    //地下探索分支
    internal class ExploreUnderground : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "地下探险");
            Description = this.GetLocalization(nameof(Description), () => "深入地下洞穴");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.StoneBlock;
            Position = new Vector2(150, 250);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达地下洞穴"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.MiningPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "挖矿药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneDirtLayerHeight || Main.LocalPlayer.ZoneRockLayerHeight) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class CollectOres : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "矿石采集");
            Description = this.GetLocalization(nameof(Description), () => "收集基础矿石");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.IronOre;
            Position = new Vector2(150, -100);
            AddParent<ExploreUnderground>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description.Iron", () => "收集铁矿或铅矿"),
                RequiredProgress = 20,
                TargetItemID = ItemID.IronOre
            });

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description.Copper", () => "收集铜矿或锡矿"),
                RequiredProgress = 20,
                TargetItemID = ItemID.CopperOre
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.RecallPotion,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "回忆药水")
            });
        }

        public override void UpdateByPlayer() {
            int ironCount = Main.LocalPlayer.InquireItem(ItemID.IronOre);
            int leadCount = Main.LocalPlayer.InquireItem(ItemID.LeadOre);
            Objectives[0].CurrentProgress = ironCount + leadCount;

            int copperCount = Main.LocalPlayer.InquireItem(ItemID.CopperOre);
            int tinCount = Main.LocalPlayer.InquireItem(ItemID.TinOre);
            Objectives[1].CurrentProgress = copperCount + tinCount;

            if (Objectives[0].IsCompleted && Objectives[1].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class CollectGems : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "宝石收藏家");
            Description = this.GetLocalization(nameof(Description), () => "收集各种宝石");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Ruby;
            Position = new Vector2(150, 0);
            AddParent<CollectOres>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集5种不同的宝石"),
                RequiredProgress = 5
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.ShinePotion,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description", () => "光芒药水")
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Diamond,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description.Diamond", () => "钻石")
            });
        }

        public override void UpdateByPlayer() {
            int gemTypes = 0;
            if (Main.LocalPlayer.InquireItem(ItemID.Amethyst) > 0) gemTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Topaz) > 0) gemTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Sapphire) > 0) gemTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Emerald) > 0) gemTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Ruby) > 0) gemTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Diamond) > 0) gemTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Amber) > 0) gemTypes++;

            Objectives[0].CurrentProgress = gemTypes;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreCaverns : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "洞穴深处");
            Description = this.GetLocalization(nameof(Description), () => "探索更深的洞穴层");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Granite;
            Position = new Vector2(150, 0);
            AddParent<ExploreUnderground>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达洞穴层"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.SpelunkerPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "洞穴探险药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneRockLayerHeight) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class FindLifeCrystal : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "生命之源");
            Description = this.GetLocalization(nameof(Description), () => "寻找生命水晶");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.LifeCrystal;
            Position = new Vector2(150, 100);
            AddParent<ExploreUnderground>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "找到生命水晶"),
                RequiredProgress = 3,
                TargetItemID = ItemID.LifeCrystal
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.LesserHealingPotion,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "次级治疗药水")
            });
        }

        public override void UpdateByPlayer() {
            int crystalCount = Main.LocalPlayer.InquireItem(ItemID.LifeCrystal);
            Objectives[0].CurrentProgress = crystalCount;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreGlowingMushroom : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "发光蘑菇");
            Description = this.GetLocalization(nameof(Description), () => "找到发光蘑菇生物群落");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.GlowingMushroom;
            Position = new Vector2(150, 200);
            AddParent<ExploreUnderground>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达蘑菇地"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.NightOwlPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "夜猫子药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneGlowshroom) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreHell : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "地狱深渊");
            Description = this.GetLocalization(nameof(Description), () => "深入地狱");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Hellstone;
            Position = new Vector2(150, 300);
            AddParent<ExploreUnderground>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达地狱"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.ObsidianSkinPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "黑曜石皮肤药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneUnderworldHeight) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class HellLabSchematic : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "地狱蓝图");
            Description = this.GetLocalization(nameof(Description), () => "在地狱嘉登实验室寻找加密图纸");

            IconType = QuestIconType.Item;
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity)) {
                if (calamity.TryFind("EncryptedSchematicHell", out ModItem item)) {
                    IconItemType = item.Type;
                }
            }

            Position = new Vector2(150, 0);
            AddParent<ExploreHell>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获取地狱图纸"),
                RequiredProgress = 1,
                TargetItemID = IconItemType
            });

            if (ModLoader.TryGetMod("CalamityMod", out Mod calamityMod)) {
                if (calamityMod.TryFind("DubiousPlating", out ModItem plating) && calamityMod.TryFind("MysteriousCircuitry", out ModItem circuit)) {
                    Rewards.Add(new QuestReward {
                        ItemType = plating.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Plating", () => "可疑镀层")
                    });
                    Rewards.Add(new QuestReward {
                        ItemType = circuit.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Circuitry", () => "神秘电路")
                    });
                }
            }

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Fireblossom,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description.Fireblossom", () => "火焰花")
            });
        }

        public override void UpdateByPlayer() {
            if (IconItemType > 0 && Main.LocalPlayer.InquireItem(IconItemType) > 0) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    //天空探索分支
    internal class ExploreSpace : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "星辰大海");
            Description = this.GetLocalization(nameof(Description), () => "飞向太空");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Star;
            Position = new Vector2(-150, -200);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达太空"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.FeatherfallPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "羽落药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.ZoneSkyHeight) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreFloatingIsland : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "浮空岛屿");
            Description = this.GetLocalization(nameof(Description), () => "找到天空中的浮空岛");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Cloud;
            Position = new Vector2(-150, -100);
            AddParent<ExploreSpace>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "到达浮空岛"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.GravitationPotion,
                Amount = 3,
                Description = this.GetLocalization("QuestReward.Description", () => "重力药水")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.position.Y < Main.worldSurface * 16 && Main.LocalPlayer.ZoneSkyHeight) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class CollectFallenStars : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "星辰收集者");
            Description = this.GetLocalization(nameof(Description), () => "收集坠落之星");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.FallenStar;
            Position = new Vector2(-150, 0);
            AddParent<ExploreSpace>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集坠落之星"),
                RequiredProgress = 10,
                TargetItemID = ItemID.FallenStar
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.LesserManaPotion,
                Amount = 15,
                Description = this.GetLocalization("QuestReward.Description", () => "次级魔力药水")
            });
        }

        public override void UpdateByPlayer() {
            int starCount = Main.LocalPlayer.InquireItem(ItemID.FallenStar);
            Objectives[0].CurrentProgress = starCount;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class PlanetoidLabSchematic : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "星流之谜");
            Description = this.GetLocalization(nameof(Description), () => "在小行星嘉登实验室寻找加密图纸");

            IconType = QuestIconType.Item;
            if (ModLoader.TryGetMod("CalamityMod", out Mod calamity)) {
                if (calamity.TryFind("EncryptedSchematicPlanetoid", out ModItem item)) {
                    IconItemType = item.Type;
                }
            }

            Position = new Vector2(-150, 100);
            AddParent<ExploreSpace>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "获取小行星图纸"),
                RequiredProgress = 1,
                TargetItemID = IconItemType
            });

            if (ModLoader.TryGetMod("CalamityMod", out Mod calamityMod)) {
                if (calamityMod.TryFind("DubiousPlating", out ModItem plating) && calamityMod.TryFind("MysteriousCircuitry", out ModItem circuit)) {
                    Rewards.Add(new QuestReward {
                        ItemType = plating.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Plating", () => "可疑镀层")
                    });
                    Rewards.Add(new QuestReward {
                        ItemType = circuit.Type,
                        Amount = 5,
                        Description = this.GetLocalization("QuestReward.Description.Circuitry", () => "神秘电路")
                    });
                }
            }

            Rewards.Add(new QuestReward {
                ItemType = ItemID.FallenStar,
                Amount = 15,
                Description = this.GetLocalization("QuestReward.Description.FallenStar", () => "坠落之星")
            });
        }

        public override void UpdateByPlayer() {
            if (IconItemType > 0 && Main.LocalPlayer.InquireItem(IconItemType) > 0) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    //建造与生活分支
    internal class BuildHouse : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "建筑大师");
            Description = this.GetLocalization(nameof(Description), () => "建造合适的房屋");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Wood;
            Position = new Vector2(-150, 0);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "建造一个有效房屋"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.Wood,
                Amount = 200,
                Description = this.GetLocalization("QuestReward.Description", () => "木材")
            });
        }

        public override void UpdateByPlayer() {
            if (Main.LocalPlayer.townNPCs > 0) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class NPCVillage : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "繁荣村落");
            Description = this.GetLocalization(nameof(Description), () => "吸引多位NPC入住");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Torch;
            Position = new Vector2(-150, 0);
            AddParent<BuildHouse>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "拥有5位NPC"),
                RequiredProgress = 5
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.GoldCoin,
                Amount = 2,
                Description = this.GetLocalization("QuestReward.Description", () => "金币")
            });
        }

        public override void UpdateByPlayer() {
            Objectives[0].CurrentProgress = (int)Main.LocalPlayer.townNPCs;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    //炼金分支
    internal class CollectHerbs : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "草药学徒");
            Description = this.GetLocalization(nameof(Description), () => "收集各种草药");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Daybloom;
            Position = new Vector2(-150, 100);
            AddParent<AdventurerQuests>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "收集3种不同的草药"),
                RequiredProgress = 3
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.BottledWater,
                Amount = 20,
                Description = this.GetLocalization("QuestReward.Description", () => "瓶装水")
            });
        }

        public override void UpdateByPlayer() {
            int herbTypes = 0;
            if (Main.LocalPlayer.InquireItem(ItemID.Daybloom) > 0) herbTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Blinkroot) > 0) herbTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Waterleaf) > 0) herbTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Moonglow) > 0) herbTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Deathweed) > 0) herbTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Shiverthorn) > 0) herbTypes++;
            if (Main.LocalPlayer.InquireItem(ItemID.Fireblossom) > 0) herbTypes++;

            Objectives[0].CurrentProgress = herbTypes;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class FirstPotion : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "炼金入门");
            Description = this.GetLocalization(nameof(Description), () => "制作你的第一瓶药水");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Bottle;
            Position = new Vector2(-150, 0);
            AddParent<CollectHerbs>();

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "制作任意药水"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.LesserManaPotion,
                Amount = 10,
                Description = this.GetLocalization("QuestReward.Description", () => "次级魔力药水")
            });
        }

        public override void CraftedItem(Recipe recipe, Item item, List<Item> consumedItems, Item destinationStack) {
            if (item.buffType > 0 && item.consumable) {
                Objectives[0].CurrentProgress = 1;
            }
        }

        public override void UpdateByPlayer() {
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    //综合成就任务
    internal class DeepExplorer : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "深渊探索者");
            Description = this.GetLocalization(nameof(Description), () => "探索所有主要生物群落");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.GPS;
            Position = new Vector2(0, -150);
            AddParent<ExploreOcean>();

            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Hard;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "完成所有探索任务"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.GPS,
                Amount = 1,
                Description = this.GetLocalization("QuestReward.Description", () => "全球定位系统")
            });

            Rewards.Add(new QuestReward {
                ItemType = ItemID.GoldCoin,
                Amount = 5,
                Description = this.GetLocalization("QuestReward.Description.Gold", () => "金币")
            });
        }

        public override void UpdateByPlayer() {
            bool allCompleted = GetQuest<ExploreSnow>().IsCompleted &&
                              GetQuest<ExploreOcean>().IsCompleted &&
                              GetQuest<ExploreJungle>().IsCompleted &&
                              GetQuest<ExploreDesert>().IsCompleted &&
                              GetQuest<ExploreHell>().IsCompleted;

            Objectives[0].CurrentProgress = allCompleted ? 1 : 0;
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
