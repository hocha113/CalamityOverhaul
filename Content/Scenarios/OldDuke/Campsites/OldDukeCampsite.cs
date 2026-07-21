using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.Actors;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Scenarios.OldDuke.Campsites
{
    /// <summary>老公爵营地</summary>
    [VaultLoaden("@CalamityMod/NPCs/OldDuke/")]
    internal class OldDukeCampsite : ModSystem, ILocalizedModType, IWorldInfo
    {
        public readonly static Rectangle PortraitRec = new(128, 26, 78, 94);
        //ADV用，7帧里只用前6(第7张嘴)
        public static Texture2D OldDuke = null!;
        public static Texture2D OldDuke_Head_Boss = null!;
        [VaultLoaden(CWRConstant.ADV + "Abysse/")]
        public static Texture2D OldPot = null!;//46x48
        [VaultLoaden(CWRConstant.ADV + "Abysse/")]
        public static Texture2D Oldflagpole = null!;//60x160
        /// <summary>人鱼钓收回中</summary>
        public static bool MermanRodMoveback { get; internal set; }
        /// <summary>营地已生成</summary>
        public static bool IsGenerated { get; internal set; }
        /// <summary>切磋中</summary>
        public static bool WannaToFight { get; set; }
        /// <summary>营地位置</summary>
        public static Vector2 CampsitePosition { get; private set; }

        public string LocalizationCategory => "ADV.OldDukeCampsite";

        private static int animationFrame;
        private static int animationTimer;
        private const int FrameDuration = 8;//帧时长
        private const int TotalFrames = 6;

        private static bool isPlayerNearby;
        private static float interactPromptAlpha;
        private const float InteractDistance = 220f;//px

        //联机生成请求去重，等回执前不重发
        private static bool pendingGenerationRequest;

        public static LocalizedText TitleText;

        /// <summary>进入营地</summary>
        public static event Action<Vector2> OnEnterCampsite;

        public override void SaveWorldData(TagCompound tag) {
            tag[nameof(IsGenerated)] = IsGenerated;
            tag[nameof(CampsitePosition)] = CampsitePosition;
        }

        public override void LoadWorldData(TagCompound tag) {
            IsGenerated = false;
            CampsitePosition = Vector2.Zero;
            try {
                if (tag != null && tag.TryGet(nameof(IsGenerated), out bool value)) {
                    IsGenerated = value;
                }
                if (tag != null && tag.TryGet(nameof(CampsitePosition), out Vector2 pos)) {
                    CampsitePosition = pos;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[OldDukeCampsite:LoadWorldData] Failed to load campsite data: {ex.Message}");
                IsGenerated = false;
                CampsitePosition = Vector2.Zero;
            }
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(IsGenerated);
            if (IsGenerated) {
                writer.WriteVector2(CampsitePosition);
            }
            writer.Write(WannaToFight);
        }

        public override void NetReceive(BinaryReader reader) {
            IsGenerated = reader.ReadBoolean();
            if (IsGenerated) {
                CampsitePosition = reader.ReadVector2();
            }
            WannaToFight = reader.ReadBoolean();
        }

        public override void SetStaticDefaults() {
            TitleText = this.GetLocalization(nameof(TitleText), () => "老公爵营地");
        }

        public override void OnModUnload() {
            OnEnterCampsite = null;
        }

        public override void OnWorldLoad() {
            MermanRodMoveback = false;
            animationFrame = 0;
            animationTimer = 0;
            isPlayerNearby = false;
            interactPromptAlpha = 0f;
            WannaToFight = false;
            pendingGenerationRequest = false;
        }

        public override void OnWorldUnload() {
            ClearCampsite();
        }

        public override void PostUpdateEverything() {
            if (!IsGenerated) {
                return;
            }

            //Actor缺失时补种
            OldDukeCampsiteGenerationService.EnsureCampsitePlaced();

            UpdateAnimation();
            CheckPlayerProximity();
            CheckWannaToFight();

            if (CanInteract() && CanTriggerInteraction() && Main.mouseRight && Main.mouseRightRelease) {
                TriggerInteraction();
            }
        }

        public override void PostUpdatePlayers() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            if (ShouldGenerateCampsite(player)) {
                RequestCampsiteGeneration();
            }
        }

        private static bool ShouldGenerateCampsite(Player player) {
            //搬家时等玩家远离再清旧营地
            if (MermanRodMoveback && player.DistanceSQ(CampsitePosition) > 1200 * 1200) {
                if (VaultUtils.isSinglePlayer) {
                    ClearCampsiteAndSync();
                }
                return true;
            }

            if (IsGenerated) {
                return false;
            }

            if (NPC.AnyNPCs(CWRID.NPC_OldDuke)) {
                return false;
            }

            if (SubWorldRef.AnyActiveSubWorld()) {
                return false;
            }

            if (OldDukeStorySync.Read(
                    d => d.OldDukeFindFragmentsQuestTriggered || d.OldDukeFindFragmentsQuestCompleted,
                    d => d.OldDukeFindFragmentsQuestTriggered || d.OldDukeFindFragmentsQuestCompleted)) {
                return true;
            }

            if (!OldDukeStorySync.Read(d => d.OldDukeCooperationAccepted, d => d.OldDukeCooperationAccepted)) {
                return false;
            }

            return true;
        }

        /// <summary>请求生成营地(客户端发包/单人直调)</summary>
        private static void RequestCampsiteGeneration() {
            if (VaultUtils.isSinglePlayer) {
                TryGenerateCampsite();
            }
            else if (VaultUtils.isClient) {
                SendGenerationRequest();
            }
        }

        /// <summary>客户端生成请求，pending去重</summary>
        private static void SendGenerationRequest() {
            if (VaultUtils.isSinglePlayer || pendingGenerationRequest) {
                return;
            }
            pendingGenerationRequest = true;
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OldDukeCampsiteGenerationRequest);
            packet.Send();
        }

        /// <summary>服务端/单人生成营地</summary>
        public static void TryGenerateCampsite() {
            if (VaultUtils.isClient) {
                return;//客户端不生成
            }

            Vector2? position = CampsiteLocationFinder.FindBestLocation();

            if (position.HasValue) {
                GenerateCampsite(position.Value);
            }
            else {
                //找不到就右上角兜底
                GenerateCampsite(new Vector2((Main.maxTilesX - 400) * 16, Main.maxTilesY / 8 * 16));
            }

            if (VaultUtils.isServer) {
                SyncCampsiteToClients();
            }
        }

        /// <summary>服务端同步营地</summary>
        private static void SyncCampsiteToClients() {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.OldDukeCampsiteSync);
            packet.Write(IsGenerated);
            if (IsGenerated) {
                packet.WriteVector2(CampsitePosition);
            }
            packet.Send();
        }

        /// <summary>客户端收营地同步</summary>
        internal static void ReceiveCampsiteSync(BinaryReader reader) {
            //回执到，清pending
            pendingGenerationRequest = false;

            bool wasGenerated = IsGenerated;
            IsGenerated = reader.ReadBoolean();

            if (IsGenerated) {
                CampsitePosition = reader.ReadVector2();

                if (!wasGenerated) {
                    OnCampsiteGenerated();
                }
            }
        }

        /// <summary>生成后客户端音效</summary>
        private static void OnCampsiteGenerated() {
            SoundEngine.PlaySound(SoundID.Splash with { Volume = 0.5f, Pitch = -0.2f }, CampsitePosition);
        }

        public static void TeleportToCampsite(Player player) {
            if (!IsGenerated) {
                return;
            }
            if (!player.Alives()) {
                return;
            }
            var playePos = CampsitePosition + new Vector2(0, -50);
            List<CampsitePotActor> pots = ActorLoader.GetActiveActors<CampsitePotActor>();
            if (pots.Count > 0) {
                playePos = pots[Main.rand.Next(pots.Count)].Position + new Vector2(0, -16);
            }
            player.Teleport(playePos, 999);
            CampsiteInteractionDialogue.GiveTeaOnStart = true;
        }

        private static void CheckWannaToFight() {
            if (WannaToFight) {
                if (!NPC.AnyNPCs(CWRID.NPC_OldDuke)) {
                    WannaToFight = false;
                    if (VaultUtils.isServer) {
                        NetMessage.SendData(MessageID.WorldData);
                    }
                }
            }
        }

        private static bool CanTriggerInteraction() {
            if (Main.mapFullscreen) {
                return false;//全屏地图
            }

            if (OldDukeEffect.IsActive) {
                return false;//硫磺海效果中
            }

            if (NPC.AnyNPCs(CWRID.NPC_OldDuke)) {
                return false;//老公爵在场
            }

            if (Main.LocalPlayer.mouseInterface) {
                return false;//mouseInterface
            }

            return true;
        }

        private static void TriggerInteraction() {
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.3f, Volume = 0.6f });

            if (!OldDukeStorySync.Read(d => d.OldDukeFirstCampsiteDialogueCompleted, d => d.OldDukeFirstCampsiteDialogueCompleted)) {
                OldDukeStorySync.Write(
                    d => d.OldDukeFirstCampsiteDialogueCompleted = true,
                    d => d.OldDukeFirstCampsiteDialogueCompleted = true);
                NarrativeRouter.Begin<Quest.FindFragments.FirstCampsiteDialogue>();
                return;
            }

            NarrativeRouter.Begin<CampsiteInteractionDialogue>();
        }

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

        private static void CheckPlayerProximity() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            float distance = Vector2.Distance(player.Center, CampsitePosition);
            bool wasNearby = isPlayerNearby;
            isPlayerNearby = distance < InteractDistance;
            foreach (OldDukeWanderingActor entity in ActorLoader.GetActiveActors<OldDukeWanderingActor>()) {
                if (entity.Position.To(player.Center).Length() < InteractDistance) {
                    isPlayerNearby = true;
                    break;
                }
            }

            if (isPlayerNearby && CanTriggerInteraction()) {
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

            if (isPlayerNearby && !wasNearby) {
                OnEnterCampsite?.Invoke(CampsitePosition);
            }
        }

        /// <summary>生成营地，isRelocation跳过箱子</summary>
        public static void GenerateCampsite(Vector2 position, bool isRelocation = false) {
            if (IsGenerated) {
                return;
            }

            CampsitePosition = position;
            IsGenerated = true;

            OldDukeCampsiteGenerationService.PlaceCampsite(position, isRelocation);

            //Y贴锅群，±120
            List<CampsitePotActor> pots = ActorLoader.GetActiveActors<CampsitePotActor>();
            if (pots.Count > 0) {
                float y = 0;
                foreach (CampsitePotActor pot in pots) {
                    y += pot.Position.Y;
                }
                y /= pots.Count;
                CampsitePosition = new Vector2(CampsitePosition.X, MathHelper.Clamp(CampsitePosition.Y, y - 120, y + 120));
            }

            OnCampsiteGenerated();
        }

        /// <summary>清本地状态，不含Actor</summary>
        public static void ClearCampsite() {
            MermanRodMoveback = false;
            IsGenerated = false;
            animationFrame = 0;
            animationTimer = 0;
            isPlayerNearby = false;
            interactPromptAlpha = 0f;
            pendingGenerationRequest = false;
            CampsitePosition = Vector2.Zero;
        }

        /// <summary>清营地+Actor并广播</summary>
        public static void ClearCampsiteAndSync() {
            if (VaultUtils.isServer) {
                ClearCampsite();
                OldDukeCampsiteGenerationService.ClearCampsiteActors();
                ModPacket packet = CWRMod.Instance.GetPacket();
                packet.Write((byte)CWRMessageType.OldDukeCampsiteSync);
                packet.Write(false);
                packet.Send();
            }
            else if (VaultUtils.isSinglePlayer) {
                ClearCampsite();
                OldDukeCampsiteGenerationService.ClearCampsiteActors();
            }
        }

        public static Rectangle GetCurrentFrame() {
            if (OldDuke == null) {
                return Rectangle.Empty;
            }

            int frameHeight = OldDuke.Height / 7;
            return new Rectangle(0, frameHeight * animationFrame, OldDuke.Width, frameHeight);
        }

        public static float GetInteractPromptAlpha() => interactPromptAlpha;

        public static bool CanInteract() => isPlayerNearby && interactPromptAlpha > 0.5f;

        public override void Unload() {
            ClearCampsite();
        }
    }
}
