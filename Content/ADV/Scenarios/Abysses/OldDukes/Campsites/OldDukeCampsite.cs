using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 老公爵营地
    /// </summary>
    [VaultLoaden("@CalamityMod/NPCs/OldDuke/")]
    internal class OldDukeCampsite : ModSystem, ILocalizedModType
    {
        //反射加载老公爵贴图，以便在ADV场景中使用，总共七帧，一般只使用前六帧，因为第七帧是张嘴动画
        public static Texture2D OldDuke;
        //老公爵的头像图标
        public static Texture2D OldDuke_Head_Boss;

        //营地数据
        public static bool IsGenerated { get; private set; }
        public static Vector2 CampsitePosition { get; private set; }

        public string LocalizationCategory => "ADV.OldDukeCampsite";

        //动画状态
        private static int animationFrame;
        private static int animationTimer;
        private const int FrameDuration = 8;//每帧持续时间
        private const int TotalFrames = 6;//使用前6帧
        
        //交互状态
        private static bool isPlayerNearby;
        private static float interactPromptAlpha;
        private const float InteractDistance = 220f;//交互距离（像素）

        public static LocalizedText TitleText;

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "老公爵营地");
        }

        public override void PostUpdateEverything() {
            if (!IsGenerated) {
                return;
            }

            UpdateAnimation();
            CheckPlayerProximity();
        }

        /// <summary>
        /// 更新动画
        /// </summary>
        private static void UpdateAnimation() {
            animationTimer++;
            if (animationTimer >= FrameDuration) {
                animationTimer = 0;
                animationFrame++;
                if (animationFrame >= TotalFrames) {
                    animationFrame = 0;
                }
            }
        }

        /// <summary>
        /// 检查玩家是否靠近营地
        /// </summary>
        private static void CheckPlayerProximity() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            float distance = Vector2.Distance(player.Center, CampsitePosition);
            bool wasNearby = isPlayerNearby;
            isPlayerNearby = distance < InteractDistance;

            //交互提示淡入淡出
            if (isPlayerNearby) {
                if (interactPromptAlpha < 1f) {
                    interactPromptAlpha += 0.05f;
                }
            }
            else {
                if (interactPromptAlpha > 0f) {
                    interactPromptAlpha -= 0.05f;
                }
            }

            interactPromptAlpha = MathHelper.Clamp(interactPromptAlpha, 0f, 1f);

            //播放进入营地音效
            if (isPlayerNearby && !wasNearby) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.3f, Pitch = -0.5f }, CampsitePosition);
            }
        }

        /// <summary>
        /// 生成营地
        /// </summary>
        public static void GenerateCampsite(Vector2 position) {
            if (IsGenerated) {
                return;
            }

            CampsitePosition = position;
            IsGenerated = true;

            //播放生成音效
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.2f }, position);
        }

        /// <summary>
        /// 清除营地
        /// </summary>
        public static void ClearCampsite() {
            IsGenerated = false;
            animationFrame = 0;
            animationTimer = 0;
            isPlayerNearby = false;
            interactPromptAlpha = 0f;
        }

        /// <summary>
        /// 获取当前帧的贴图区域
        /// </summary>
        public static Rectangle GetCurrentFrame() {
            if (OldDuke == null) {
                return Rectangle.Empty;
            }

            int frameHeight = OldDuke.Height / 7;//总共7帧
            return new Rectangle(0, frameHeight * animationFrame, OldDuke.Width, frameHeight);
        }

        /// <summary>
        /// 获取交互提示透明度
        /// </summary>
        public static float GetInteractPromptAlpha() => interactPromptAlpha;

        /// <summary>
        /// 检查是否可以交互
        /// </summary>
        public static bool CanInteract() => isPlayerNearby && interactPromptAlpha > 0.5f;

        public override void Unload() {
            ClearCampsite();
        }
    }
}
