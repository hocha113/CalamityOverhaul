using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.FindCampsites;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.Findfragments;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Campsites
{
    /// <summary>
    /// 老公爵营地
    /// </summary>
    [VaultLoaden("@CalamityMod/NPCs/OldDuke/")]
    internal class OldDukeCampsite : ModSystem, ILocalizedModType
    {
        //反射加载老公爵纹理，以便在ADV场景中使用，总共七帧，一般只使用前六帧，因为第七帧是张嘴动画
        public static Texture2D OldDuke;
        //老公爵的头像图标
        public static Texture2D OldDuke_Head_Boss;
        [VaultLoaden(CWRConstant.ADV + "Abysse/")]
        public static Texture2D OldPot;//反射加载老公爵营地的锅纹理，大小宽46像素高48像素，适合放地上用于丰富营地场景
        [VaultLoaden(CWRConstant.ADV + "Abysse/")]
        public static Texture2D Oldflagpole;//反射加载老公爵营地的旗帜纹理，大小宽60像素高160像素，适合放地上用于丰富营地场景

        /// <summary>
        /// 营地是否已生成
        /// </summary>
        public static bool IsGenerated { get; private set; }
        /// <summary>
        /// 是否在和老公爵切磋
        /// </summary>
        public static bool WannaToFight { get; set; }
        /// <summary>
        /// 营地位置
        /// </summary>
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

        /// <summary>
        /// 玩家进入营地事件
        /// </summary>
        public static event Action<Vector2> OnEnterCampsite;

        public override void SaveWorldData(TagCompound tag) {
            tag[nameof(IsGenerated)] = IsGenerated;
            tag[nameof(CampsitePosition)] = CampsitePosition;
        }

        public override void LoadWorldData(TagCompound tag) {
            IsGenerated = false;
            if (tag.TryGet(nameof(IsGenerated), out bool value)) {
                IsGenerated = value;
            }
            CampsitePosition = Vector2.Zero;
            if (tag.TryGet(nameof(CampsitePosition), out Vector2 pos)) {
                CampsitePosition = pos;
            }
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "老公爵营地");
        }

        public override void OnModUnload() {
            OnEnterCampsite = null;
        }

        public override void PostUpdateEverything() {
            if (!IsGenerated) {
                return;
            }

            UpdateAnimation();
            CheckPlayerProximity();
            CheckWannaToFight();

            //检测右键交互
            if (CanInteract() && Main.mouseRight && Main.mouseRightRelease) {
                TriggerInteraction();
            }
        }

        private static void CheckWannaToFight() {
            if (WannaToFight) {//检测是否在和老公爵切磋
                if (!NPC.AnyNPCs(CWRID.NPC_OldDuke)) {
                    WannaToFight = false;//老公爵已被击败，重置切磋状态
                    OldDukeEffect.IsActive = false;//停止硫磺海效果
                }
            }
        }

        /// <summary>
        /// 触发交互
        /// </summary>
        private static void TriggerInteraction() {
            if (Main.mapFullscreen) {
                return;//玩家如果展开了全屏地图，就不要进行交互
            }

            //播放交互音效
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.3f, Volume = 0.6f });

            if (!Main.LocalPlayer.TryGetADVSave(out var save)) {
                return;
            }

            //首次营地对话
            if (!save.OldDukeFirstCampsiteDialogueCompleted) {
                save.OldDukeFirstCampsiteDialogueCompleted = true;

                //触发首次对话场景
                ScenarioManager.Reset<Quest.FindFragments.FirstCampsiteDialogue>();
                ScenarioManager.Start<Quest.FindFragments.FirstCampsiteDialogue>();
                FindFragmentUI.Instance.SetDefScreenYValue();
                return;
            }

            //后续交互对话
            ScenarioManager.Reset<CampsiteInteractionDialogue>();
            ScenarioManager.Start<CampsiteInteractionDialogue>();
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

            //触发进入营地事件
            if (isPlayerNearby && !wasNearby) {
                OnEnterCampsite?.Invoke(CampsitePosition);
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
            FindCampsiteUI.Instance.SetDefScreenYValue();

            //设置装饰物品位置
            OldDukeCampsiteDecoration.SetupPotPosition(position);

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

            //重置装饰状态
            OldDukeCampsiteDecoration.ResetDecoration();
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
