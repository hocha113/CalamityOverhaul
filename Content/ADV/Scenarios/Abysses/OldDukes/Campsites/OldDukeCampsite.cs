using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.FindCampsites;
using CalamityOverhaul.Content.ADV.Scenarios.Abysses.OldDukes.Quest.Findfragments;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
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
    internal class OldDukeCampsite : ModSystem, ILocalizedModType, IWorldInfo
    {
        //头像矩形区域
        public readonly static Rectangle PortraitRec = new(128, 26, 78, 94);
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
        /// 是否已经尝试生成营地
        /// </summary>
        private bool hasTriedGenerate;

        /// <summary>
        /// 玩家进入营地事件
        /// </summary>
        public static event Action<Vector2> OnEnterCampsite;

        /*
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
        */

        public override void NetSend(BinaryWriter writer) {
            writer.Write(IsGenerated);
            if (IsGenerated) {
                writer.WriteVector2(CampsitePosition);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            IsGenerated = reader.ReadBoolean();
            if (IsGenerated) {
                CampsitePosition = reader.ReadVector2();
            }
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "老公爵营地");
        }

        public override void OnModUnload() {
            OnEnterCampsite = null;
        }

        public override void OnWorldLoad() {
            IsGenerated = false;
            CampsitePosition = Vector2.Zero;
        }

        public override void OnWorldUnload() {
            hasTriedGenerate = false;
            ClearCampsite();
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

        public override void PostUpdatePlayers() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            //检查是否应该生成营地
            if (!hasTriedGenerate && ShouldGenerateCampsite(player)) {
                hasTriedGenerate = true;
                RequestCampsiteGeneration();
            }
        }

        /// <summary>
        /// 检查是否应该生成营地
        /// </summary>
        private static bool ShouldGenerateCampsite(Player player) {
            if (!player.TryGetADVSave(out var save)) {
                return false;
            }

            //检查玩家是否已经同意合作
            if (!save.OldDukeCooperationAccepted) {
                return false;
            }

            //检查营地是否已经生成
            if (IsGenerated) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 请求生成营地（客户端发送给服务器或单人直接生成）
        /// </summary>
        private static void RequestCampsiteGeneration() {
            if (VaultUtils.isSinglePlayer) {
                TryGenerateCampsite();
            }
            else if (VaultUtils.isClient) {
                SendGenerationRequest();
            }
        }

        /// <summary>
        /// 客户端发送生成请求给服务器
        /// </summary>
        private static void SendGenerationRequest() {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OldDukeCampsiteGenerationRequest);
            packet.Send();
        }

        /// <summary>
        /// 尝试生成营地（服务器或单人执行）
        /// </summary>
        public static void TryGenerateCampsite() {
            if (VaultUtils.isClient) {
                return;//客户端不要自己生成营地中心
            }

            //使用位置查找器寻找最佳位置
            Vector2? position = CampsiteLocationFinder.FindBestLocation();

            if (position.HasValue) {
                GenerateCampsite(position.Value);
            }
            else {
                //如果找不到合适位置，则在世界右上角生成
                GenerateCampsite(new Vector2((Main.maxTilesX - 400) * 16, (Main.maxTilesY / 8) * 16));
            }

            if (VaultUtils.isServer) {
                SyncCampsiteToClients();
            }
        }

        /// <summary>
        /// 服务器同步营地数据给所有客户端
        /// </summary>
        private static void SyncCampsiteToClients() {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OldDukeCampsiteSync);
            packet.Write(IsGenerated);
            if (IsGenerated) {
                packet.WriteVector2(CampsitePosition);
            }
            packet.Send();
        }

        /// <summary>
        /// 客户端接收营地同步数据
        /// </summary>
        internal static void ReceiveCampsiteSync(BinaryReader reader) {
            bool wasGenerated = IsGenerated;
            IsGenerated = reader.ReadBoolean();
            
            if (IsGenerated) {
                CampsitePosition = reader.ReadVector2();
                
                if (!wasGenerated) {
                    OnCampsiteGenerated();
                }
            }
        }

        /// <summary>
        /// 营地生成后的初始化操作
        /// </summary>
        private static void OnCampsiteGenerated() {
            FindCampsiteUI.Instance.SetDefScreenYValue();
            ModContent.GetInstance<OldDukeCampsiteRenderer>().SetEntityInitialized(false);
            //播放生成音效
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.2f }, CampsitePosition);
        }

        private static void CheckWannaToFight() {
            if (WannaToFight) {//检测是否在和老公爵切磋
                if (!NPC.AnyNPCs(CWRID.NPC_OldDuke)) {
                    WannaToFight = false;//老公爵已被击败，重置切磋状态
                    OldDukeEffect.IsActive = false;//停止硫磺海效果
                    OldDukeEffect.Send();
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

            if (OldDukeEffect.IsActive) {
                return;//如果硫磺海效果已经启用，就不要进行交互
            }

            if (Main.LocalPlayer.mouseInterface) {
                return;//鼠标正在交互状态下，就不要进行交互
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

            //设置装饰物品位置
            OldDukeCampsiteDecoration.SetupPotPosition(position);

            //调整营地位置的Y值，使其更合理
            var list = OldDukeCampsiteDecoration.GetPotPositions();
            if (list.Count > 0) {
                float y = 0;
                foreach (var value in list) {
                    y += value.Y;
                }
                y /= list.Count;
                //将营地Y位置限制在锅位置的上下120像素范围内
                CampsitePosition = new Vector2(CampsitePosition.X, MathHelper.Clamp(CampsitePosition.Y, y - 120, y + 120));
            }

            if (VaultUtils.isClient) {
                OnCampsiteGenerated();
            }
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
