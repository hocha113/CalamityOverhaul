using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend;
using CalamityOverhaul.OtherMods.SubWorld;
using InnoVault.Actors;
using InnoVault.Cinematics;
using InnoVault.Models3D.Runtime;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
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

        public string LocalizationCategory => "ADV.Himayo";

        public static LocalizedText InteractHint { get; private set; }
        public static LocalizedText InventoryFullHint { get; private set; }

        //纯本地交互
        private static bool isPlayerNearby;
        private static float interactPromptAlpha;
        private const float InteractDistance = 190f;

        //运行期低频自检；首个安全更新帧不经过该节流
        private const int EnsureCheckInterval = 60;
        private const int PlacementFailureLogCooldown = 300;
        private static int ensureCheckTimer;
        private static int placementFailureLogTimer;

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

            //世界尺寸已就绪时立即废弃损坏位置；未就绪则由首帧权威维护统一修复。
            if (IsGenerated && ToriiShrineLocationFinder.WorldGeometryReady
                && !ToriiShrineLocationFinder.IsValidWorldPosition(ShrinePosition)) {
                CWRMod.Instance.Logger.Warn($"[ToriiShrine:LoadWorldData] Discarding invalid shrine position {ShrinePosition}, will regenerate");
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
            ShrinePosition = IsGenerated ? reader.ReadVector2() : Vector2.Zero;
            if (IsGenerated && !ToriiShrineLocationFinder.IsValidWorldPosition(ShrinePosition)) {
                CWRMod.Instance.Logger.Warn($"[ToriiShrine:NetReceive] Ignoring invalid shrine position {ShrinePosition}");
                IsGenerated = false;
                ShrinePosition = Vector2.Zero;
            }
        }

        public override void OnWorldLoad() => ResetLocalState();

        public override void OnWorldUnload() => ClearShrine();

        public override void Unload() => ClearShrine();

        private static void ResetLocalState() {
            isPlayerNearby = false;
            interactPromptAlpha = 0f;
            ensureCheckTimer = 0;
            placementFailureLogTimer = 0;
        }

        public override void PostUpdateEverything() {
            MaintainAuthoritativeShrine();

            if (IsGenerated && !Main.dedServ) {
                UpdateInteraction();
            }
        }

        /// <summary>
        /// 世界态由服务端/单人统一维护。首个安全更新帧立即恢复Actor，之后才进入低频自检。
        /// </summary>
        private static void MaintainAuthoritativeShrine() {
            if (VaultUtils.isClient || SubWorldRef.AnyActiveSubWorld()) {
                return;
            }

            if (placementFailureLogTimer > 0) {
                placementFailureLogTimer--;
            }

            if (IsGenerated && !ToriiShrineLocationFinder.IsValidWorldPosition(ShrinePosition)) {
                CWRMod.Instance.Logger.Warn($"[ToriiShrine] Invalid runtime position {ShrinePosition}, regenerating");
                IsGenerated = false;
                ShrinePosition = Vector2.Zero;
            }

            if (!IsGenerated) {
                TryGenerateShrine();
                return;
            }

            if (ensureCheckTimer > 0) {
                ensureCheckTimer--;
                return;
            }

            bool actorReady = EnsureSingleShrineActor();
            ensureCheckTimer = actorReady ? EnsureCheckInterval : 0;
        }

        /// <summary>服务端/单人解析可靠位置并生成；世界几何未就绪时由下一帧重试</summary>
        public static void TryGenerateShrine() {
            if (VaultUtils.isClient || IsGenerated || SubWorldRef.AnyActiveSubWorld()) {
                return;
            }
            if (!ToriiShrineLocationFinder.TryResolveGuaranteedLocation(
                out Vector2 position, out ToriiShrinePlacementTier tier)) {
                return;
            }

            if (tier != ToriiShrinePlacementTier.StrictTerrain) {
                CWRMod.Instance.Logger.Warn($"[ToriiShrine] Placement used {tier} fallback at {position}");
            }

            GenerateShrine(position);
            if (VaultUtils.isServer) {
                SyncShrineToClients();
            }
        }

        private static void SyncShrineToClients() {
            ModPacket packet = CWRNetWork.GetPacket<ToriiShrineSyncNet>();
            packet.Write(IsGenerated);
            if (IsGenerated) {
                packet.WriteVector2(ShrinePosition);
            }
            packet.Send();
        }

        /// <summary>客户端接收权威世界态；Actor实体仍由InnoVault生成广播同步</summary>
        internal static void ReceiveShrineSync(BinaryReader reader) {
            bool generated = reader.ReadBoolean();
            Vector2 position = generated ? reader.ReadVector2() : Vector2.Zero;
            if (!VaultUtils.isClient) {
                return;
            }

            if (generated && !ToriiShrineLocationFinder.IsValidWorldPosition(position)) {
                CWRMod.Instance.Logger.Warn($"[ToriiShrine:ReceiveShrineSync] Ignoring invalid shrine position {position}");
                generated = false;
                position = Vector2.Zero;
            }

            bool wasGenerated = IsGenerated;
            IsGenerated = generated;
            ShrinePosition = position;
            if (IsGenerated && !wasGenerated) {
                OnShrineGenerated();
            }
        }

        /// <summary>提交有效世界态并立即放置Actor；无效输入会走完整兜底链</summary>
        public static void GenerateShrine(Vector2 groundAnchor) {
            if (VaultUtils.isClient || IsGenerated) {
                return;
            }

            if (!ToriiShrineLocationFinder.IsValidWorldPosition(groundAnchor)) {
                if (!ToriiShrineLocationFinder.TryResolveGuaranteedLocation(
                    out groundAnchor, out ToriiShrinePlacementTier tier)) {
                    return;
                }
                CWRMod.Instance.Logger.Warn($"[ToriiShrine] Replaced invalid generation anchor with {tier} fallback at {groundAnchor}");
            }

            ShrinePosition = groundAnchor;
            IsGenerated = true;
            bool actorReady = EnsureSingleShrineActor();
            ensureCheckTimer = actorReady ? EnsureCheckInterval : 0;
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

        /// <summary>权威端维持恰好一个Actor，并把位置纠正到存档锚点</summary>
        private static bool EnsureSingleShrineActor() {
            if (VaultUtils.isClient || !IsGenerated
                || !ToriiShrineLocationFinder.IsValidWorldPosition(ShrinePosition)) {
                return false;
            }

            List<ToriiShrineActor> actors = ActorLoader.GetActiveActors<ToriiShrineActor>();
            if (actors.Count > 1) {
                CWRMod.Instance.Logger.Warn($"[ToriiShrine] Found {actors.Count} shrine actors; removing duplicates");
            }

            ToriiShrineActor keeper = null;
            foreach (ToriiShrineActor actor in actors) {
                if (keeper == null) {
                    keeper = actor;
                    continue;
                }
                ActorLoader.KillActor(actor.WhoAmI);
            }

            if (keeper != null) {
                if ((keeper.Position - ShrinePosition).LengthSquared() > 0.25f) {
                    CWRMod.Instance.Logger.Warn($"[ToriiShrine] Correcting actor position {keeper.Position} to {ShrinePosition}");
                    keeper.Position = ShrinePosition;
                    if (VaultUtils.isServer) {
                        keeper.NetUpdate = true;
                    }
                }
                return true;
            }

            int actorIndex = ActorLoader.NewActor<ToriiShrineActor>(ShrinePosition);
            if (actorIndex >= 0) {
                return true;
            }

            if (placementFailureLogTimer <= 0) {
                CWRMod.Instance.Logger.Error($"[ToriiShrine] Actor placement failed at {ShrinePosition}; retrying automatically");
                placementFailureLogTimer = PlacementFailureLogCooldown;
            }
            return false;
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
        /// <summary>本地玩家是否应看到完整鸟居；已拔刀或随身已有鬼切均隐藏</summary>
        public static bool ShouldShowForLocalPlayer() {
            Player player = Main.LocalPlayer;
            return player != null && player.active
                && !HimayoStorySync.ToriiSwordTaken
                && !player.HasItem(OnikiriOverride.ID);
        }

        /// <summary>本地玩家是否仍看得到并可拔取刀</summary>
        public static bool SwordPresentForLocalPlayer() => ShouldShowForLocalPlayer();

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
            player.GiveItem(player.GetSource_Misc("ToriiShrine"), ModContent.ItemType<OnikiriItem>());
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.45f, Volume = 0.5f }, player.Center);
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
            player.GiveItem(player.GetSource_Misc("ToriiShrine"), ModContent.ItemType<OnikiriItem>());

            player.CWR().GetScreenShake(10f);
            SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.45f, Volume = 0.5f }, ShrinePosition);

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

    /// <summary>鸟居神社权威世界态下发信道</summary>
    internal sealed class ToriiShrineSyncNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) => ToriiShrine.ReceiveShrineSync(reader);
    }
}
