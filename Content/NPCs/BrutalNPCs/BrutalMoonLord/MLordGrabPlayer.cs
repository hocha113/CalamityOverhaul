using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord
{
    /// <summary>
    /// 掌中处刑被抓端：玩家位置是客户端权威，钉握/锁控/甩出全部只在
    /// 被抓玩家自己的客户端施加（读同步来的核心投技槽，形状同原版月总舌头）。
    /// 兼含 1 血兜底（满血玩家不会被一套投技处死）与释放无敌
    /// </summary>
    internal class MLordGrabPlayer : ModPlayer
    {
        /// <summary>钉握中的核心 whoAmI，未被抓 -1（实例字段：每玩家一份）</summary>
        private int pinCore = -1;
        /// <summary>释放后的免摔伤余帧</summary>
        private int noFallTicks;
        /// <summary>甩出用：最后一帧的手速（松手瞬间继承成抛物）</summary>
        private Vector2 lastHandVelocity;

        private bool Pinned => pinCore >= 0;

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            bool wasPinned = Pinned;
            NPC hand = ResolveGrab(out NPC core);

            if (hand != null) {
                if (!wasPinned) {
                    OnPinStart(hand);
                }
                pinCore = core.whoAmI;
                ApplyPin(core, hand);
            }
            else if (wasPinned) {
                OnRelease();
            }

            //释放后的免摔伤窗（甩落不该转化为摔死）
            if (!Pinned && noFallTicks > 0) {
                noFallTicks--;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }
        }

        /// <summary>被抓期间锁常规输入（移动/跳跃/用物/交互/钩爪/坐骑；快捷药水保留，与原版舌头口径一致）</summary>
        public override void SetControls() {
            if (Player.whoAmI != Main.myPlayer || !Pinned) {
                return;
            }
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlThrow = false;
        }

        /// <summary>被抓期间任何伤害都留 1 血：投技连段永不处死满血玩家的最终兜底</summary>
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!Pinned) {
                return;
            }
            modifiers.SetMaxDamage(Math.Max(Player.statLife - 1, 1));
        }

        /// <summary>死亡即断投：清本地钉握并停运镜（PostUpdate 死亡期不再执行）</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer || !Pinned) {
                return;
            }
            ClearPinState();
            StopGrabClip();
        }

        #region 钉握解算

        /// <summary>解出正抓着本地玩家的抓握手；无则 null</summary>
        private NPC ResolveGrab(out NPC grabCore) {
            grabCore = null;
            if (!CWRWorld.HasBoss) {//世上无 Boss 时不必扫表
                return null;
            }
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.MoonLordCore) {
                    continue;
                }
                //槽位复用等异常取不到覆写时视作无投技（精确索引缺键会抛出）
                if (!npc.TryGetOverride(out MoonLordCoreAI coreAI)) {
                    continue;
                }
                if (MLordFacts.GetCoreState(npc) != MLordStateIndex.PalmExecution) {
                    continue;
                }
                if ((int)coreAI.ai[MLordAiSlots.OvGrabTarget] - 1 != Player.whoAmI) {
                    continue;
                }
                int handIndex = (int)coreAI.ai[MLordAiSlots.OvGrabHand] - 1;
                if (handIndex < 0 || handIndex >= Main.maxNPCs) {
                    continue;
                }
                NPC hand = Main.npc[handIndex];
                if (!hand.active || hand.type != NPCID.MoonLordHand) {
                    continue;
                }
                grabCore = npc;
                return hand;
            }
            return null;
        }

        /// <summary>钉握起手一次性处理：清钩爪、下坐骑、攥握冲击</summary>
        private void OnPinStart(NPC hand) {
            Player.RemoveAllGrapplingHooks();
            if (Player.mount != null && Player.mount.Active) {
                Player.mount.Dismount(Player);
            }
            MLordScreenFX.Punch(hand.Center, 7f, 12);
        }

        /// <summary>每帧钉在掌心：位置硬设 + 清速度 + 免摔伤 + 面向头颅；运镜仅本端</summary>
        private void ApplyPin(NPC core, NPC hand) {
            Player.Center = hand.Center + new Vector2(0f, 8f);
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.ChangeDir(core.Center.X >= Player.Center.X ? 1 : -1);
            lastHandVelocity = hand.velocity;

            //飞行中的钩爪逐帧掐掉（原版舌头同口径），防附着后与钉握抢位
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == Main.myPlayer && proj.aiStyle == 7) {
                    proj.Kill();
                }
            }

            //被抓者本端运镜（旁观者不接管相机）
            if (CutsceneDirector.CurrentClip is not MLordGrabCutscene) {
                CutsceneDirector.Play<MLordGrabCutscene, NPC>(core, restartSameClip: false);
            }
        }

        /// <summary>松手：继承手速甩出 + 足额无敌 + 免摔伤窗 + 停运镜</summary>
        private void OnRelease() {
            ClearPinState();
            if (!Player.dead) {
                Vector2 fling = lastHandVelocity;
                if (fling.Length() < 8f) {
                    //手速缺失（异常中断）：制造一记温和的下抛
                    fling = new Vector2(Player.direction * -0.5f, 1f).SafeNormalize(Vector2.UnitY) * 22f;
                }
                Player.velocity = fling * 1.05f;
                Player.SetImmuneTimeForAllTypes(90);
                noFallTicks = 45;
            }
            StopGrabClip();
        }

        private void ClearPinState() {
            pinCore = -1;
        }

        private static void StopGrabClip() {
            if (CutsceneDirector.CurrentClip is MLordGrabCutscene) {
                CutsceneDirector.Stop();
            }
        }

        #endregion
    }
}
