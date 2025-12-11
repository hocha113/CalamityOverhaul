using CalamityOverhaul.Content.QuestLogs.Core;
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
            Position = new Vector2(-300, 0);
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

    internal class ExploreSnow : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "极寒之地");
            Description = this.GetLocalization(nameof(Description), () => "探索雪原生物群落");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.SnowBlock;
            Position = new Vector2(-150, -100);
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

    internal class ExploreOcean : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "世界尽头");
            Description = this.GetLocalization(nameof(Description), () => "到达海洋生物群落");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Seashell;
            Position = new Vector2(-150, 100);
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

    internal class ExploreDungeon : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "阴森地牢");
            Description = this.GetLocalization(nameof(Description), () => "发现地牢入口");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.GreenBrick;
            Position = new Vector2(-150, 0);
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

    internal class ExploreHell : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "地狱深渊");
            Description = this.GetLocalization(nameof(Description), () => "深入地狱");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Hellstone;
            Position = new Vector2(0, 150);
            AddParent<AdventurerQuests>();

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

            Position = new Vector2(-150, 0);
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
            if (IconItemType > 0 && Main.LocalPlayer.HasItem(IconItemType)) {
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

            Position = new Vector2(0, 150);
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
            if (IconItemType > 0 && Main.LocalPlayer.HasItem(IconItemType)) {
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

            Position = new Vector2(-150, 0);
            AddParent<ExploreOcean>();

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
            if (IconItemType > 0 && Main.LocalPlayer.HasItem(IconItemType)) {
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
            Position = new Vector2(-150, 200);
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

            Position = new Vector2(-150, 0);
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
            if (IconItemType > 0 && Main.LocalPlayer.HasItem(IconItemType)) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }

    internal class ExploreSpace : QuestNode
    {
        public override void SetStaticDefaults() {
            DisplayName = this.GetLocalization(nameof(DisplayName), () => "星辰大海");
            Description = this.GetLocalization(nameof(Description), () => "飞向太空");

            IconType = QuestIconType.Item;
            IconItemType = ItemID.Star;
            Position = new Vector2(0, -150);
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

            Position = new Vector2(0, -150);
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
            if (IconItemType > 0 && Main.LocalPlayer.HasItem(IconItemType)) {
                Objectives[0].CurrentProgress = 1;
            }
            if (Objectives[0].IsCompleted && !IsCompleted) IsCompleted = true;
        }
    }
}
