using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.Actors;
using InnoVault.Models3D.Runtime;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>
    /// 鬼切鸟居：世界出生点附近的地表会立起一座鸟居（3D模型+<see cref="ToriiShrineActor"/>），
    /// 鸟居下插着鬼切。刀从开荒第一天就在那里，但要等丛林龙陨落（无灾厄时以月亮领主为准）才拔得动；
    /// 早期尝试会触发 <see cref="ToriiSealedDialogue"/> 的低语。拔刀按玩家独立结算
    /// （<see cref="Data.Modules.HimayoStoryData.ToriiSwordTaken"/>），拿到刀后
    /// <see cref="FirstMetHimayo"/> 会经由其触发策略自动接管
    /// </summary>
    internal class ToriiShrine : ModSystem, ILocalizedModType, IWorldInfo
    {
        //鸟居3D模型：客户端在PostSetupContent自动加载，服务端得到Vault3DModel.Empty
        //模型来源: "Torii" by kazukisakamoto (Sketchfab, CC-BY-4.0)，署名见同目录license.txt
        [VaultLoaden("Assets/Models/Torii/scene")]
        public static Vault3DModel ToriiModel = null;
        [VaultLoaden("CalamityOverhaul/Content/LegendWeapon/OnikiriLegend/OnikiriItem")]
        public static Asset<Texture2D> OnikiriTexture = null;

        /// <summary>
        /// 神社是否已生成
        /// </summary>
        public static bool IsGenerated { get; internal set; }
        /// <summary>
        /// 神社地面锚点（鸟居正下方地表中心，像素坐标）
        /// </summary>
        public static Vector2 ShrinePosition { get; private set; }

        public string LocalizationCategory => "ADV.ToriiShrine";

        public static LocalizedText InteractHint { get; private set; }
        public static LocalizedText SealedHint { get; private set; }

        //交互状态（纯本地）
        private static bool isPlayerNearby;
        private static float interactPromptAlpha;
        private const float InteractDistance = 190f;

        //生成请求去重，避免联机下条件成立到收到回执之间逐帧刷包
        private static bool pendingGenerationRequest;

        //补种自检的节流计时器
        private static int ensureCheckTimer;

        public override void SetStaticDefaults() {
            InteractHint = this.GetLocalization(nameof(InteractHint), () => "[右键] 拔刀");
            SealedHint = this.GetLocalization(nameof(SealedHint), () => "[右键] 握住刀柄");
        }

        public override void SaveWorldData(TagCompound tag) {
            tag[nameof(IsGenerated)] = IsGenerated;
            tag[nameof(ShrinePosition)] = ShrinePosition;
        }

        public override void LoadWorldData(TagCompound tag) {
            IsGenerated = false;
            ShrinePosition = Vector2.Zero;
            try {
                if (tag != null && tag.TryGet(nameof(IsGenerated), out bool generated)) {
                    IsGenerated = generated;
                }
                if (tag != null && tag.TryGet(nameof(ShrinePosition), out Vector2 pos)) {
                    ShrinePosition = pos;
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"[ToriiShrine:LoadWorldData] Failed to load shrine data: {ex.Message}");
                IsGenerated = false;
                ShrinePosition = Vector2.Zero;
            }
        }

        public override void NetSend(BinaryWriter writer) {
            writer.Write(IsGenerated);
            if (IsGenerated) {
                writer.WriteVector2(ShrinePosition);
            }
        }

        public override void NetReceive(BinaryReader reader) {
            IsGenerated = reader.ReadBoolean();
            if (IsGenerated) {
                ShrinePosition = reader.ReadVector2();
            }
        }

        public override void OnWorldLoad() => ResetLocalState();

        public override void OnWorldUnload() => ClearShrine();

        public override void Unload() => ClearShrine();

        private static void ResetLocalState() {
            isPlayerNearby = false;
            interactPromptAlpha = 0f;
            pendingGenerationRequest = false;
            ensureCheckTimer = 0;
        }

        public override void PostUpdatePlayers() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return;
            }

            if (ShouldGenerateShrine()) {
                RequestShrineGeneration();
            }
        }

        public override void PostUpdateEverything() {
            if (!IsGenerated) {
                return;
            }

            //声明式补种：世界重载后Actor缺失时自动补回
            EnsureShrinePlaced();

            if (!Main.dedServ) {
                UpdateInteraction();
            }
        }

        /// <summary>
        /// 鸟居从进入世界的第一天起就存在：只要尚未生成且不在子世界，就该立起来
        /// </summary>
        private static bool ShouldGenerateShrine() {
            if (IsGenerated) {
                return false;
            }
            if (SubWorldRef.AnyActiveSubWorld()) {
                return false;
            }
            return true;
        }

        private static void RequestShrineGeneration() {
            if (VaultUtils.isSinglePlayer) {
                TryGenerateShrine();
            }
            else if (VaultUtils.isClient) {
                SendGenerationRequest();
            }
        }

        /// <summary>
        /// 客户端发送生成请求给服务器，带去重标记避免收到回执前逐帧刷包
        /// </summary>
        private static void SendGenerationRequest() {
            if (pendingGenerationRequest) {
                return;
            }
            pendingGenerationRequest = true;
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.ToriiShrineGenerationRequest);
            packet.Send();
        }

        /// <summary>
        /// 尝试生成神社（服务器或单人执行）：选址失败时兜底在出生点正上方
        /// </summary>
        public static void TryGenerateShrine() {
            if (VaultUtils.isClient) {
                return;
            }
            if (IsGenerated) {
                //联机下多名客户端可能同时请求，已生成时只需补发一次同步
                if (VaultUtils.isServer) {
                    SyncShrineToClients();
                }
                return;
            }

            Vector2? position = ToriiShrineLocationFinder.FindBestLocation();
            GenerateShrine(position ?? new Vector2(Main.spawnTileX * 16f + 8f, Main.spawnTileY * 16f));

            if (VaultUtils.isServer) {
                SyncShrineToClients();
            }
        }

        private static void SyncShrineToClients() {
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.ToriiShrineSync);
            packet.Write(IsGenerated);
            if (IsGenerated) {
                packet.WriteVector2(ShrinePosition);
            }
            packet.Send();
        }

        /// <summary>
        /// 客户端接收神社同步数据
        /// </summary>
        internal static void ReceiveShrineSync(BinaryReader reader) {
            //收到一次回执就说明请求周期已经走完，允许下次再发
            pendingGenerationRequest = false;

            bool wasGenerated = IsGenerated;
            IsGenerated = reader.ReadBoolean();

            if (IsGenerated) {
                ShrinePosition = reader.ReadVector2();
                if (!wasGenerated) {
                    OnShrineGenerated();
                }
            }
        }

        /// <summary>
        /// 生成神社：写入位置并放置鸟居Actor（Actor仅服务端/单人放置，客户端经框架同步获得）
        /// </summary>
        public static void GenerateShrine(Vector2 groundAnchor) {
            if (IsGenerated) {
                return;
            }

            ShrinePosition = groundAnchor;
            IsGenerated = true;

            PlaceShrineActor();
            OnShrineGenerated();
        }

        /// <summary>
        /// 神社出现时的客户端听觉反馈：附近玩家能听到一声远处的清响
        /// </summary>
        private static void OnShrineGenerated() {
            if (Main.dedServ) {
                return;
            }
            if (Main.LocalPlayer.Alives() && Main.LocalPlayer.DistanceSQ(ShrinePosition) < 2200f * 2200f) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.55f, Pitch = -0.35f }, ShrinePosition);
            }
        }

        /// <summary>
        /// 声明式补种检查：已生成但没有存活的鸟居Actor时重新放置一次
        /// </summary>
        private static void EnsureShrinePlaced() {
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;
            }

            ensureCheckTimer++;
            if (ensureCheckTimer < 60) {
                return;
            }
            ensureCheckTimer = 0;

            if (ActorLoader.GetActiveActors<ToriiShrineActor>().Count > 0) {
                return;
            }

            PlaceShrineActor();
        }

        private static void PlaceShrineActor() {
            if (!VaultUtils.isServer && !VaultUtils.isSinglePlayer) {
                return;
            }
            ActorLoader.NewActor<ToriiShrineActor>(ShrinePosition);
        }

        /// <summary>
        /// 清除神社的本地/存档状态（不含Actor清理，世界卸载等收尾路径调用）
        /// </summary>
        public static void ClearShrine() {
            IsGenerated = false;
            ShrinePosition = Vector2.Zero;
            ResetLocalState();
        }

        #region 交互
        /// <summary>
        /// 刀对本地玩家是否仍然在场：拔过、或者背包里已经有鬼切的玩家看不到刀
        /// </summary>
        public static bool SwordPresentForLocalPlayer() {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active) {
                return false;
            }
            if (HimayoStorySync.ToriiSwordTaken) {
                return false;
            }
            return !player.HasItem(ModContent.ItemType<OnikiriItem>());
        }

        /// <summary>
        /// 拔刀的进度门槛：丛林龙陨落之后；无灾厄环境退化为月亮领主
        /// </summary>
        public static bool GateOpen => CWRRef.Has ? CWRRef.GetDownedYharon() : NPC.downedMoonlord;

        public static float GetInteractPromptAlpha() => interactPromptAlpha;

        public static string GetPromptText() => GateOpen ? InteractHint.Value : SealedHint.Value;

        private static void UpdateInteraction() {
            CheckPlayerProximity();

            if (isPlayerNearby && interactPromptAlpha > 0.5f
                && CanTriggerInteraction() && Main.mouseRight && Main.mouseRightRelease) {
                TriggerInteraction();
            }
        }

        private static void CheckPlayerProximity() {
            Player player = Main.LocalPlayer;
            bool swordPresent = player != null && player.active && SwordPresentForLocalPlayer();
            Vector2 swordAnchor = ShrinePosition + new Vector2(0, -ToriiShrineActor.SwordCenterHeight);

            isPlayerNearby = swordPresent && player.Center.Distance(swordAnchor) < InteractDistance;

            //交互提示淡入淡出
            if (isPlayerNearby && CanTriggerInteraction()) {
                if (interactPromptAlpha < 1f) {
                    interactPromptAlpha += 0.05f;
                }
            }
            else if (interactPromptAlpha > 0f) {
                interactPromptAlpha -= 0.05f;
            }

            interactPromptAlpha = MathHelper.Clamp(interactPromptAlpha, 0f, 1f);
        }

        private static bool CanTriggerInteraction() {
            if (Main.mapFullscreen) {
                return false;
            }
            if (Main.LocalPlayer.mouseInterface) {
                return false;
            }
            if (NarrativeRouter.IsActive<ToriiSealedDialogue>()) {
                return false;
            }
            return true;
        }

        private static void TriggerInteraction() {
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.3f, Volume = 0.6f });

            if (!GateOpen) {
                //还不是时候：刀身低语
                NarrativeRouter.Begin<ToriiSealedDialogue>();
                return;
            }

            PullSword();
        }

        /// <summary>
        /// 拔刀（本地玩家）：交付鬼切、落拔刀标记、震屏与声画演出；
        /// <see cref="FirstMetHimayo"/> 的触发策略检测到背包里的鬼切后会自动开演
        /// </summary>
        internal static void PullSword() {
            Player player = Main.LocalPlayer;
            if (!SwordPresentForLocalPlayer()) {
                return;
            }

            HimayoStorySync.MarkToriiSwordTaken();
            player.QuickSpawnItem(player.GetSource_Misc("ToriiShrine"), ModContent.ItemType<OnikiriItem>());

            player.CWR().GetScreenShake(10f);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.35f, Volume = 0.9f }, ShrinePosition);
            SoundEngine.PlaySound(SoundID.Item4 with { Pitch = 0.45f, Volume = 0.5f }, ShrinePosition);

            foreach (ToriiShrineActor actor in ActorLoader.GetActiveActors<ToriiShrineActor>()) {
                actor.SwordPulledBurst();
            }
        }
        #endregion

        /// <summary>
        /// 调试入口（单人）：在指定位置附近向下吸附地面并强制重建神社，会清掉旧状态与Actor
        /// </summary>
        public static void DebugRebuildAt(Vector2 worldPos) {
            if (!VaultUtils.isSinglePlayer) {
                return;
            }
            foreach (ToriiShrineActor actor in ActorLoader.GetActiveActors<ToriiShrineActor>()) {
                ActorLoader.KillActor(actor.WhoAmI);
            }
            ClearShrine();
            if (ToriiShrineLocationFinder.TrySnapToGround(worldPos, out Vector2 groundPos)) {
                GenerateShrine(groundPos);
            }
        }
    }
}
