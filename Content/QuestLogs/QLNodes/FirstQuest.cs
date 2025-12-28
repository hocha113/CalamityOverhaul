using CalamityOverhaul.Content.QuestLogs.Core;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using static InnoVault.VaultUtils;

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

            AddChild<MiningQuest>();
        }

        public override void OnWorldEnter() {
            Rewards.Clear();
            //自动扫描 StarterBag 的内容物作为奖励
            if (CWRID.Item_StarterBag > 0) {
                var dropInfos = ItemDropScanner.GetItemDropsForPlayer(CWRID.Item_StarterBag, Main.LocalPlayer);
                foreach (var dropInfo in dropInfos) {
                    if (dropInfo.ItemType > ItemID.None) {
                        AddReward(dropInfo.ItemType, dropInfo.MaxStack > 1 ? dropInfo.MaxStack : 1);
                    }
                }
            }

            if (ModLoader.TryGetMod("CalamityModMusic", out var musicMod) && musicMod.TryFind<ModItem>("CalamityMusicbox", out var musicbox)) {
                AddReward(musicbox.Type, 1);
            }

            if (ModLoader.TryGetMod("MagicStorage", out Mod magicStorage)) {
                if (magicStorage.TryFind<ModItem>("StorageHeart", out var heart)) {
                    AddReward(heart.Type, 1);
                }
                if (magicStorage.TryFind<ModItem>("StorageUnit", out var unit)) {
                    AddReward(unit.Type, 4);
                }
                if (magicStorage.TryFind<ModItem>("CraftingAccess", out var crafting)) {
                    AddReward(crafting.Type, 1);
                }
            }
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
            bool isNig = QuestLog.Instance.NightMode;
            Texture2D value = CWRAsset.SoftGlow.Value;
            Color color = Color.Gold with { A = 0 } * alpha;
            if (isNig) {
                color *= 0.3f;
            }
            for (int i = 0; i < 6; i++) {
                spriteBatch.Draw(value, drawPos, null, color, 0, value.Size() / 2, scale * (2 + i * 0.2f), SpriteEffects.None, 0);
            }
            return true;
        }
    }
}
