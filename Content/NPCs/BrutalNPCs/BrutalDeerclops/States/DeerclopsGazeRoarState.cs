using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 凝视吼叫——"别与它对视"。风雪先退(反向预兆)，独眼白转血红，
    /// 长鸣升调后骤然噤声，随即咆哮：惩罚窗内面向它的玩家被冻结。逐玩家本地结算
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.GazeRoar, typeof(DeerclopsStateContext))]
    internal class DeerclopsGazeRoarState : DeerclopsStateBase
    {
        public override string StateName => "GazeRoar";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.GazeRoar;

        private const int SetupEnd = 40;
        private const int PunishWindow = 60;
        private const float GazeRange = 1150f;

        /// <summary>凝视教学提示，本客户端一次</summary>
        private static bool gazeHintShown;

        /// <summary>本地玩家本次咆哮是否已受罚(各端只管自己)</summary>
        private bool localPunished;

        private int WarnEnd(DeerclopsStateContext ctx) => ctx.IsPhase2 ? 95 : 110;
        private int RoarEnd(DeerclopsStateContext ctx) => WarnEnd(ctx) + PunishWindow;
        private int StateEnd(DeerclopsStateContext ctx) => RoarEnd(ctx) + 36;

        public override void OnEnter(DeerclopsStateContext context) {
            base.OnEnter(context);
            localPunished = false;
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.HaltMovement = true;
            npc.damage = 0;
            FaceTarget(context);

            int warnEnd = WarnEnd(context);
            int roarEnd = RoarEnd(context);

            //幕一：风雪退去——预兆用寂静书写
            if (Timer <= SetupEnd) {
                context.VeilTarget = 0.1f;
                context.EyeGlow = 0.3f;
                if (Timer < 12) {
                    context.AnimMode = DeerAnimMode.Crouch;
                }
                if (Timer == 6 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item119 with { Volume = 0.5f, Pitch = -0.6f }, npc.Center);
                }
                //教学提示：首次遇到且看得见它
                if (Timer == 20 && !Main.dedServ && !gazeHintShown
                    && Main.LocalPlayer.Alives() && Main.LocalPlayer.Distance(npc.Center) < 1500f) {
                    gazeHintShown = true;
                    CombatText.NewText(Main.LocalPlayer.Hitbox, DeerclopsMotion.GazeRed, DeerclopsAI.GazeWarn_Text.Value, true);
                }
                return null;
            }

            //幕二：蓄势——眼由白转红，长鸣升调，末12帧骤然噤声
            if (Timer <= warnEnd) {
                context.VeilTarget = 0.08f;
                context.GazePhase = 1;
                float p = (Timer - SetupEnd) / (float)(warnEnd - SetupEnd);
                context.EyeGlow = MathHelper.Lerp(0.3f, 1f, p);
                context.EyeHeat = p;
                context.AnimMode = DeerAnimMode.Roar;
                context.AnimTimer = (Timer - SetupEnd) * 32 / (warnEnd - SetupEnd);

                if (Timer == SetupEnd + 4 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.35f, Pitch = -0.9f }, npc.Center);
                }
                if (Timer == SetupEnd + (warnEnd - SetupEnd) / 2 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 0.5f, Pitch = -0.45f }, npc.Center);
                }
                //收束的冷芒吸入眼中(本端)
                if (!Main.dedServ && Timer % 3 == 0) {
                    Vector2 eye = npc.Center - new Vector2(-npc.spriteDirection * 20f, 60f);
                    Vector2 spawn = eye + Main.rand.NextVector2Unit() * Main.rand.NextFloat(90f, 220f);
                    Dust dust = Dust.NewDustPerfect(spawn, DustID.Frost, (eye - spawn) * 0.07f, 120, default, Main.rand.NextFloat(0.8f, 1.4f));
                    dust.noGravity = true;
                }
                return null;
            }

            //幕三：咆哮与惩罚窗
            if (Timer <= roarEnd) {
                context.VeilTarget = 0.85f;
                context.GazePhase = 2;
                context.EyeGlow = 1f;
                context.EyeHeat = 1f;
                context.AnimMode = DeerAnimMode.Roar;
                //慢推进让咆哮帧覆盖整个惩罚窗
                context.AnimTimer = 32 + (Timer - warnEnd) / 3;

                if (Timer == warnEnd + 1) {
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 1.35f, Pitch = 0.05f }, npc.Center);
                    }
                    DeerclopsMotion.CameraPunch(npc.Center, 11f, 24, "DeerGazeRoar");
                    //影爆(本端)
                    if (!Main.dedServ) {
                        for (int i = 0; i < 26; i++) {
                            Dust dust = Dust.NewDustPerfect(npc.Center, DustID.Shadowflame,
                                Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 11f), 130, default, Main.rand.NextFloat(1.2f, 2f));
                            dust.noGravity = true;
                        }
                    }
                }
                if (Timer % 5 == 0) {
                    DeerclopsMotion.CameraPunch(npc.Center, 3f, 10, "DeerGazeRumble");
                }

                //凝视惩罚：本地玩家自查自罚(原版式逐端结算)
                if (!Main.dedServ && !localPunished && Timer % 3 == 0
                    && DeerclopsAI.LocalPlayerFacing(npc, GazeRange)) {
                    PunishLocalPlayer(context);
                }
                return null;
            }

            //幕四：余韵
            context.EyeHeat = MathHelper.Clamp(1f - (Timer - roarEnd) / 30f, 0f, 1f);
            if (Timer >= StateEnd(context)) {
                return new DeerclopsStalkState();
            }
            return null;
        }

        /// <summary>冻结本地玩家：与它对视的代价</summary>
        private void PunishLocalPlayer(DeerclopsStateContext context) {
            localPunished = true;
            Player player = Main.LocalPlayer;
            player.AddBuff(BuffID.Frozen, context.IsPhase2 ? 55 : 45);
            player.AddBuff(BuffID.Chilled, 240);
            player.AddBuff(BuffID.Slow, 180);
            DeerclopsVeilFX.TriggerPunishFlash();
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 1f, Pitch = -0.5f }, player.Center);
            for (int i = 0; i < 18; i++) {
                Dust dust = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Ice,
                    Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(0.5f, 2f), 60, default, Main.rand.NextFloat(1f, 1.7f));
                dust.noGravity = Main.rand.NextBool();
            }
        }
    }
}
