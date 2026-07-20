using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Actors;
using Microsoft.Xna.Framework;
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
    /// 焦黑裂隙据点（WRAITHS-GHOSTHAND-PLAN.md §3）：岩石层石质洞壁的动态锚定选点、
    /// 锚完整性复检、逐进入者活化条件，以及长命锁的投放与反噬期补给（服务器权威，1Hz）。
    /// 锚状态与存档在 <see cref="WraithSiteSystem"/>，调度在 <c>WraithDirector</c>
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

        /// <summary>
        /// 候选玩家须位于岩石层下；每轮 60 次采样找"石质壁面 + 洞穴气窝 + 自然墙背书"的裂隙位，
        /// 全失败返回 null 等 45 秒重试
        /// </summary>
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

        /// <summary>单样本评估：气窝 → 自然墙背书 → 石质壁面 → 禁区排除 → 锚点=壁面中点向气窝侧偏 24px</summary>
        private static Vector2? EvaluateSample(int cx, int cy) {
            int left = cx - PocketWidth / 2;
            int top = cy - 1;

            //洞穴气窝:5×4 瓦无任何实体物块(平台视为实体拒绝),顺路统计自然墙
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

            //石质壁面:气窝中心向两侧各扫 8 瓦找 ≥3 瓦高连续石质实体竖列,两侧皆有取近侧
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

            //禁区排除:气窝外扩 2 瓦内不见地牢砖/丛林蜥蜴砖/蜂巢块
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

            //裂隙长在壁面上,手自壁中来:壁面竖列中点的瓦中心向气窝侧偏 24px
            return new Vector2(faceColumn * 16f + 8f - faceDir * 24f, cy * 16f + 8f);
        }

        /// <summary>竖列判定：该列在气窝纵向邻域内有 ≥3 瓦连续石质实体（含腐/猩/珍珠石变体）</summary>
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
            //二幕起(幕次归属不得迁移,依鬼律 6)
            if (!WraithActs.ActTwo) {
                return false;
            }
            //进入者本人在岩石层
            if (candidate.Center.Y <= Main.rockLayer * 16.0) {
                return false;
            }
            //已认主者不再触发(低频高印象,依鬼律 14);无刀者照常触发(读规则活下来,依鬼律 2)
            WraithVesselHandle vessel = WraithVessels.ResolveCarried(candidate);
            if (vessel.IsValid && vessel.Store.TryGet(ctx.Definition.Key, out WraithProgressRecord record) && record.PactRenewed) {
                return false;
            }
            //锚完整性复检:应对玩家挖穿据点,失败拔锚(保留事件计数)等下轮重选
            if (!AnchorIntact(ctx.Anchor)) {
                WraithSiteSystem.Unanchor(ctx.Definition.Key);
                return false;
            }
            return true;
        }

        /// <summary>锚点 3 瓦半径内仍有 ≥3 块石质实体瓦</summary>
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

        /// <summary>
        /// 破壁拍投锁（潜壁 t=180，实体权威端调用）：世界无锁且锚点 2000px 内无人随身持有时，
        /// 自锚点沿破口法线上抛——它藏在灰里，随石屑一起翻出
        /// </summary>
        internal static void ThrowLockOnEmerge(GhostHandActor hand) {
            if (VaultUtils.isClient || AnyLockInEconomy(hand.Center, LockDenyCarrierRange)) {
                return;
            }
            //破口法线方向 3px/f 上抛(§3.4 原文):与手体伸出、石屑弹射同一朝向
            SpawnLock(hand.Center, new Vector2(hand.ResolveEmergeDir() * 3f, -3f));
        }

        /// <summary>世界任意处已有掉落的锁</summary>
        private static bool AnyWorldLock() {
            int lockType = ModContent.ItemType<CharredLock>();
            foreach (Item item in Main.ActiveItems) {
                if (item.type == lockType && item.stack > 0) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>世界已有锁，或参考点范围内有玩家随身持锁（锁不可囤）</summary>
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

        /// <summary>
        /// 反噬期回据点取锁（1Hz）：挣脱体在场、原主贴近锚点、身上与世界均无锁 → 灰堆出锁。
        /// 把玩家引回案发地（Rumor5 预学），重收伏的叙事闭环
        /// </summary>
        public override void PostUpdateEverything() {
            if (VaultUtils.isClient || Main.gameMenu || ++resupplyTimer < 60) {
                return;
            }
            resupplyTimer = 0;

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
            //补给门:原主背包无锁且世界无锁(§3.4 第二行的字面条件)
            if (GhostHandActor.HasCharredLock(owner) || AnyWorldLock()) {
                return;
            }

            SpawnLock(record.Anchor, new Vector2(0f, -2f));
            if (VaultUtils.isServer) {
                //浮字与音效是本端演出,远端原主给聊天行代偿;键传输,受端按各自语言自译
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
