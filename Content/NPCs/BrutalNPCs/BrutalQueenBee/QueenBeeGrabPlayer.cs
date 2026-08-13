using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.States;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee
{
    /// <summary>
    /// 投技·蜜牢收网玩家侧：只在被抓玩家自己的客户端生效——<br/>
    /// 从女王同步状态推导钉位/运镜/受伤拍/释放，服务器与旁观端不写本地玩家任何东西<br/>
    /// (玩家位置客户端权威，月总舌头形状；伤害在受害者端走常规 Hurt)
    /// </summary>
    internal class QueenBeeGrabPlayer : ModPlayer
    {
        //正钉住我的女王whoAmI，-1无(本客户端自用)
        private int grabbingQueen = -1;
        //已结算过的最高命中拍，严格向前防重播/回绕
        private int lastHurtBeat = -1;
        //连段总伤预算余量(满血玩家不可能被一套处死的硬上限)
        private int damageBudget;
        //释放后落伤保护窗
        private int recoverTicks;

        /// <summary>投技运镜期间的震屏(运镜接管相机后普通震屏可能失效)</summary>
        internal static void RequestGrabShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRServerConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not QueenBeeGrabCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            (NPC queen, QBSwarmLiftState lift) = FindLiftHoldingMe();

            if (lift != null) {
                if (grabbingQueen < 0) {
                    OnHoldStart(lift);
                }
                grabbingQueen = queen.whoAmI;
                ApplyPin(queen, lift);
            }
            else if (grabbingQueen >= 0) {
                OnRelease();
            }

            UpdateRecover();
        }

        /// <summary>死亡帧不走PostUpdate：在这里兜底解除钉位与运镜</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            recoverTicks = 0;
            if (grabbingQueen >= 0) {
                grabbingQueen = -1;
                if (CutsceneDirector.CurrentClip is QueenBeeGrabCutscene) {
                    CutsceneDirector.Stop();
                }
            }
        }

        /// <summary>找当前正抓着本地玩家的女王与其投技状态，无则(null,null)</summary>
        private (NPC, QBSwarmLiftState) FindLiftHoldingMe() {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.QueenBee) {
                    continue;
                }
                if (!npc.TryGetOverride(out BrutalQueenBeeAI queenAI)) {
                    continue;
                }
                if (queenAI.Machine?.CurrentState is not QBSwarmLiftState lift) {
                    continue;
                }
                if (lift.HoldActive && lift.VictimWho == Player.whoAmI) {
                    return (npc, lift);
                }
            }
            return (null, null);
        }

        /// <summary>被裹进茧的第一帧：断钩爪下坐骑、置预算、命中拍快进对齐(补入场迟到)</summary>
        private void OnHoldStart(QBSwarmLiftState lift) {
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.RemoveAllGrapplingHooks();

            damageBudget = (int)(Player.statLifeMax2 * QBSwarmLiftState.TotalDamageBudgetScale);
            recoverTicks = 0;

            //迟到入场(联机延迟)不回放已过的命中拍
            lastHurtBeat = -1;
            foreach (int beat in QBSwarmLiftState.HurtBeats) {
                if (lift.Timer >= beat) {
                    lastHurtBeat = beat;
                }
            }
        }

        /// <summary>每帧钉位：位置钉茧心、清速度、防落伤、启运镜、结算命中拍</summary>
        private void ApplyPin(NPC queen, QBSwarmLiftState lift) {
            Player.Center = lift.CocoonCenter;
            Player.velocity = Vector2.Zero;
            Player.fallStart = Player.fallStart2 = (int)(Player.position.Y / 16f);

            //运镜只此客户端播放；高优先级clip(如死亡演出)在播时让位，之后逐帧重试
            if (CutsceneDirector.CurrentClip is not QueenBeeGrabCutscene) {
                CutsceneDirector.Play<QueenBeeGrabCutscene, NPC>(queen, restartSameClip: false);
            }

            //命中拍：与本端状态Timer精确对齐，视觉与受伤同帧
            foreach (int beat in QBSwarmLiftState.HurtBeats) {
                if (lift.Timer < beat || beat <= lastHurtBeat) {
                    continue;
                }
                lastHurtBeat = beat;
                RequestGrabShake(6f, 12);
                int dmg = Math.Min((int)(queen.defDamage * QBSwarmLiftState.PassDamageScale), damageBudget);
                if (dmg > 0 && !Player.immune) {
                    Player.Hurt(PlayerDeathReason.ByNPC(queen.whoAmI), dmg, 0);
                    damageBudget -= dmg;
                }
            }
        }

        /// <summary>释放沿：爆散→终结击(致死减免留命)+下抛；断投→温和释放。均给足额无敌与落伤保护</summary>
        private void OnRelease() {
            NPC queen = grabbingQueen >= 0 && grabbingQueen < Main.maxNPCs ? Main.npc[grabbingQueen] : null;
            QBSwarmLiftState lift = null;
            if (queen != null && queen.active && queen.type == NPCID.QueenBee
                && queen.TryGetOverride(out BrutalQueenBeeAI queenAI)) {
                lift = queenAI.Machine?.CurrentState as QBSwarmLiftState;
            }
            bool detonated = lift != null && lift.DetonationReached;

            if (detonated) {
                int dmg = Math.Min((int)(queen.defDamage * QBSwarmLiftState.FinaleDamageScale), damageBudget);
                //零防御保守界：终结击若会致死则减免留命(防御只会让实收更低)
                if (dmg >= Player.statLife - 25) {
                    dmg = Player.statLife - 25;
                }
                if (dmg > 0 && !Player.immune) {
                    Player.Hurt(PlayerDeathReason.ByNPC(queen.whoAmI), dmg, 0);
                }
                //向女王反侧下抛
                int side = Player.Center.X >= queen.Center.X ? 1 : -1;
                Player.velocity = new Vector2(side * 4.5f, 9.5f);
                RequestGrabShake(9f, 16);
            }
            else {
                //断投(死亡/逃逸/女王倒下)：原地温和释放
                Player.velocity = Vector2.Zero;
            }

            //足额释放无敌+落伤保护窗
            Player.immune = true;
            Player.immuneTime = 90;
            Player.SetImmuneTimeForAllTypes(90);
            recoverTicks = 100;
            grabbingQueen = -1;

            if (CutsceneDirector.CurrentClip is QueenBeeGrabCutscene) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>释放后的坠落保护：持续重置落点直到触地或窗口耗尽(高空抛落不叠加隐藏摔伤)</summary>
        private void UpdateRecover() {
            if (recoverTicks <= 0) {
                return;
            }
            recoverTicks--;
            Player.fallStart = Player.fallStart2 = (int)(Player.position.Y / 16f);
            //释放首帧速度刚被清零，不算落地：留出起坠余量后再做触地提前收窗
            if (recoverTicks < 92 && Player.velocity.Y == 0f) {
                recoverTicks = 0;
            }
        }
    }
}
