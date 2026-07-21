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
    /// 鬼切鸟居：世界出生点附近的地表会立起一座鸟居（3D模型+<see cref="ToriiShrineActor"/>），
    /// 鸟居下插着鬼切。刀从开荒第一天就在那里，随时可拔。拔刀按玩家独立结算
    /// （<see cref="Data.Modules.HimayoStoryData.ToriiSwordTaken"/>），拔刀后天色渐入逢魔黄昏
    /// （<see cref="ToriiDusk"/>），鸟居对该玩家原地化樱消散（<see cref="ToriiShrineActor"/> 的本地演出），
    /// 拿到刀后 <see cref="FirstMetHimayo"/> 会经由其触发策略在演出收尾后自动接管，黄昏持续到对话落幕
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
        public static LocalizedText InventoryFullHint { get; private set; }

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

            //存档半损防线：位置键缺失/越界时废弃标记，让下一次更新走正常重新选址，
            //否则鸟居会"生成"在(0,0)之类玩家永远找不到的地方
            if (IsGenerated && !IsValidShrinePosition(ShrinePosition)) {
                CWRMod.Instance.Logger.Warn($"[ToriiShrine:LoadWorldData] Discarding invalid shrine position {ShrinePosition}, will regenerate");
                IsGenerated = false;
                ShrinePosition = Vector2.Zero;
            }
        }

        /// <summary>神社锚点是否落在世界有效范围内（含选址器同款的40格边缘余量）</summary>
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
        /// 尝试生成神社（服务器或单人执行）：选址失败时兜底在出生点，并尽量向下吸附到实心地面
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
            if (position == null) {
                //兜底也要落地：出生点向下吸附地面，免得极端地形（空岛/虚空类世界）出现悬空鸟居；
                //连地面都没有时保留出生点原始坐标，至少保证神社存在且可交互
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
        /// 神社出现时的客户端听觉反馈：附近玩家能听到一声远处的清响。
        /// 半径需覆盖选址器的最远落点（160格），保证新世界首次进入必有提示音
        /// </summary>
        private static void OnShrineGenerated() {
            if (Main.dedServ) {
                return;
            }
            //if (Main.LocalPlayer.Alives() && Main.LocalPlayer.DistanceSQ(ShrinePosition) < 3200f * 3200f) {
            //    SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.55f, Pitch = -0.35f }, ShrinePosition);
            //}
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
            //退场演出若被世界卸载打断，归还 Models3D 合成权
            ToriiShrineDissolve.Reset();
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
            if (ToriiShrineActor.PullRiteHolding) {
                return false;
            }
            return true;
        }

        private static void TriggerInteraction() {
            SoundEngine.PlaySound(SoundID.MenuTick with { Pitch = -0.3f, Volume = 0.6f });
            TryBeginPullRite();
        }

        /// <summary>
        /// 背包满时拒绝拔刀：鬼切没有兜底获取途径，绝不能让它以掉落物形态
        /// 落地冒消失风险（拔刀标记一落即不可逆，刀丢了就是永久软锁）
        /// </summary>
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
        /// 拔刀入口（本地玩家）：优先走拔刀仪式（Actor 动画 + <see cref="ToriiPullCutscene"/> 运镜），
        /// 鬼切在仪式到手帧交付；Actor 缺席等异常情形退化为 <see cref="PullSword"/> 瞬发拔刀，
        /// 保证刀永远不会因演出系统而拿不到
        /// </summary>
        internal static void TryBeginPullRite() {
            Player player = Main.LocalPlayer;
            if (!SwordPresentForLocalPlayer() || !CheckInventorySpace(player)) {
                return;
            }

            foreach (ToriiShrineActor actor in ActorLoader.GetActiveActors<ToriiShrineActor>()) {
                if (actor.BeginPullRite()) {
                    //运镜播放失败（更高优先级片段在播）不致命：仪式照演，只是镜头不动
                    CutsceneDirector.Play<ToriiPullCutscene, int>(actor.WhoAmI, restartSameClip: false);
                    return;
                }
            }

            PullSword();
        }

        /// <summary>
        /// 仪式到手帧的交付：落拔刀标记、入包、到手清响；
        /// 拔离时刻的迸发/震屏/拔刀声由仪式自身在对应节拍播放
        /// </summary>
        internal static void GrantSwordFromRite(Player player) {
            if (!SwordPresentForLocalPlayer()) {
                return;
            }

            HimayoStorySync.MarkToriiSwordTaken();
            player.QuickSpawnItem(player.GetSource_Misc("ToriiShrine"), ModContent.ItemType<OnikiriItem>());
        }

        /// <summary>
        /// 瞬发拔刀（无仪式兜底路径）：交付鬼切、落拔刀标记、震屏与声画演出，
        /// 鸟居随即开始本地退场（黄昏渐入→原地化樱消散），
        /// <see cref="FirstMetHimayo"/> 的触发策略在退场收尾后自动开演
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
