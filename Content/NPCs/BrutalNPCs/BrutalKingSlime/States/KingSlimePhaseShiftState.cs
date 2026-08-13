using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.States
{
    /// <summary>
    /// 阶段转换演出(60%血)：僵止→鼓胀金光内透→王冠脱冕升空绕场归位砸扣→凝胶环爆→沸腾亮相
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.PhaseShift, typeof(KingSlimeStateContext))]
    internal class KingSlimePhaseShiftState : KingSlimeStateBase
    {
        public override string StateName => "PhaseShift";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.PhaseShift;

        #region 节拍
        private const int FreezeEnd = 28;
        private const int SwellEnd = 86;
        internal const int CrownLiftFrame = 90;
        private const int BurstFrame = 132;
        private const int TotalTime = 186;
        #endregion

        private bool burstFired;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            burstFired = false;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Timer++;

            context.ContactDamageScale = 0f;
            npc.velocity.X *= 0.82f;

            if (Timer <= FreezeEnd) {
                //僵止：一切动作凝固
                context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f, 0.25f);
                if (Timer == 6) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.85f, Volume = 0.9f }, npc.Center);
                }
            }
            else if (Timer <= SwellEnd) {
                //鼓胀：内部金光渐透，颤抖加剧
                float t = (Timer - FreezeEnd) / (float)(SwellEnd - FreezeEnd);
                context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f + 0.3f * t, 0.2f);
                context.AuraMode = 1;
                context.AuraProgress = t;
                context.NinjaGlow = 0.25f * t;
                context.WobbleAmp = MathHelper.Clamp(context.WobbleAmp + 0.004f, 0f, 0.16f);

                if (!VaultUtils.isServer) {
                    if ((int)Timer % 4 == 0) {
                        KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.55f, 3);
                    }
                    if ((int)Timer % 12 == 0) {
                        KingSlimeGelFX.CameraPunch(npc.Center, 1.2f + t * 2.6f, 10, "BKSPhaseRumble");
                    }
                    if ((int)Timer % 16 == 0) {
                        SoundEngine.PlaySound(SoundID.Drown with { Pitch = -0.2f + t * 0.5f, Volume = 0.5f + t * 0.4f, MaxInstances = 3 }, npc.Center);
                    }
                }
            }
            else if (Timer == CrownLiftFrame) {
                //脱冕：先立旗再放冠，王冠回归判据依赖此旗
                context.Phase2Started = true;
                if (!VaultUtils.isClient && context.FindCrown() == null) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        KingSlimeRenderer.CrownAnchorWorld(npc, context),
                        Vector2.Zero, ModContent.ProjectileType<BKSCrownProj>(),
                        (int)(npc.defDamage * 0.55f), 0f, Main.myPlayer,
                        npc.whoAmI, BKSCrownProj.ModeLaunch);
                }
                KingSlimeGelFX.CrownChime(npc.Top, 0.5f, 1.2f);
                KingSlimeGelFX.GoldGlint(npc.Top, 26, 9f);
                KingSlimeGelFX.CameraPunch(npc.Top, 5f, 14, "BKSCrownLift", -Vector2.UnitY);
            }
            else if (Timer == BurstFrame) {
                //凝胶环爆：推场警告，正式进入P2
                if (!burstFired) {
                    burstFired = true;
                    SoundEngine.PlaySound(SoundID.QueenSlime with { Pitch = -0.4f, Volume = 1.1f }, npc.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1f }, npc.Center);
                    context.SquashVelocity -= 0.45f;
                    KingSlimeGelFX.CameraPunch(npc.Center, 8f, 18, "BKSPhaseBurst");
                    if (!VaultUtils.isServer) {
                        KingSlimeGelFX.LandingBurst(npc.Bottom, 22f, 1.6f);
                    }
                    if (!VaultUtils.isClient) {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                            ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer, 2f);
                        int dmg = (int)(npc.defDamage * 0.3f);
                        for (int i = 0; i < 14; i++) {
                            Vector2 dir = (MathHelper.TwoPi / 14f * i).ToRotationVector2();
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 8f + new Vector2(0f, -1.5f),
                                ModContent.ProjectileType<BKSGelGlobProj>(), dmg, 0f, Main.myPlayer, 0f);
                        }
                    }
                }
            }
            //脱冕到环爆之间保持蓄力光环(王冠升空的悬念拍)
            if (Timer > SwellEnd && Timer <= BurstFrame) {
                context.AuraMode = 1;
                context.AuraProgress = 1f;
                context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1.2f, 0.1f);
            }

            //爆后沸腾余韵
            if (Timer > BurstFrame) {
                context.AuraMode = 2;
                context.AuraProgress = 0.6f;
                if (!VaultUtils.isServer && (int)Timer % 6 == 0) {
                    KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.5f, 2);
                }
            }

            if (Timer >= TotalTime) {
                if (!VaultUtils.isClient) {
                    //P2出招环从头开始，单跳直入首招(潮汐开场)
                    context.AttackPhaseIndex = 0;
                    return new KingSlimeHopState(1);
                }
            }

            return null;
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            context.Phase2Started = true;
            //吞没投技不在P2开幕立刻可用：给玩家一段熟悉新阶段的缓冲
            if (context.EngulfCooldown < 480) {
                context.EngulfCooldown = 480;
            }
        }
    }
}
