using CalamityOverhaul.Content.ADV;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.OtherMods.NoxusBoss
{
    internal class GiveBlazingBud : ModPlayer, ILocalizedModType
    {
        public string LocalizationCategory => "ADV";
        public static LocalizedText MessageText;
        public static int RandTimer;
        public override void SetStaticDefaults() {
            MessageText = this.GetLocalization(nameof(MessageText), () => "焚烧的余烬随风低语，一朵奇异的花飘落到你手中……");
        }
        public override void OnEnterWorld() {
            RandTimer = Main.rand.Next(60 * 6, 60 * 10);
        }
        public override void PostUpdate() {
            if (VaultUtils.isServer) {
                return;
            }
            if (CWRMod.Instance.noxusBoss == null) {
                return;
            }
            if (!Player.TryGetADVSave(out var save)) {
                return;
            }
            if (!save.EternalBlazingNow) {
                return;
            }
            if (save.GiveBlazingBud) {
                return;
            }
            if (--RandTimer > 0) {
                return;
            }
            if (!CWRMod.Instance.noxusBoss.TryFind("BlazingBud", out ModItem blazingBudItem)) {
                return;
            }

            ADVRewardPopup.ShowReward(blazingBudItem.Type, 1, "", appearDuration: 24, holdDuration: -1, giveDuration: 16, requireClick: true,
                    anchorProvider: () => {
                        var rect = DialogueUIRegistry.Current?.GetPanelRect() ?? Rectangle.Empty;
                        if (rect == Rectangle.Empty) {
                            return new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.45f);
                        }
                        return new Vector2(rect.Center.X, rect.Y - 70f);
                    }, offset: Vector2.Zero
                    , styleProvider: () => ADVRewardPopup.RewardStyle.Brimstone);

            save.GiveBlazingBud = true;

            VaultUtils.Text(MessageText.Value, Color.Orange);
        }
    }
}
