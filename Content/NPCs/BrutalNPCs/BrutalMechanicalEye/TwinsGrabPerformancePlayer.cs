using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye.States.Common;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMechanicalEye
{
    /// <summary>
    /// 钳形投技被抓玩家本地表演层：玩家位置为客户端权威，
    /// 位移钉定/输入锁/弹射与运镜启停只在被抓玩家自己的客户端施加，
    /// 依据为同步来的双眼投技槽(<see cref="TwinsPincerGrabState.TryGetGrabData"/>)
    /// </summary>
    internal class TwinsGrabPerformancePlayer : ModPlayer
    {
        //本帧是否处于被夹节拍(Clamp~EjectCharge)
        private bool pinned;
        //抓取全程接入标记(含弹射拍)
        private bool engaged;
        private bool flungApplied;
        private Vector2 pinAnchor;
        private float pinLineAngle;
        //释放后的摔伤保护余量
        private int fallGuardTicks;

        /// <summary>找到正夹着本地玩家的眼(优先魔焰)，返回其节拍数据</summary>
        private NPC FindEyeGrabbingMe(out int beat, out float lineAngle) {
            beat = TwinsPincerGrabState.BeatNone;
            lineAngle = 0f;
            NPC found = null;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.type != NPCID.Spazmatism && npc.type != NPCID.Retinazer) {
                    continue;
                }
                if (!TwinsPincerGrabState.TryGetGrabData(npc, out int grabbed, out int eyeBeat, out float eyeAngle)) {
                    continue;
                }
                if (grabbed != Player.whoAmI) {
                    continue;
                }
                if (eyeBeat < TwinsPincerGrabState.BeatClamp || eyeBeat > TwinsPincerGrabState.BeatEject) {
                    continue;
                }
                if (found == null || npc.type == NPCID.Spazmatism) {
                    found = npc;
                    beat = eyeBeat;
                    lineAngle = eyeAngle;
                    if (npc.type == NPCID.Spazmatism) {
                        break;
                    }
                }
            }
            return found;
        }

        public override void PostUpdate() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }

            //释放后短暂摔伤保护
            if (fallGuardTicks > 0) {
                fallGuardTicks--;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }

            NPC eye = FindEyeGrabbingMe(out int beat, out float lineAngle);
            bool playing = CutsceneDirector.CurrentClip is TwinsPincerCutscene;

            if (eye != null) {
                if (!engaged) {
                    Engage(eye, beat, lineAngle);
                }
                pinLineAngle = lineAngle;
                UpdatePinAnchor(eye, beat, lineAngle);

                if (beat >= TwinsPincerGrabState.BeatClamp && beat <= TwinsPincerGrabState.BeatEjectCharge) {
                    pinned = true;
                    ApplyPin();
                }
                else if (beat == TwinsPincerGrabState.BeatEject) {
                    pinned = false;
                    if (!flungApplied) {
                        ApplyFling();
                    }
                }

                //运镜仅被抓者本地播放
                if (!playing) {
                    CutsceneDirector.Play<TwinsPincerCutscene, NPC>(eye, restartSameClip: false);
                }
            }
            else {
                if (engaged) {
                    Release();
                }
                if (playing) {
                    CutsceneDirector.Stop();
                }
            }
        }

        /// <summary>接入抓取：斩断位移类挂点，锚定交扣点</summary>
        private void Engage(NPC eye, int beat, float lineAngle) {
            engaged = true;
            pinned = true;
            flungApplied = false;
            pinLineAngle = lineAngle;
            pinAnchor = EstimateClampPoint(eye, beat, lineAngle);

            Player.RemoveAllGrapplingHooks();
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.pulley = false;
        }

        /// <summary>逐帧从同步的眼位反推交扣点并低通平滑，吸收呼吸浮动与包时序抖动</summary>
        private void UpdatePinAnchor(NPC eye, int beat, float lineAngle) {
            Vector2 estimate = EstimateClampPoint(eye, beat, lineAngle);
            pinAnchor = Vector2.Lerp(pinAnchor, estimate, 0.2f);
        }

        /// <summary>
        /// 由眼的同步位置反推钳口交点：夹合拍双眼停位 ±74，
        /// 束缚起改用持绳激光眼停位(交点+轴向150)反推，魔焰绕环不可反推；
        /// 反推不出且无有效旧锚(为零向量)时退回玩家自身位置，绝不钉向世界原点
        /// </summary>
        private Vector2 EstimateClampPoint(NPC eye, int beat, float lineAngle) {
            Vector2 dir = lineAngle.ToRotationVector2();
            bool eyeIsSpaz = eye.type == NPCID.Spazmatism;
            if (beat <= TwinsPincerGrabState.BeatClamp) {
                return eyeIsSpaz ? eye.Center + dir * 74f : eye.Center - dir * 74f;
            }
            NPC retin = eyeIsSpaz ? TwinsStateContext.GetPartnerNpc(eye.type) : eye;
            if (retin != null && retin.active) {
                return retin.Center - dir * 150f;
            }
            return pinAnchor == Vector2.Zero ? Player.Center : pinAnchor;
        }

        /// <summary>逐帧钉定：位置回锚点带轻微挣扎摆动，速度清零，防摔防漂</summary>
        private void ApplyPin() {
            float t = Main.GlobalTimeWrappedHourly;
            Vector2 sway = new((float)Math.Sin(t * 11f) * 2.2f, (float)Math.Sin(t * 8.4f + 1.7f) * 1.8f);
            Player.Center = Vector2.Lerp(Player.Center, pinAnchor + sway, 0.55f);
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
        }

        /// <summary>弹射：沿钳形轴线的垂向朝下甩出，给足无敌帧与摔伤保护</summary>
        private void ApplyFling() {
            flungApplied = true;
            Vector2 perp = (pinLineAngle + MathHelper.PiOver2).ToRotationVector2();
            if (perp.Y < 0f) {
                perp = -perp;
            }
            Player.velocity = perp * 21f;
            Player.SetImmuneTimeForAllTypes(90);
            fallGuardTicks = 70;
        }

        /// <summary>脱离抓取(自然结束或任何异常断投)，兜底补无敌与摔伤保护</summary>
        private void Release() {
            engaged = false;
            pinned = false;
            if (!flungApplied) {
                //异常断投没走弹射，也要给保护
                Player.SetImmuneTimeForAllTypes(60);
                fallGuardTicks = 50;
            }
            flungApplied = false;
        }

        //本地 control 在 CopyInto 里重写，须在 SetControls 清
        public override void SetControls() {
            if (!pinned || Player.whoAmI != Main.myPlayer) {
                return;
            }
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlDown = false;
            Player.controlJump = false;
            Player.controlHook = false;
            Player.controlMount = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlThrow = false;
            Player.controlSmart = false;
            Player.controlTorch = false;
        }

        public override void PreUpdate() {
            if (!pinned || Player.whoAmI != Main.myPlayer) {
                return;
            }
            //移动结算前先压一次，PostUpdate 再兜底
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
        }

        /// <summary>死亡兜底：PostUpdate 停跑，这里断开钉定并停运镜</summary>
        public override void UpdateDead() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            pinned = false;
            engaged = false;
            flungApplied = false;
            fallGuardTicks = 0;
            if (CutsceneDirector.CurrentClip is TwinsPincerCutscene) {
                CutsceneDirector.Stop();
            }
        }
    }
}
