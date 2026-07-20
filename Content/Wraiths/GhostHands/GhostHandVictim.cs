using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Actors;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 被攥玩家的本端执行层（同 KillMe 必须本端的既有精神）：读同步攥握态清移动操控
    /// （<b>物品使用保留</b>——切火把自救的通道），每帧把自身速度写成拖拽意图，
    /// 正常吃物块碰撞（地形可暂时卡住=天然挣扎感）。攥握态消失的当帧本地撤演出镜像。
    /// 免抓计时供权威端的再抓判定读取。全字段实例级（依鬼律 15）
    /// </summary>
    internal sealed class GhostHandVictim : ModPlayer
    {
        //90t 免抓:松手路径由权威端写入,本类各端各自跑表
        private int gripImmunity;
        //本端演出镜像撤拍的翻转检测
        private bool wasGripped;
        //持有者本端扣锁的去重戳(消耗信号=观测到 Clutch 相位)
        private int consumedLockActor = -1;
        private ushort consumedLockGeneration;

        /// <summary>免抓期内（权威端的再抓闸门）</summary>
        public bool GripImmune => gripImmunity > 0;

        /// <summary>松手时权威端授予免抓（取更长的一段）</summary>
        public void GrantGripImmunity(int ticks) => gripImmunity = Math.Max(gripImmunity, ticks);

        /// <summary>
        /// 正攥着本玩家的手，无则 null。每 tick 热路径：活跃数 O(1) 先剪 + 槽位数组直扫，
        /// 零分配（镜像 <c>WraithDirector.EncounterInProgress</c> 写法）
        /// </summary>
        private GhostHandActor FindGripper() {
            if (ActorLoader.GetActiveActorCount() <= 0) {
                return null;
            }
            Actor[] actors = ActorLoader.Actors;
            for (int i = 0; i < actors.Length; i++) {
                if (actors[i] is GhostHandActor { Active: true } hand
                    && hand.IsGripping && hand.VictimWho == Player.whoAmI) {
                    return hand;
                }
            }
            return null;
        }

        public override void SetControls() {
            //防御门:现状 TML 只对本地玩家调本钩子,防上游行为变化
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (FindGripper() == null) {
                return;
            }
            //清移动/跳跃/钩爪/坐骑;物品使用与快捷栏切换保留(自救通道)
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        public override void PreUpdateMovement() {
            //速度改写只归受害者本端(远端镜像吃实体同步,服务器不模拟玩家移动)
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            GhostHandActor gripper = FindGripper();
            if (gripper == null) {
                return;
            }
            Player.RemoveAllGrapplingHooks();
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            //钉住(队友凝视)时意图为零:悬在原地,心跳继续
            Player.velocity = gripper.DragIntent;
        }

        public override void PostUpdate() {
            if (gripImmunity > 0) {
                gripImmunity--;
            }
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }

            //攥握态清除的当帧本地撤演出(权威撤拍包在途时也不多跳半秒心跳)
            bool gripped = FindGripper() != null;
            if (wasGripped && !gripped) {
                Player.GetModPlayer<WraithPlayer>().ClearOmenMirror();
            }
            wasGripped = gripped;

            WatchHolderConsume();
        }

        /// <summary>
        /// 持有者本端扣锁：背包客户端权威，服务器不越权改写。它扑到"手持锁的我"并进入
        /// 蜷缩（Clutch）即消耗信号——观测到就扣一枚，(实体, 代) 戳去重防重复扣。
        /// 每 tick 热路径，同 <see cref="FindGripper"/> 的零分配纪律
        /// </summary>
        private void WatchHolderConsume() {
            if (ActorLoader.GetActiveActorCount() <= 0) {
                return;
            }
            Actor[] actors = ActorLoader.Actors;
            for (int i = 0; i < actors.Length; i++) {
                if (actors[i] is not GhostHandActor { Active: true } hand
                    || hand.Phase != GhostHandPhase.Clutch || hand.CovetHolderWho != Player.whoAmI) {
                    continue;
                }
                if (consumedLockActor == hand.WhoAmI && consumedLockGeneration == hand.Generation) {
                    continue;
                }
                consumedLockActor = hand.WhoAmI;
                consumedLockGeneration = hand.Generation;
                ConsumeOneLock();
                return;
            }
        }

        private void ConsumeOneLock() {
            int lockType = Terraria.ModLoader.ModContent.ItemType<CharredLock>();
            //手中优先(递出的那枚),背包兜底
            Item held = Player.HeldItem;
            if (held != null && !held.IsAir && held.type == lockType) {
                held.stack--;
                if (held.stack <= 0) {
                    held.TurnToAir();
                }
                return;
            }
            foreach (Item item in Player.inventory) {
                if (item != null && !item.IsAir && item.type == lockType) {
                    item.stack--;
                    if (item.stack <= 0) {
                        item.TurnToAir();
                    }
                    return;
                }
            }
        }

        public override void OnEnterWorld() {
            gripImmunity = 0;
            wasGripped = false;
            consumedLockActor = -1;
        }
    }
}
