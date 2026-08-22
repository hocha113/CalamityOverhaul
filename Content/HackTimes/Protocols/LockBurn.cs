using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 锁芯烧穿：无钥匙解锁上锁容器，随机烧毁其中一件。<br/>
    /// 箱子是世界实体，权威端可以直接写 <c>Main.chest[].item[]</c>
    /// 这与玩家背包（服务端写不进，见 tml-netcode-pitfalls §6.2）是两回事。
    /// 解锁走 <see cref="Chest.Unlock"/> + <see cref="MessageID.Unlock"/> 广播
    /// （镜像原版 MessageBuffer case 52 的服务端转发），烧毁槽位走
    /// <see cref="MessageID.SyncChestItem"/> 单槽同步
    /// </summary>
    internal class LockBurn : QuickHackDef
    {
        private static readonly Color Scorch = new(255, 150, 60);
        private static readonly Color ScorchDark = new(180, 70, 30);

        public override void SetDefaults() {
            //怪物级定价：必须比"去打钥匙"更贵，只在"现在就要开"时才划算
            UploadTime = 240;
            RamCost = 8;
            Category = QuickHackCategory.TileManip;
            SupportedTargets = HackTargetKind.Container;
            UnlockedByDefault = false;
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (target is not ContainerScannable c) return false;
            //必须上锁，且锁型在可烧穿名单里，名单镜像 Chest.Unlock 的
            //接受集，避免"付了 8 RAM 却开不了"的静默失败
            if (!Chest.IsLocked(c.AnchorX, c.AnchorY)) return false;
            return CanBurnOpen(c.AnchorX, c.AnchorY);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not ContainerScannable c) return false;
            int x = c.AnchorX;
            int y = c.AnchorY;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (!Chest.Unlock(x, y)) return false;
                if (Main.netMode == NetmodeID.Server) {
                    //镜像 case 52 的服务端转发：先让各客户端本地跑一遍
                    //Chest.Unlock（音效与尘埃就地播），再补一发 tile 快照兜底。
                    //52 号在 MessageID 里叫 LockAndUnlock；number2=1 是箱锁
                    NetMessage.TrySendData(MessageID.LockAndUnlock, -1, -1, null,
                        0, 1f, x, y);
                    NetMessage.SendTileSquare(-1, x, y, 2);
                }
                BurnRandomSlot(c);
            }

            if (Main.netMode != NetmodeID.Server) EmitBurnCue(c.WorldCenter);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            //解锁的音效尘埃由中继的 Chest.Unlock 播，这里补焦痕层
            EmitBurnCue(target.WorldCenter);
        }

        //烧掉一件：权威端一次性掷点（离散结果、权威端独占，见 pitfalls §9.1），
        //改完用 SyncChestItem 把该槽推给所有客户端
        private static void BurnRandomSlot(ContainerScannable c) {
            int chestIndex = c.ResolveChestIndex();
            if (chestIndex < 0) return;
            Chest chest = Main.chest[chestIndex];
            if (chest?.item == null) return;

            int occupied = 0;
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item != null && !item.IsAir) occupied++;
            }
            //空箱只白得一次解锁，没有可烧的就跳过（生成箱不会空，防御分支）
            if (occupied == 0) return;

            int pick = Main.rand.Next(occupied);
            for (int i = 0; i < chest.item.Length; i++) {
                Item item = chest.item[i];
                if (item == null || item.IsAir) continue;
                if (pick-- > 0) continue;

                item.TurnToAir();
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.SyncChestItem, -1, -1, null,
                        chestIndex, i);
                }
                return;
            }
        }

        /// <summary>
        /// 锁型白名单，逐条镜像 <see cref="Chest.Unlock"/> 的 switch：
        /// 金箱(2)/暗影箱(4)/1.4 新锁箱(36/38/40) 直接开；
        /// 地牢生态箱(23~27 与 Containers2 的 13) 要求世纪之花已倒；
        /// 模组容器交给 TileLoader 的锁定判定，能不能开由 Unlock 自己说了算
        /// </summary>
        private static bool CanBurnOpen(int x, int y) {
            Tile tile = Framing.GetTileSafely(x, y);
            if (!tile.HasTile) return false;
            if (tile.TileType >= TileID.Count) {
                //模组箱：IsLocked 已在上游确认，这里放行给 Chest.Unlock 的
                //TileLoader.UnlockChest 分支处理
                return true;
            }
            int style = tile.TileFrameX / 36;
            if (tile.TileType == TileID.Containers) {
                if (style == 2 || style == 4
                    || style == 36 || style == 38 || style == 40) {
                    return true;
                }
                if (style >= 23 && style <= 27) return NPC.downedPlantBoss;
                return false;
            }
            if (tile.TileType == TileID.Containers2) {
                return style == 13 && NPC.downedPlantBoss;
            }
            return false;
        }

        //焦痕：锁孔位置一撮暗橙火花坠落 + 一声闷响
        private static void EmitBurnCue(Vector2 center) {
            for (int i = 0; i < 16; i++) {
                Vector2 offset = Main.rand.NextVector2Circular(14f, 12f);
                Vector2 vel = new(Main.rand.NextFloat(-1.4f, 1.4f),
                    Main.rand.NextFloat(-2.6f, -0.4f));
                Color tint = Main.rand.NextBool() ? Scorch : ScorchDark;
                PRTLoader.NewParticle<PRT_Spark>(center + offset, vel, tint, 0.85f)
                    ?.Configure(true, 26);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = -0.4f }, center);
                SoundEngine.PlaySound(CWRSound.Hacker with { Pitch = -0.2f }, center);
            }
        }
    }
}
