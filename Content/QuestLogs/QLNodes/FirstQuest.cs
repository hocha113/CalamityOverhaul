using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.QuestLogs.QLNodes
{
    public class FirstQuest : QuestNode
    {
        public override void SetStaticDefaults() {
            IconTexturePath = "CalamityOverhaul/icon_small";
            Position = new Vector2(0, 0);
            QuestType = QuestType.Main;
            Difficulty = QuestDifficulty.Easy;

            Objectives.Add(new QuestObjective {
                Description = this.GetLocalization("QuestObjective.Description", () => "点击领取"),
                RequiredProgress = 1
            });

            Rewards.Add(new QuestReward { ItemType = ItemID.CopperBroadsword, Amount = 1 });
            Rewards.Add(new QuestReward { ItemType = ItemID.CopperBow, Amount = 1 });
            Rewards.Add(new QuestReward { ItemType = ItemID.AmethystStaff, Amount = 1 });
            Rewards.Add(new QuestReward { ItemType = ItemID.CopperHammer, Amount = 1 });
            Rewards.Add(new QuestReward { ItemType = ItemID.WoodenArrow, Amount = 100 });
            Rewards.Add(new QuestReward { ItemType = CWRID.Item_SquirrelSquireStaff, Amount = 1 });
            Rewards.Add(new QuestReward { ItemType = CWRID.Item_ThrowingBrick, Amount = 150 });

            Rewards.Add(new QuestReward { ItemType = ItemID.ManaCrystal, Amount = 1 });

            Rewards.Add(new QuestReward { ItemType = ItemID.Bomb, Amount = 10 });
            Rewards.Add(new QuestReward { ItemType = ItemID.Rope, Amount = 50 });

            Rewards.Add(new QuestReward { ItemType = ItemID.MiningPotion, Amount = 1 });
            Rewards.Add(new QuestReward { ItemType = ItemID.SpelunkerPotion, Amount = 2 });
            Rewards.Add(new QuestReward { ItemType = ItemID.SwiftnessPotion, Amount = 3 });
            Rewards.Add(new QuestReward { ItemType = ItemID.GillsPotion, Amount = 2 });
            Rewards.Add(new QuestReward { ItemType = ItemID.ShinePotion, Amount = 1 });
            Rewards.Add(new QuestReward { ItemType = ItemID.RecallPotion, Amount = 3 });

            Rewards.Add(new QuestReward { ItemType = ItemID.Torch, Amount = 25 });
            Rewards.Add(new QuestReward { ItemType = ItemID.Chest, Amount = 3 });

            Rewards.Add(new QuestReward { ItemType = CWRID.Item_LoreAwakening, Amount = 1 });

            if (ModLoader.TryGetMod("MagicStorage", out Mod magicStorage)) {
                Rewards.Add(new QuestReward { ItemType = magicStorage.Find<ModItem>("StorageHeart").Type, Amount = 1 });
                Rewards.Add(new QuestReward { ItemType = magicStorage.Find<ModItem>("StorageUnit").Type, Amount = 4 });
                Rewards.Add(new QuestReward { ItemType = magicStorage.Find<ModItem>("CraftingAccess").Type, Amount = 1 });
            }

            AddChild<MiningQuest>();
        }

        public override void UpdateByPlayer() {
            //更新进度
            Objectives[0].CurrentProgress = 1;

            //检查完成
            if (Objectives[0].IsCompleted && !IsCompleted) {
                IsCompleted = true;
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 drawPos, float scale, bool isHovered, float alpha) {
            Texture2D value = CWRAsset.SoftGlow.Value;
            for (int i = 0; i < 6; i++) {
                spriteBatch.Draw(value, drawPos, null, Color.Gold with { A = 0 } * alpha, 0, value.Size() / 2, scale * (2 + i * 0.2f), SpriteEffects.None, 0);
            }
            return true;
        }
    }
}
