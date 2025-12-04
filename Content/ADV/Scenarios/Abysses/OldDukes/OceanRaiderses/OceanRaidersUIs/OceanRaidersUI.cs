using CalamityOverhaul.Common;
using InnoVault.UIHandles;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Items.OceanRaiderses.OceanRaidersUIs
{
    /// <summary>
    /// 海洋吞噬者专属箱子UI
    /// </summary>
    internal class OceanRaidersUI : UIHandle, ILocalizedModType
    {
        public static OceanRaidersUI Instance => UIHandleLoader.GetUIHandleOfType<OceanRaidersUI>();

        //UI状态
        private bool _active;
        public override bool Active {
            get => _active || animation.UIAlpha > 0f;
            set => _active = value;
        }

        public string LocalizationCategory => "UI";
        public static LocalizedText TitleText;
        public static LocalizedText StorageText;

        //UI尺寸
        private const int PanelWidth = 760;
        private const int PanelHeight = 700;
        private const int HeaderHeight = 80;
        private const int StorageStartX = 20;
        private const int StorageStartY = HeaderHeight + 10;

        //当前绑定的机器
        private OceanRaidersTP currentMachine;

        //组件
        private readonly OceanRaidersAnimation animation = new();
        private readonly OceanRaidersEffects effects = new();
        private OceanRaidersInteraction interaction;
        private OceanRaidersRenderer renderer;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "海洋吞噬者存储");
            StorageText = this.GetLocalization(nameof(StorageText), () => "存储空间");
        }

        /// <summary>
        /// 打开UI并绑定机器
        /// </summary>
        public void Interactive(OceanRaidersTP machine) {
            if (machine == null) return;

            if (currentMachine != machine) {
                currentMachine = machine;
                _active = true;

                //初始化组件
                if (interaction == null || renderer == null) {
                    interaction = new OceanRaidersInteraction(player, machine);
                    renderer = new OceanRaidersRenderer(player, machine, animation, interaction);
                }
                else {
                    interaction.UpdateMachine(machine);
                    renderer.UpdateMachine(machine);
                }
            }
            else {
                _active = !_active;
            }

            SoundEngine.PlaySound(CWRSound.ButtonZero with { Pitch = -0.2f });
        }

        public override void Update() {
            //更新动画进度
            animation.UpdateUIAnimation(_active);

            if (animation.UIAlpha <= 0f) {
                CleanupEffects();
                return;
            }

            //检查机器是否仍然有效
            if (currentMachine == null || !currentMachine.Active || currentMachine.CenterInWorld.To(player.Center).Length() > 220) {
                _active = false;
                return;
            }

            //更新硫磺海动画
            animation.UpdateSulfseaEffects();

            //计算面板位置
            Vector2 panelPosition = renderer.CalculatePanelPosition();

            //更新粒子和特效
            effects.UpdateParticles(_active, panelPosition, PanelWidth, PanelHeight);

            //更新UI交互
            if (_active && animation.PanelSlideProgress > 0.9f) {
                UpdateInteraction(panelPosition);
            }

            //更新槽位悬停动画
            animation.UpdateSlotHoverAnimations(interaction.HoveredSlot);
        }

        private void UpdateInteraction(Vector2 panelPosition) {
            UIHitBox = new Rectangle(
                (int)panelPosition.X,
                (int)panelPosition.Y,
                PanelWidth,
                PanelHeight
            );

            hoverInMainPage = UIHitBox.Intersects(MouseHitBox);

            if (hoverInMainPage) {
                player.mouseInterface = true;
                player.CWR().DontSwitchWeaponTime = 2;

                //优先检查关闭按钮
                if (interaction.UpdateCloseButton(MousePosition.ToPoint(), panelPosition, keyLeftPressState == KeyPressState.Pressed)) {
                    _active = false;
                    SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f });
                    return;
                }

                //更新槽位交互
                Vector2 storageStartPos = panelPosition + new Vector2(StorageStartX, StorageStartY);
                interaction.UpdateSlotInteraction(
                    MousePosition.ToPoint(),
                    storageStartPos,
                    keyLeftPressState == KeyPressState.Pressed,
                    Main.mouseLeft,
                    keyRightPressState == KeyPressState.Pressed,
                    Main.mouseRight
                );
            }
            else if (keyLeftPressState == KeyPressState.Pressed && animation.UIAlpha >= 1f && !player.mouseInterface) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f });
            }

            //ESC关闭
            if (Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) {
                _active = false;
                SoundEngine.PlaySound(SoundID.MenuClose with { Pitch = -0.3f });
            }
        }

        private void CleanupEffects() {
            effects.Clear();
            interaction?.Reset();
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (animation.UIAlpha <= 0f || renderer == null) return;

            Vector2 panelPosition = renderer.CalculatePanelPosition();
            renderer.Draw(spriteBatch, panelPosition, effects);
        }
    }
}
