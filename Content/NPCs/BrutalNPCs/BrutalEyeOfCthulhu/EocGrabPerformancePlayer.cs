using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu.States;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.Cinematics;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEyeOfCthulhu
{
    /// <summary>
    /// 撕咬拖曳被抓者本机侧：钉身/锁控/运镜/分段结算全部由同步来的 NPC 抓取态
    /// （ai[2]=MawDrag 且 ai[3]=±(本机 whoAmI+1)）推导，玩家位置为客户端权威，
    /// 服务器不写玩家位置；旁观者只看到同步的 NPC 动作与被抓者位置包
    /// </summary>
    internal class EocGrabPerformancePlayer : ModPlayer
    {
        /// <summary>正抓着本机的克眼下标，-1=未被抓（每玩家实例字段，仅本机实例有意义）</summary>
        private int grabbingEye = -1;
        /// <summary>被抓本地帧计时，分段结算按它推进</summary>
        private int grabTimer;
        /// <summary>拖行方向符号，随 ai[3] 符号缓存（释放瞬间 ai[3] 已清零取不到）</summary>
        private float dragSign = 1f;
        private bool bite1Done;
        private bool bite2Done;
        private bool releaseDone;
        /// <summary>释放后运镜再吊几帧陨坑镜头</summary>
        private int lingerTimer;
        /// <summary>超时兜底释放后的再抓屏蔽帧，防 ai[3] 迟迟未清导致的抓-放死循环</summary>
        private int regrabBlockTimer;

        internal bool IsGrabbed => grabbingEye >= 0;

        /// <summary>钉身点：口器前端略下沉，读作被咬住贴地推行</summary>
        internal static Vector2 PinPos(NPC eye)
            => EocMawDragState.MawWorldPos(eye) + new Vector2(0f, 10f);

        /// <summary>投技运镜期间的镜头震动（运镜接管相机后普通震屏可能失效）</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not EocMawDragCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
        }

        /// <summary>找正抓着该玩家的克眼；必须确认本模组接管在场，防原版 ai 撞值</summary>
        internal static NPC FindGrabbingEye(int playerIndex) {
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.EyeofCthulhu) {
                    continue;
                }
                if ((int)npc.ai[2] != (int)EocStateIndex.MawDrag) {
                    continue;
                }
                int packed = (int)npc.ai[3];
                if (packed == 0 || Math.Abs(packed) - 1 != playerIndex) {
                    continue;
                }
                if (!npc.TryGetOverride(out Dictionary<Type, NPCOverride> overrides)
                    || !overrides.TryGetValue(typeof(EyeOfCthulhuAI), out NPCOverride eyeOverride)
                    || eyeOverride is not EyeOfCthulhuAI) {
                    continue;
                }
                return npc;
            }
            return null;
        }

        /// <summary>下标处的克眼仍在投技状态则返回之，否则 null（状态被强切/眼已消失）</summary>
        private static NPC ValidateEye(int index) {
            if (index < 0 || index >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[index];
            if (!npc.active || npc.type != NPCID.EyeofCthulhu) {
                return null;
            }
            if ((int)npc.ai[2] != (int)EocStateIndex.MawDrag) {
                return null;
            }
            return npc;
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            if (regrabBlockTimer > 0) {
                regrabBlockTimer--;
            }

            if (!IsGrabbed) {
                NPC candidate = regrabBlockTimer > 0 ? null : FindGrabbingEye(Player.whoAmI);
                if (candidate != null) {
                    BeginGrab(candidate);
                }
            }
            else {
                NPC eye = ValidateEye(grabbingEye);
                if (eye == null) {
                    //状态被死亡/撤离强切：无终结释放
                    EndGrab(withSlam: false, eye: null);
                }
                else {
                    int packed = (int)eye.ai[3];
                    if (packed == 0) {
                        //砸地释放信号
                        EndGrab(withSlam: true, eye);
                    }
                    else if (Math.Abs(packed) - 1 != Player.whoAmI) {
                        EndGrab(withSlam: false, eye: null);
                    }
                    else if (TimeFreezeSystem.IsFrozen(eye)) {
                        //时停冻住了眼球：钉身与锁控保持，计时/结算/超时全部挂起
                        EnsureCutscene(eye);
                    }
                    else {
                        dragSign = Math.Sign(packed);
                        grabTimer++;
                        EnsureCutscene(eye);
                        RunBeats(eye);
                        //保底：包丢尽也要在本地按时放人，并屏蔽一段再抓
                        if (grabTimer > 240) {
                            regrabBlockTimer = 120;
                            EndGrab(withSlam: true, eye);
                        }
                    }
                }
            }

            TickLinger();
        }

        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            //被抓期间死于外源（岩浆/仆从等）：立即断投停运镜
            if (IsGrabbed) {
                EndGrab(withSlam: false, eye: null);
            }
            TickLinger();
        }

        /// <summary>被抓期间的兜底保护与物理连续性，每帧重申</summary>
        public override void PreUpdateMovement() {
            if (Player.whoAmI != Main.myPlayer || !IsGrabbed || Player.dead) {
                return;
            }
            NPC eye = ValidateEye(grabbingEye);
            if (eye == null) {
                return;
            }
            Player.noItems = true;
            Player.noBuilding = true;
            Player.noKnockback = true;
            //毯式免疫：拖行途中不吃仆从/环境的杂伤，分段结算自行开洞
            Player.immune = true;
            if (Player.immuneTime < 2) {
                Player.immuneTime = 2;
            }
            Player.immuneNoBlink = true;
            Player.velocity = eye.velocity;
            Player.fallStart = (int)(Player.position.Y / 16f);
        }

        /// <summary>被抓期间锁死本机操控输入</summary>
        public override void SetControls() {
            if (Player.whoAmI != Main.myPlayer || !IsGrabbed) {
                return;
            }
            Player.controlLeft = Player.controlRight = false;
            Player.controlUp = Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = Player.controlUseTile = false;
            Player.controlHook = Player.controlMount = false;
            Player.controlThrow = false;
        }

        /// <summary>NPC 更新后把人钉到口器上，帧内零滞后（由 <see cref="EocGrabPinSystem"/> 驱动）</summary>
        internal void ApplyPin() {
            if (Player.whoAmI != Main.myPlayer || !IsGrabbed || Player.dead) {
                return;
            }
            NPC eye = ValidateEye(grabbingEye);
            if (eye == null) {
                return;
            }
            Player.Center = PinPos(eye);
            Player.velocity = eye.velocity;
            Player.fallStart = (int)(Player.position.Y / 16f);
            //面朝来向，读作被拖着走
            Player.direction = dragSign >= 0f ? -1 : 1;
        }

        private void BeginGrab(NPC eye) {
            grabbingEye = eye.whoAmI;
            grabTimer = 0;
            bite1Done = bite2Done = releaseDone = false;
            dragSign = Math.Sign((int)eye.ai[3]);
            if (dragSign == 0f) {
                dragSign = 1f;
            }
            //斩断一切位移挂点
            Player.RemoveAllGrapplingHooks();
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.pulley = false;
            //咬合瞬间的个人顿帧反馈
            EnsureCutscene(eye);
            EocScreenFX.PushFlash(0.45f, 9);
            EocScreenFX.PushPulse(0.7f);
            RequestShake(7f, 10);
        }

        /// <summary>分段结算：咬合→研磨，按本地计时走拍</summary>
        private void RunBeats(NPC eye) {
            if (!bite1Done && grabTimer >= 8) {
                bite1Done = true;
                Bite(eye, 0.6f);
                RequestShake(6f, 8);
                EocScreenFX.PushPulse(0.6f);
            }
            if (!bite2Done && grabTimer >= 78) {
                bite2Done = true;
                Bite(eye, 0.5f);
                RequestShake(5f, 8);
                EocScreenFX.PushPulse(0.5f);
            }
        }

        /// <summary>释放：withSlam=true 补终结一击（砸地），false=异常断投只放人</summary>
        private void EndGrab(bool withSlam, NPC eye) {
            if (!releaseDone) {
                releaseDone = true;
                if (withSlam && eye != null && !Player.dead && Player.Distance(eye.Center) < 500f) {
                    Bite(eye, 1.15f);
                    RequestShake(10f, 14);
                    EocScreenFX.PushFlash(0.5f, 10);
                }
            }
            grabbingEye = -1;
            //释放体势：小幅弹开 + 足额无敌帧 + 消摔伤
            if (!Player.dead) {
                Player.velocity = new Vector2(dragSign * 4.5f, -6.5f);
            }
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.immune = true;
            Player.immuneTime = 90;
            Player.SetImmuneTimeForAllTypes(90);
            Player.immuneNoBlink = false;
            lingerTimer = 20;
        }

        /// <summary>
        /// 脚本咬合结算：HurtInfo 直伤（不过防御与闪避，数值即设计值），
        /// 非致死钳制，投技任何一段都不处死玩家，至多打到 1 血
        /// </summary>
        private void Bite(NPC eye, float fraction) {
            if (Player.dead || Player.creativeGodMode) {
                return;
            }
            int raw = Math.Max((int)(eye.defDamage * fraction), 8);
            int final = Math.Min(raw, Math.Max(Player.statLife - 1, 0));
            if (final <= 0) {
                return;
            }
            Player.immune = false;
            Player.immuneTime = 0;
            Player.HurtInfo info = new() {
                DamageSource = PlayerDeathReason.ByNPC(eye.whoAmI),
                SourceDamage = final,
                Damage = final,
                HitDirection = dragSign >= 0f ? 1 : -1,
                Knockback = 0f,
                Dodgeable = false,
                PvP = false,
                CooldownCounter = -1,
            };
            Player.Hurt(info);
        }

        private void EnsureCutscene(NPC eye) {
            if (CutsceneDirector.CurrentClip is not EocMawDragCutscene) {
                CutsceneDirector.Play<EocMawDragCutscene, NPC>(eye, restartSameClip: false);
            }
        }

        /// <summary>释放后吊镜与运镜回收：未被抓时若本片还在放，倒数后平滑停</summary>
        private void TickLinger() {
            if (IsGrabbed) {
                return;
            }
            if (CutsceneDirector.CurrentClip is EocMawDragCutscene) {
                if (lingerTimer > 0) {
                    lingerTimer--;
                }
                else {
                    CutsceneDirector.Stop();
                }
            }
        }
    }

    /// <summary>NPC 更新结束后执行钉身，保证被抓者与口器帧内同步</summary>
    internal sealed class EocGrabPinSystem : ModSystem
    {
        public override void PostUpdateNPCs() {
            if (Main.dedServ || Main.gameMenu) {
                return;
            }
            Player lp = Main.LocalPlayer;
            if (lp == null || !lp.active || lp.dead) {
                return;
            }
            lp.GetModPlayer<EocGrabPerformancePlayer>().ApplyPin();
        }
    }
}
