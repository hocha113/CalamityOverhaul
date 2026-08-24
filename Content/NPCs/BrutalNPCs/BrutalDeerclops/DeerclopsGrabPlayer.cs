using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States;
using InnoVault.Cinematics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops
{
    /// <summary>
    /// 投技玩家侧：玩家位置是客户端权威，被抓者的钉身/锁控/伤害节拍/运镜启停
    /// 全部由其own客户端从同步来的NPC状态推导执行(月总舌头形状)；
    /// 其余客户端只对本地镜像做钉身，服务器绝不写玩家位置
    /// </summary>
    internal class DeerclopsGrabPlayer : ModPlayer
    {
        /// <summary>释放时授予的无敌帧</summary>
        private const int ReleaseImmune = 90;

        //以下全部为"每玩家实例"状态，禁static
        private bool grabActive;
        private bool releaseDone;
        private Vector2 grabStartPos;
        /// <summary>已结算过的最大节拍tick，防同拍重放(7.5)</summary>
        private int lastBeatFired;

        /// <summary>携抓运镜期间的镜头震动(运镜接管相机后普通震屏可能失效)</summary>
        internal static void RequestShake(float intensity, int duration) {
            if (VaultUtils.isServer || !CWRClientConfig.Instance.ScreenVibration) {
                return;
            }
            if (CutsceneDirector.CurrentClip is not DeerclopsGrabCutscene) {
                return;
            }
            CutsceneDirector.Shake(Vector2.Zero, intensity, 0.9f, duration);
        }

        public override void PostUpdate() {
            //服务器不参与：位置权威在客户端(其对被抓者位置的认知来自玩家包)
            if (VaultUtils.isServer) {
                return;
            }

            bool found = DeerclopsEyeGrabState.TryFindGrabbingDeer(Player.whoAmI, out NPC deer, out DeerclopsEyeGrabState grabState);
            if (found) {
                int timer = grabState.Timer;
                if (!grabActive) {
                    grabActive = true;
                    releaseDone = false;
                    lastBeatFired = 0;
                    grabStartPos = Player.Center;
                    OnGrabStart();
                }

                if (timer < DeerclopsEyeGrabState.ReleaseTick) {
                    PinPlayer(deer, timer);
                }
                else if (!releaseDone) {
                    DoRelease(deer);
                }

                if (Player.whoAmI == Main.myPlayer) {
                    DriveBeats(deer, timer);
                    DriveCutscene(deer);
                }
            }
            else if (grabActive) {
                //异常断投(boss死亡/传送逃逸/目标切换等)：同样给足释放待遇
                if (!releaseDone) {
                    DoRelease(null);
                }
                grabActive = false;
                if (Player.whoAmI == Main.myPlayer) {
                    StopCutsceneIfOurs();
                }
            }
        }

        /// <summary>死亡中断：解除一切，不留残锁(死亡时PostUpdate不再执行)</summary>
        public override void UpdateDead() {
            if (!grabActive) {
                return;
            }
            grabActive = false;
            releaseDone = true;
            if (Player.whoAmI == Main.myPlayer) {
                StopCutsceneIfOurs();
            }
        }

        /// <summary>被抓期间锁死操控(此钩子仅本地玩家被调用)</summary>
        public override void SetControls() {
            if (!grabActive || releaseDone) {
                return;
            }
            Player.controlLeft = Player.controlRight = false;
            Player.controlUp = Player.controlDown = false;
            Player.controlJump = false;
            Player.controlUseItem = Player.controlUseTile = false;
            Player.controlHook = Player.controlMount = false;
            Player.controlThrow = false;
            //爪中禁持物：断掉回忆药水等一切使用途径
            Player.noItems = true;
        }

        /// <summary>抓住一瞬：斩断位移类挂点(仅own客户端，状态经原版玩家包回传)</summary>
        private void OnGrabStart() {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (Player.mount?.Active == true) {
                Player.mount.Dismount(Player);
            }
            Player.RemoveAllGrapplingHooks();
            Player.StopVanityActions();
        }

        /// <summary>
        /// 钉身：拖拽段自被抓点插值到爪锚(硬拉，影质无视地形)，此后咬死爪锚。
        /// 每帧清速度并回写fallStart，杜绝释放后凭空摔伤
        /// </summary>
        private void PinPlayer(NPC deer, int timer) {
            Vector2 pin;
            if (timer <= DeerclopsEyeGrabState.CatchFreezeEnd) {
                //顿帧：抓住的一瞬凝住
                pin = grabStartPos;
            }
            else if (timer <= DeerclopsEyeGrabState.DragEnd) {
                float t = (timer - DeerclopsEyeGrabState.CatchFreezeEnd)
                    / (float)(DeerclopsEyeGrabState.DragEnd - DeerclopsEyeGrabState.CatchFreezeEnd);
                //拖拽加速度感：慢启动猛收尾
                pin = Vector2.Lerp(grabStartPos, DeerclopsEyeGrabState.ClawAnchor(deer, timer), t * t);
            }
            else {
                pin = DeerclopsEyeGrabState.ClawAnchor(deer, timer) + new Vector2(0f, 16f);
            }

            Player.Center = pin;
            Player.velocity = Vector2.Zero;
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fallStart2 = Player.fallStart;
        }

        /// <summary>三段伤害节拍(仅own客户端结算，Hurt自带广播)；钳制永不致死</summary>
        private void DriveBeats(NPC deer, int timer) {
            TryFireBeat(timer, DeerclopsEyeGrabState.GripHit, DeerclopsEyeGrabState.GripDamageFrac, deer, breath: false);
            TryFireBeat(timer, DeerclopsEyeGrabState.BreathHit, DeerclopsEyeGrabState.BreathDamageFrac, deer, breath: true);
            TryFireBeat(timer, DeerclopsEyeGrabState.SlamHit, DeerclopsEyeGrabState.SlamDamageFrac, deer, breath: false);
        }

        private void TryFireBeat(int timer, int beatTick, float damageFrac, NPC deer, bool breath) {
            if (timer < beatTick || lastBeatFired >= beatTick) {
                return;
            }
            lastBeatFired = beatTick;

            bool deathMode = CWRRef.GetDeathMode() || CWRRef.GetBossRushActive();
            int damage = (int)(Player.statLifeMax2 * damageFrac * (deathMode ? 1.25f : 1f));
            //留命钳制：满血一套必不致死，残血进爪也只打到1
            damage = Math.Min(damage, Math.Max(Player.statLife - 1, 0));
            if (damage > 0) {
                Player.HurtInfo hurtInfo = new() {
                    DamageSource = PlayerDeathReason.ByNPC(deer.whoAmI),
                    SourceDamage = damage,
                    Damage = damage,
                    HitDirection = 0,
                    Knockback = 0f,
                    Dodgeable = false,
                    PvP = false,
                    CooldownCounter = -1,
                };
                Player.Hurt(hurtInfo);
            }

            if (breath) {
                //吐息附带冻noise：释放后仍延续一段的减益
                Player.AddBuff(BuffID.Frostburn, 300);
                Player.AddBuff(BuffID.Chilled, 600);
                Rendering.DeerclopsVeilFX.TriggerPunishFlash();
                RequestShake(5f, 18);
            }
            else {
                RequestShake(4f, 12);
            }
        }

        /// <summary>释放：足额无敌+小弹跳脱手；镜头由片段自然收尾</summary>
        private void DoRelease(NPC deer) {
            releaseDone = true;
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            Player.SetImmuneTimeForAllTypes(ReleaseImmune);
            int dir = deer != null ? (deer.spriteDirection != 0 ? deer.spriteDirection : 1) : 1;
            Player.velocity = new Vector2(dir * 3.2f, -6.4f);
            Player.fallStart = (int)(Player.position.Y / 16f);
            Player.fallStart2 = Player.fallStart;
            if (!Main.dedServ) {
                SoundEngine.PlaySound(SoundID.Item51 with { Volume = 0.6f, Pitch = -0.4f }, Player.Center);
            }
        }

        private static void DriveCutscene(NPC deer) {
            if (CutsceneDirector.CurrentClip is not DeerclopsGrabCutscene) {
                //restartSameClip:false，已播则复用；高优先级片段在场时Play会被拒绝，属预期降级
                CutsceneDirector.Play<DeerclopsGrabCutscene, NPC>(deer, restartSameClip: false);
            }
        }

        private static void StopCutsceneIfOurs() {
            if (CutsceneDirector.CurrentClip is DeerclopsGrabCutscene) {
                CutsceneDirector.Stop();
            }
        }
    }
}
