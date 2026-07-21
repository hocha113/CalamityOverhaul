using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.Actors;
using InnoVault.Cinematics;
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
    /// 世界出生点附近立鸟居(<see cref="ToriiShrineActor"/>)，下插鬼切<br/>
    /// 拔刀按玩家独立(<see cref="Data.Modules.HimayoStoryData.ToriiSwordTaken"/>)，接 <see cref="ToriiDusk"/> 与本地化樱，收尾后 <see cref="FirstMetHimayo"/> 接管
    /// </summary>
    internal class ToriiShrine : ModSystem, ILocalizedModType, IWorldInfo
    {
        //客户端PostSetupContent加载，服务端Empty
        //来源 "Torii" by kazukisakamoto (Sketchfab CC-BY-4.0)，见同目录license.txt
        [VaultLoaden("Assets/Models/Torii/scene")]
        public static Vault3DModel ToriiModel = null;
        [VaultLoaden("CalamityOverhaul/Content/LegendWeapon/OnikiriLegend/OnikiriItem")]
        public static Asset<Texture2D> OnikiriTexture = null;

        public static bool IsGenerated { get; internal set; }
        /// <summary>地面锚点，鸟居正下地表中心，像素</summary>
        public static Vector2 ShrinePosition { get; private set; }

        public string LocalizationCategory => "ADV.ToriiShrine";

        public static LocalizedText InteractHint { get; private set; }
        public static LocalizedText InventoryFullHint { get; private set; }

        //纯本地交互
        private static bool isPlayerNearby;
        private static float interactPromptAlpha;
        private const float InteractDistance = 190f;

        //生成请求去重，防回执前刷包
        private static bool pendingGenerationRequest;

        //补种自检节流
        private static int ensureCheckTimer;

        public override void SetStaticDefaults() {
            InteractHint = this.GetLocalization(nameof(InteractHint), () => "[右键] 拔刀");
            InventoryFullHint = this.GetLocalization(nameof(InventoryFullHint), () => "背包已满，腾出一格再来拔刀");
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

            //位置缺失/越界则废弃，防落在(0,0)
            if (IsGenerated && !IsValidShrinePosition(ShrinePosition)) {
                CWRMod.Instance.Logger.Warn($"[ToriiShrine:LoadWorldData] Discarding invalid shrine position {ShrinePosition}, will regenerate");
                IsGenerated = false;
                ShrinePosition = Vector2.Zero;
            }
        }

        /// <summary>锚点是否在有效范围(含40格边缘余量)</summary>
        private static bool IsValidShrinePosition(Vector2 position) {
            const float Margin = 40f * 16f;
            return position.X >= Margin && position.X <= Main.maxTilesX * 16f - Margin
                && position.Y >= Margin && position.Y <= Main.maxTilesY * 16f - Margin;
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

            //世界重载后Actor缺失则补种
            EnsureShrinePlaced();

            if (!Main.dedServ) {
                UpdateInteraction();
            }
        }

        /// <summary>尚未生成且非子世界</summary>
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

        /// <summary>发生成请求，pending去重</summary>
        private static void SendGenerationRequest() {
            if (pendingGenerationRequest) {
                return;
            }
            pendingGenerationRequest = true;
            ModPacket packet = CWRMod.Instance.GetPacket();
            packet.Write((byte)CWRMessageType.ToriiShrineGenerationRequest);
            packet.Send();
        }

        /// <summary>服务端/单人生成，选址失败则出生点吸附地面</summary>
        public static void TryGenerateShrine() {
            if (VaultUtils.isClient) {
                return;
            }
            if (IsGenerated) {
                //已生成则补发同步
                if (VaultUtils.isServer) {
                    SyncShrineToClients();
                }
                return;
            }

            Vector2? position = ToriiShrineLocationFinder.FindBestLocation();
            if (position == null) {
                //出生点吸附地面，无地面则保留出生点
                Vector2 spawnPos = new(Main.spawnTileX * 16f + 8f, Main.spawnTileY * 16f);
                position = ToriiShrineLocationFinder.TrySnapToGround(spawnPos, out Vector2 snapped)
                    ? snapped : spawnPos;
            }
            GenerateShrine(position.Value);

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

        /// <summary>收神社同步</summary>
        internal static void ReceiveShrineSync(BinaryReader reader) {
            //回执到，清pending
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

        /// <summary>写入位置并放Actor(仅服务端/单人放，客户端靠框架同步)</summary>
        public static void GenerateShrine(Vector2 groundAnchor) {
            if (IsGenerated) {
                return;
            }

            ShrinePosition = groundAnchor;
            IsGenerated = true;

            PlaceShrineActor();
            OnShrineGenerated();
        }

        /// <summary>出现时清响，半径需盖住选址最远160格</summary>
        private static void OnShrineGenerated() {
            if (Main.dedServ) {
                return;
            }
            //if (Main.LocalPlayer.Alives() && Main.LocalPlayer.DistanceSQ(ShrinePosition) < 3200f * 3200f) {
            //    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.55f, Pitch = -0.35f }, ShrinePosition);
            //}
        }

        /// <summary>已生成但无存活Actor则重放</summary>
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

        /// <summary>清本地/存档态(不含Actor)，世界卸载等收尾用</summary>
        public static void ClearShrine() {
            IsGenerated = false;
            ShrinePosition = Vector2.Zero;
            ResetLocalState();
            //卸载打断时归还Models3D合成权
            ToriiShrineDissolve.Reset();
        }

        #region 交互
        /// <summary>本地玩家是否仍看得到刀(拔过或包里已有则否)</summary>
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

        public static float GetInteractPromptAlpha() => interactPromptAlpha;

        public static string GetPromptText() => InteractHint.Value;

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

            //提示淡入淡出
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
            if (ToriiShrineActor.PullRiteHolding) {
                return false;
            }
            return true;
        }

        private static void TriggerInteraction() {
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.3f, Volume = 0.6f });
            TryBeginPullRite();
        }

        /// <summary>背包满拒拔，无兜底，标记不可逆会软锁</summary>
        private static bool CheckInventorySpace(Player player) {
            Item onikiri = new(ModContent.ItemType<OnikiriItem>());
            if (player.ItemSpace(onikiri).CanTakeItemToPersonalInventory) {
                return true;
            }
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.6f, Volume = 0.5f });
            CombatText.NewText(player.getRect(), new Color(235, 95, 118), InventoryFullHint.Value);
            return false;
        }

        /// <summary>
        /// 拔刀入口，优先仪式(<see cref="ToriiPullCutscene"/>)，Actor缺席则 <see cref="PullSword"/>
        /// </summary>
        internal static void TryBeginPullRite() {
            Player player = Main.LocalPlayer;
            if (!SwordPresentForLocalPlayer() || !CheckInventorySpace(player)) {
                return;
            }

            foreach (ToriiShrineActor actor in ActorLoader.GetActiveActors<ToriiShrineActor>()) {
                if (actor.BeginPullRite()) {
                    //运镜失败不致命，仪式照演
                    CutsceneDirector.Play<ToriiPullCutscene, int>(actor.WhoAmI, restartSameClip: false);
                    return;
                }
            }

            PullSword();
        }

        /// <summary>仪式到手帧交付，迸发/震屏/拔刀声由仪式节拍自播</summary>
        internal static void GrantSwordFromRite(Player player) {
            if (!SwordPresentForLocalPlayer()) {
                return;
            }

            HimayoStorySync.MarkToriiSwordTaken();
            player.QuickSpawnItem(player.GetSource_Misc("ToriiShrine"), ModContent.ItemType<OnikiriItem>());
        }

        /// <summary>
        /// 瞬发拔刀兜底，交付后本地退场，<see cref="FirstMetHimayo"/> 收尾后开演
        /// </summary>
        internal static void PullSword() {
            Player player = Main.LocalPlayer;
            if (!SwordPresentForLocalPlayer() || !CheckInventorySpace(player)) {
                return;
            }

            HimayoStorySync.MarkToriiSwordTaken();
            player.QuickSpawnItem(player.GetSource_Misc("ToriiShrine"), ModContent.ItemType<OnikiriItem>());

            player.CWR().GetScreenShake(10f);

            foreach (ToriiShrineActor actor in ActorLoader.GetActiveActors<ToriiShrineActor>()) {
                actor.SwordPulledBurst();
                actor.BeginDeparture();
            }
        }
        #endregion

        /// <summary>调试重建(单人)，附近吸附地面并清旧态</summary>
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
