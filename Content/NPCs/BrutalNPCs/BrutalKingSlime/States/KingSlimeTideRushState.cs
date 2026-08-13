using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 潮汐冲刷(体积形变签名1)：液化沉体→化作贴地凝胶潮头往返冲刷→回卷重组。
    /// 潮体期本体无形无判定，伤害由潮波弹幕承载
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.TideRush, typeof(KingSlimeStateContext))]
    internal class KingSlimeTideRushState : KingSlimeStateBase
    {
        public override string StateName => "TideRush";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.TideRush;

        private const int LiquifyTime = 28;
        private const int ReformTime = 26;

        /// <summary>0液化 1冲刷 2转向拍 3重组</summary>
        private int phase;
        private int phaseTimer;
        private int passesLeft;
        private int rushDir;
        private float rushSpeed;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            passesLeft = context.IsPhase2 ? 2 : 1;
            rushSpeed = 0f;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;
            phaseTimer++;

            switch (phase) {
                case 0: UpdateLiquify(context); break;
                case 1: UpdateRush(context); break;
                case 2: UpdateTurnaround(context); break;
                case 3: {
                    IKingSlimeState next = UpdateReform(context);
                    if (next != null) {
                        return next;
                    }
                    break;
                }
            }

            //看门狗：过长直接重组收招
            if (Timer > 620 && phase < 3) {
                EnterReform(context);
            }
            if (Timer > 700 && !VaultUtils.isClient) {
                return BackToHop(context);
            }

            return null;
        }

        /// <summary>幕一：液化沉体，读起手</summary>
        private void UpdateLiquify(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.X *= 0.72f;
            context.ContactDamageScale = 0f;

            float t = phaseTimer / (float)LiquifyTime;
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f - 0.68f * t, 0.35f);
            context.BodyOpacity = MathHelper.Lerp(1f, 0.45f, t);
            context.AuraMode = 1;
            context.AuraProgress = t * 0.8f;

            if (!VaultUtils.isServer) {
                if (phaseTimer % 3 == 0) {
                    KingSlimeGelFX.BubbleFizz(npc.Bottom - new Vector2(0f, 10f), npc.width * 0.45f, 2);
                }
                if (phaseTimer == LiquifyTime / 2) {
                    SoundEngine.PlaySound(SoundID.Drown with { Pitch = -0.4f, Volume = 0.8f, MaxInstances = 3 }, npc.Center);
                }
            }

            if (phaseTimer >= LiquifyTime) {
                //化潮：服务端生成潮波承载伤害
                rushDir = DirToTarget(context);
                rushSpeed = 9f;
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(rushDir, 0f),
                        ModContent.ProjectileType<BKSTideWaveProj>(), (int)(npc.defDamage * 0.5f), 0f, Main.myPlayer,
                        npc.whoAmI, 0f, 620f);
                }
                SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.55f, Volume = 1f }, npc.Center);
                KingSlimeGelFX.CameraPunch(npc.Bottom, 4f, 12, "BKSTideStart", new Vector2(rushDir, 0f));
                phase = 1;
                phaseTimer = 0;
            }
        }

        /// <summary>幕二：贴地冲刷，越冲越快，冲过目标后过冲一段</summary>
        private void UpdateRush(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //潮体期本体隐形无判定(伤害在潮波)
            context.HideBodySprite = true;
            context.ContactDamageScale = 0f;
            context.SkipGravity = true;
            npc.dontTakeDamage = true;

            //客户端方向自愈：位置同步包把速度拉回服务端值后，本地方向随之校正
            if (VaultUtils.isClient && Math.Abs(npc.velocity.X) > 1f) {
                rushDir = Math.Sign(npc.velocity.X);
            }

            //复合加速
            rushSpeed = Math.Min(rushSpeed * 1.045f, context.IsDeathMode ? 30f : 26f);
            npc.velocity = new Vector2(rushDir * rushSpeed, 0f);

            //贴地形起伏
            Vector2 ground = KingSlimeGelFX.FindGroundBelow(npc.Center + new Vector2(rushDir * 60f, -50f), 22);
            float targetY = ground.Y - npc.height * 0.35f;
            npc.Center = new Vector2(npc.Center.X, MathHelper.Lerp(npc.Center.Y, targetY, 0.32f));

            //冲过目标260px后进入转向拍
            float overshoot = (npc.Center.X - player.Center.X) * rushDir;
            if (overshoot > 260f || phaseTimer > 210) {
                phase = 2;
                phaseTimer = 0;
            }
        }

        /// <summary>幕三：硬煞回卷拍，向后抛洒凝胶雨</summary>
        private void UpdateTurnaround(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            context.HideBodySprite = true;
            context.ContactDamageScale = 0f;
            context.SkipGravity = true;
            npc.dontTakeDamage = true;

            //硬煞
            npc.velocity.X *= 0.8f;

            //回卷帧：向身后抛洒珠雨(可读的转向信号)
            if (phaseTimer == 4) {
                SoundEngine.PlaySound(SoundID.SplashWeak with { Pitch = -0.2f, Volume = 0.9f, MaxInstances = 3 }, npc.Center);
                if (!VaultUtils.isServer) {
                    KingSlimeGelFX.GelSplatter(npc.Center - new Vector2(0f, 30f), new Vector2(-rushDir, -1.4f), 9, 7f, 1f);
                }
            }

            if (phaseTimer >= 14) {
                passesLeft--;
                if (passesLeft > 0) {
                    //反向再冲
                    rushDir = -rushDir;
                    rushSpeed = 11f;
                    phase = 1;
                    phaseTimer = 0;
                }
                else {
                    EnterReform(context);
                }
            }
        }

        private void EnterReform(KingSlimeStateContext context) {
            if (phase == 3) {
                return;
            }
            phase = 3;
            phaseTimer = 0;
            NPC npc = context.Npc;
            npc.velocity = Vector2.Zero;
            //凝胶逆流回聚
            SoundEngine.PlaySound(SoundID.NPCDeath1 with { Pitch = -0.5f, Volume = 0.8f, MaxInstances = 3 }, npc.Center);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 10; i++) {
                    Vector2 from = npc.Center + Main.rand.NextVector2CircularEdge(90f, 50f);
                    InnoVault.PRT.PRTLoader.NewParticle<PRT_BKSGelBead>(from, (npc.Center - from) * 0.09f,
                        KingSlimeGelFX.GelMid * 0.8f, Main.rand.NextFloat(0.7f, 1.2f))?.Configure(18, 0.05f, 0.99f);
                }
            }
        }

        /// <summary>幕四：隆起重组，过冲弹起</summary>
        private IKingSlimeState UpdateReform(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            float t = phaseTimer / (float)ReformTime;

            context.ContactDamageScale = 0f;
            context.HideBodySprite = false;
            context.BodyOpacity = MathHelper.Clamp(t * 1.8f, 0.4f, 1f);
            npc.dontTakeDamage = t < 0.5f;
            npc.velocity.X *= 0.8f;

            if (phaseTimer == 1) {
                context.SquashVelocity += 0.5f;//隆起过冲
            }
            if (phaseTimer == ReformTime / 2) {
                KingSlimeGelFX.SquishSound(npc.Center, 0.1f, 0.9f);
            }

            if (phaseTimer >= ReformTime) {
                npc.dontTakeDamage = false;
                if (!VaultUtils.isClient) {
                    return BackToHop(context);
                }
            }
            return null;
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            context.Npc.dontTakeDamage = false;
        }
    }
}
