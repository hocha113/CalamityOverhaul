using CalamityOverhaul.Common;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RAMSystems
{
    internal abstract class BaseRamUpgradeChip : ModItem
    {
        protected abstract RamUpgradeKind UpgradeKind { get; }

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.maxStack = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 30;
            Item.useAnimation = 30;
            Item.consumable = true;
            Item.rare = ItemRarityID.Cyan;
            Item.value = Item.sellPrice(gold: 1);
            Item.UseSound = CWRSound.ChipSet;
            Item.autoReuse = true;
        }

        public override bool CanUseItem(Player player)
            => RamSystem.CanUseUpgrade(player, UpgradeKind);

        public override bool? UseItem(Player player) {
            if (player.whoAmI != Main.myPlayer || Main.netMode == NetmodeID.Server) {
                return false;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return RamNet.SendUpgradeRequest(player, UpgradeKind);
            }
            if (!RamSystem.TryUseUpgrade(player, UpgradeKind)) {
                return false;
            }
            SoundEngine.PlaySound(SoundID.ResearchComplete, player.Center);
            return true;
        }

        public override bool ConsumeItem(Player player)
            => Main.netMode == NetmodeID.SinglePlayer;

        internal static void HandleRequestResult(Player player,
            in RamRequestResult result) {
            if (player == null || player.whoAmI != Main.myPlayer
                || result.OperationId is not (RamNet.CapacityUpgradeOperation
                    or RamNet.RecoveryUpgradeOperation)) {
                return;
            }
            if (result.ResultCode == (byte)RamUpgradeResultCode.Success) {
                SoundEngine.PlaySound(SoundID.ResearchComplete, player.Center);
                return;
            }
            RamSystem.NotifyInsufficient();
            SoundEngine.PlaySound(SoundID.MenuTick with {
                Pitch = -0.5f,
                Volume = 0.5f,
            }, player.Center);
        }
    }
}
