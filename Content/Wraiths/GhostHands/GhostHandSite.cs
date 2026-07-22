using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Actors;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 焦黑裂隙据点。选点/活化/锁投放与反噬补给；状态在 SiteSystem
    /// </summary>
    internal sealed class GhostHandSite : ModSystem
    {
        //====据点参数（§3.2）====
        private const float TriggerRadius = 900f;
        private const int CooldownTicks = 18000;
        private const int AnchorRetryTicks = 2700;
        //====选点采样（§3.1）====
        private const int SampleAttempts = 60;
        private const int SampleSpreadX = 140;
        private const int SampleSpreadY = 90;
        //气窝 5 宽 × 4 高(瓦)
        private const int PocketWidth = 5;
        private const int PocketHeight = 4;
        //自然墙背书占比
        private const float WallBackingRatio = 0.40f;
        //====锁经济（§3.4）====
        private const float LockDenyCarrierRange = 2000f;
        private const float ResupplyOwnerRange = 300f;

        private int resupplyTimer;

        internal static WraithSitePlan BuildPlan() => new() {
            AnchorPicker = PickAnchor,
            ActivationCondition = EvaluateActivation,
            TriggerRadius = TriggerRadius,
            CooldownTicks = CooldownTicks,
            AnchorRetryTicks = AnchorRetryTicks,
        };

        //====动态锚定选点====

        /// <summary>岩石层下采样裂隙位，失败返回 null</summary>
        private static Vector2? PickAnchor(WraithSiteContext ctx) {
            Player candidate = ctx.Candidate;
            if (candidate == null || candidate.Center.Y <= (Main.rockLayer + 40) * 16.0) {
                return null;
            }
            Point origin = candidate.Center.ToTileCoordinates();
            for (int attempt = 0; attempt < SampleAttempts; attempt++) {
                int x = origin.X + Main.rand.Next(-SampleSpreadX, SampleSpreadX + 1);
                int y = origin.Y + Main.rand.Next(-SampleSpreadY, SampleSpreadY + 1);
                x = Math.Clamp(x, 60, Main.maxTilesX - 60);
                y = Math.Clamp(y, (int)Main.rockLayer + 30, Main.maxTilesY - 220);
                Vector2? anchor = EvaluateSample(x, y);
                if (anchor != null) {
                    return anchor;
                }
            }
            return null;
        }

        /// <summary>单样本评估裂隙锚</summary>
        private static Vector2? EvaluateSample(int cx, int cy) {
            int left = cx - PocketWidth / 2;
            int top = cy - 1;

            //洞穴气窝 5×4
            int naturalWall = 0;
            for (int x = left; x < left + PocketWidth; x++) {
                for (int y = top; y < top + PocketHeight; y++) {
                    if (!WorldGen.InWorld(x, y, 40)) {
                        return null;
                    }
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) {
                        return null;
                    }
                    if (tile.WallType != WallID.None && !Main.wallHouse[tile.WallType]) {
                        naturalWall++;
                    }
                }
            }
            if (naturalWall < PocketWidth * PocketHeight * WallBackingRatio) {
                return null;
            }

            //石质壁面竖列
            int faceColumn = 0;
            int faceDir = 0;
            for (int dx = 1; dx <= 8 && faceDir == 0; dx++) {
                if (IsStoneFaceColumn(cx - dx, cy)) {
                    faceColumn = cx - dx;
                    faceDir = -1;
                }
                else if (IsStoneFaceColumn(cx + dx, cy)) {
                    faceColumn = cx + dx;
                    faceDir = 1;
                }
            }
            if (faceDir == 0) {
                return null;
            }

            //禁区排除
            for (int x = left - 2; x < left + PocketWidth + 2; x++) {
                for (int y = top - 2; y < top + PocketHeight + 2; y++) {
                    if (!WorldGen.InWorld(x, y, 40)) {
                        continue;
                    }
                    Tile tile = Main.tile[x, y];
                    if (!tile.HasTile) {
                        continue;
                    }
                    int type = tile.TileType;
                    if (type == TileID.BlueDungeonBrick || type == TileID.GreenDungeonBrick
                        || type == TileID.PinkDungeonBrick || type == TileID.LihzahrdBrick || type == TileID.Hive) {
                        return null;
                    }
                }
            }

            //锚点偏气窝侧 24px
            return new Vector2(faceColumn * 16f + 8f - faceDir * 24f, cy * 16f + 8f);
        }

        /// <summary>竖列 ≥3 瓦连续石质</summary>
        private static bool IsStoneFaceColumn(int x, int cy) {
            int run = 0;
            for (int y = cy - 2; y <= cy + 2; y++) {
                if (WorldGen.InWorld(x, y, 40) && IsStoneTile(x, y)) {
                    if (++run >= 3) {
                        return true;
                    }
                }
                else {
                    run = 0;
                }
            }
            return false;
        }

        private static bool IsStoneTile(int x, int y) {
            Tile tile = Main.tile[x, y];
            if (!tile.HasTile || !Main.tileSolid[tile.TileType]) {
                return false;
            }
            return tile.TileType == TileID.Stone || TileID.Sets.Conversion.Stone[tile.TileType];
        }

        //====活化条件（逐进入者评估，D2）====

        private static bool EvaluateActivation(WraithSiteContext ctx) {
            Player candidate = ctx.Candidate;
            if (candidate == null || candidate.dead) {
                return false;
            }
            //二幕起
            if (!WraithActs.ActTwo) {
                return false;
            }
            //进入者在岩石层
            if (candidate.Center.Y <= Main.rockLayer * 16.0) {
                return false;
            }
            //已认主不再触发
            WraithVesselHandle vessel = WraithVessels.ResolveCarried(candidate);
            if (vessel.IsValid && vessel.Store.TryGet(ctx.Definition.Key, out WraithProgressRecord record) && record.PactRenewed) {
                return false;
            }
            //锚完整性复检
            if (!AnchorIntact(ctx.Anchor)) {
                WraithSiteSystem.Unanchor(ctx.Definition.Key);
                return false;
            }
            return true;
        }

        /// <summary>锚点附近仍有石质瓦</summary>
        private static bool AnchorIntact(Vector2 anchor) {
            Point tile = anchor.ToTileCoordinates();
            int stone = 0;
            for (int x = tile.X - 3; x <= tile.X + 3; x++) {
                for (int y = tile.Y - 3; y <= tile.Y + 3; y++) {
                    if (WorldGen.InWorld(x, y, 40) && IsStoneTile(x, y) && ++stone >= 3) {
                        return true;
                    }
                }
            }
            return false;
        }

        //====长命锁投放与补给（服务器权威，锁经济恒为 1）====

        /// <summary>破壁拍投锁，世界无锁且附近无人持锁时上抛</summary>
        internal static void ThrowLockOnEmerge(GhostHandActor hand) {
            if (VaultUtils.isClient || AnyLockInEconomy(hand.Center, LockDenyCarrierRange)) {
                return;
            }
            //破口法线上抛
            SpawnLock(hand.Center, new Vector2(hand.ResolveEmergeDir() * 3f, -3f));
        }

        /// <summary>世界已有掉落锁</summary>
        private static bool AnyWorldLock() {
            int lockType = ModContent.ItemType<CharredLock>();
            foreach (Item item in Main.ActiveItems) {
                if (item.type == lockType && item.stack > 0) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>世界或附近已有锁</summary>
        private static bool AnyLockInEconomy(Vector2 around, float carrierRange) {
            if (AnyWorldLock()) {
                return true;
            }
            foreach (Player player in Main.ActivePlayers) {
                if (Vector2.DistanceSquared(player.Center, around) <= carrierRange * carrierRange
                    && GhostHandActor.HasCharredLock(player)) {
                    return true;
                }
            }
            return false;
        }

        private static int SpawnLock(Vector2 position, Vector2 velocity) {
            int index = Item.NewItem(new EntitySource_Misc("CWR_GhostHandLock"), position, Vector2.One,
                ModContent.ItemType<CharredLock>());
            if (index >= 0 && index < Main.maxItems) {
                Main.item[index].velocity = velocity;
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, -1, -1, null, index);
                }
            }
            return index;
        }

        /// <summary>反噬期回据点取锁，1Hz</summary>
        public override void PostUpdateEverything() {
            if (VaultUtils.isClient || Main.gameMenu || ++resupplyTimer < 60) {
                return;
            }
            resupplyTimer = 0;
            //上线闸关跳过
            if (!WraithDirector.CanonContentActive) {
                return;
            }

            GhostHandActor escaped = null;
            foreach (GhostHandActor hand in ActorLoader.GetActiveActors<GhostHandActor>()) {
                if (hand.IsEscaped) {
                    escaped = hand;
                    break;
                }
            }
            if (escaped == null) {
                return;
            }
            Player owner = escaped.EscapedOwnerPlayer;
            if (owner == null || owner.dead) {
                return;
            }
            if (!WraithSiteSystem.TryGet(escaped.Definition.Key, out WraithSiteRecord record) || !record.Anchored) {
                return;
            }
            if (Vector2.DistanceSquared(owner.Center, record.Anchor) > ResupplyOwnerRange * ResupplyOwnerRange) {
                return;
            }
            //补给门
            if (GhostHandActor.HasCharredLock(owner) || AnyWorldLock()) {
                return;
            }

            SpawnLock(record.Anchor, new Vector2(0f, -2f));
            if (VaultUtils.isServer) {
                //本端浮字，远端聊天行
                if (GhostHand.LockUnearthed != null) {
                    ChatHelper.SendChatMessageToClient(GhostHand.LockUnearthed.ToNetworkText(),
                        GhostHandDrawHelper.Ember, owner.whoAmI);
                }
            }
            else {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.7f, Pitch = -0.1f }, record.Anchor);
                if (GhostHand.LockUnearthed != null) {
                    CombatText.NewText(new Rectangle((int)record.Anchor.X - 20, (int)record.Anchor.Y - 20, 40, 40),
                        GhostHandDrawHelper.Ember, GhostHand.LockUnearthed.Value, true);
                }
            }
        }
    }
}
