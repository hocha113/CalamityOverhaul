using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.Projectiles;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDeerclops.States
{
    /// <summary>
    /// 白澈长嚎(≤28%血，一次性大招)：暴雪吞没一切，唯它身侧留一圈清明——
    /// 安全区反转，玩家被迫与恐惧同行。圈内冰刺梳交替扫出，圈外压边手巡猎
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)DeerclopsStateIndex.Whiteout, typeof(DeerclopsStateContext))]
    internal class DeerclopsWhiteoutState : DeerclopsStateBase
    {
        public override string StateName => "Whiteout";
        public override DeerclopsStateIndex StateIndex => DeerclopsStateIndex.Whiteout;

        private const int EntryEnd = 66;
        private const int LoopRound = 140;
        private const int LoopCount = 3;
        private const int LoopEnd = EntryEnd + LoopRound * LoopCount;
        private const int StateEnd = LoopEnd + 66;
        /// <summary>清明圈半径(与渲染uClearRadius一致)</summary>
        internal const float ClearRadius = 430f;

        public override void OnEnter(DeerclopsStateContext context) {
            base.OnEnter(context);
            if (!VaultUtils.isClient) {
                DeerclopsAI.SetFlag(context.Npc, DeerclopsAI.FlagWhiteoutUsed);
            }
        }

        public override IDeerclopsState OnUpdate(DeerclopsStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            //白澈强度全程声明
            context.Whiteout = MathHelper.Clamp((Timer - 8) / 58f, 0f, 1f);
            if (Timer > LoopEnd) {
                context.Whiteout = MathHelper.Clamp(1f - (Timer - LoopEnd) / 50f, 0f, 1f);
            }
            context.VeilTarget = 0.9f;
            context.EyeGlow = 1f;
            context.EyeHeat = 1f;

            //幕一：仰天长嚎，白幕落下
            if (Timer <= EntryEnd) {
                context.HaltMovement = true;
                npc.damage = 0;
                context.AnimMode = DeerAnimMode.Roar;
                context.AnimTimer = Timer;

                if (Timer == 24 && !Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.DeerclopsScream with { Volume = 1.3f, Pitch = -0.3f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.BlizzardStrongLoop with { Volume = 0.9f }, npc.Center);
                }
                if (Timer == 30) {
                    DeerclopsMotion.CameraPunch(npc.Center, 8f, 24, "DeerWhiteoutRoar");
                }
                return null;
            }

            //幕二：白澈巡猎——缓步逼近，圈内梳刺，圈外放手
            if (Timer <= LoopEnd) {
                context.MoveSpeedMult = 0.55f;
                npc.damage = npc.defDamage;

                int loopTimer = (Timer - EntryEnd - 1) % LoopRound;
                int round = (Timer - EntryEnd - 1) / LoopRound;

                //每轮两把梳刺：交替侧扫出
                if (loopTimer == 20 || loopTimer == 90) {
                    int sweepDir = (round + (loopTimer == 90 ? 1 : 0)) % 2 == 0 ? 1 : -1;
                    if (npc.spriteDirection != 0) {
                        sweepDir *= npc.spriteDirection;
                    }
                    SpawnSpikeComb(context, sweepDir);
                    if (!Main.dedServ) {
                        SoundEngine.PlaySound(SoundID.DeerclopsRubbleAttack with { Volume = 0.8f, Pitch = -0.2f }, npc.Bottom);
                    }
                    DeerclopsMotion.CameraPunch(npc.Bottom, 5f, 16, "DeerWhiteoutComb", Vector2.UnitY);
                }

                //圈外者：压边手巡猎(服务端)
                if (!VaultUtils.isClient && loopTimer % 46 == 0) {
                    foreach (Player player in Main.ActivePlayers) {
                        if (!player.Alives()) {
                            continue;
                        }
                        float dist = player.Distance(npc.Center);
                        if (dist > ClearRadius + 90f && dist < 3000f) {
                            DeerShadowHandProj.SpawnBorderHand(npc, player);
                        }
                    }
                }
                return null;
            }

            //幕三：力竭喘息——白幕散去，破绽全开
            context.HaltMovement = true;
            npc.damage = 0;
            context.AnimMode = DeerAnimMode.Crouch;
            context.EyeGlow = MathHelper.Clamp(1f - (Timer - LoopEnd) / 40f, 0.2f, 1f);
            if (Timer == LoopEnd + 10 && !Main.dedServ) {
                SoundEngine.PlaySound(SoundID.DeerclopsHit with { Volume = 0.9f, Pitch = -0.5f }, npc.Center);
            }

            if (Timer >= StateEnd) {
                return new DeerclopsStalkState();
            }
            return null;
        }

        /// <summary>圈内梳刺：从脚边向外14根，行进快、留缺口</summary>
        private void SpawnSpikeComb(DeerclopsStateContext context, int dir) {
            if (VaultUtils.isClient) {
                return;
            }
            NPC npc = context.Npc;
            Point feet = npc.Bottom.ToTileCoordinates();
            int damage = context.IsDeathMode ? 20 : 16;

            for (int i = 0; i < 14; i++) {
                if (i % 5 == 4) {
                    continue;
                }
                int tileX = feet.X + dir * (2 + i * 2);
                float lean = dir * i * 0.6f * (MathHelper.PiOver4 / 14f);
                float scale = MathHelper.Clamp(0.7f + i * 0.04f, 0.6f, 1.2f);
                int telegraph = TelegraphTime(context, 16 + i * 3, 12);
                DeerIceSpikeProj.TrySpawn(npc, tileX, feet.Y, lean, scale, telegraph, damage);
            }
        }
    }
}
