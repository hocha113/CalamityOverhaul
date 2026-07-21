using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Actors;
using System;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.GhostHands
{
    /// <summary>
    /// 被攥本端执行。清移动保留物品使用；速度写拖拽意图；字段实例级
    /// </summary>
    internal sealed class GhostHandVictim : ModPlayer
    {
        //90t 免抓
        private int gripImmunity;
        //镜像撤拍翻转检测
        private bool wasGripped;
        //扣锁去重戳
        private int consumedLockActor = -1;
        private ushort consumedLockGeneration;

        /// <summary>免抓期内</summary>
        public bool GripImmune => gripImmunity > 0;

        /// <summary>授予免抓，取更长</summary>
        public void GrantGripImmunity(int ticks) => gripImmunity = Math.Max(gripImmunity, ticks);

        /// <summary>正攥着本玩家的手，无则 null</summary>
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
            //仅本地玩家
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (FindGripper() == null) {
                return;
            }
            //清移动，保留物品使用
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        public override void PreUpdateMovement() {
            //速度改写仅受害者本端
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
            //钉住时意图为零
            Player.velocity = gripper.DragIntent;
        }

        public override void PostUpdate() {
            if (gripImmunity > 0) {
                gripImmunity--;
            }
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }

            //松手当帧撤演出
            bool gripped = FindGripper() != null;
            if (wasGripped && !gripped) {
                Player.GetModPlayer<WraithPlayer>().ClearOmenMirror();
            }
            wasGripped = gripped;

            WatchHolderConsume();
        }

        /// <summary>持有者本端扣锁，Clutch 相位触发，代戳去重</summary>
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
            //手中优先，背包兜底
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
