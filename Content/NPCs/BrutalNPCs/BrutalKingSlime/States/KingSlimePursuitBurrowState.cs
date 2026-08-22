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
    /// 追击阀：目标远离/失联时化胶渗地→地下高速掠行(地表隆起线预兆)→喷泉爆出。
    /// 替代原版瞬移：以"行进过去"取代"闪现"，本身也是一次攻击
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.PursuitBurrow, typeof(KingSlimeStateContext))]
    internal class KingSlimePursuitBurrowState : KingSlimeStateBase
    {
        public override string StateName => "PursuitBurrow";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.PursuitBurrow;

        private const int SinkTime = 22;
        /// <summary>公平阀(契约3)：爆出点由喷泉预兆24帧，常量直接写入喷泉弹幕ai
        /// 警示期本体无判定，玩家有一整段位移窗离开爆点</summary>
        private const int GeyserWarnTime = 24;
        private const int EruptRecover = 30;

        /// <summary>0渗地 1地下掠行 2爆出</summary>
        private int phase;
        private int phaseTimer;
        private bool geyserSpawned;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            phase = 0;
            phaseTimer = 0;
            geyserSpawned = false;
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;
            phaseTimer++;

            switch (phase) {
                case 0: {
                    //渗地：液化沉入
                    npc.velocity.X *= 0.7f;
                    context.ContactDamageScale = 0f;
                    float t = phaseTimer / (float)SinkTime;
                    context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1f - 0.72f * t, 0.35f);
                    context.BodyOpacity = 1f - t * 0.85f;
                    if (phaseTimer == 4) {
                        SoundEngine.PlaySound(SoundID.Drown with { Pitch = -0.5f, Volume = 0.9f }, npc.Center);
                    }
                    if (!VaultUtils.isServer && phaseTimer % 3 == 0) {
                        KingSlimeGelFX.BubbleFizz(npc.Bottom - new Vector2(0f, 8f), npc.width * 0.4f, 2);
                    }

                    if (phaseTimer >= SinkTime) {
                        phase = 1;
                        phaseTimer = 0;
                        npc.dontTakeDamage = true;
                    }
                    break;
                }
                case 1: {
                    //地下掠行：无形高速，地表隆起线+土粒预兆
                    context.HideBodySprite = true;
                    context.ContactDamageScale = 0f;
                    context.SkipGravity = true;
                    npc.dontTakeDamage = true;
                    npc.noTileCollide = true;

                    if (!player.Alives()) {
                        //目标失效直接爆出收招
                        EnterErupt(context);
                        break;
                    }

                    //贴地下方掠向玩家脚底
                    Vector2 ground = KingSlimeGelFX.FindGroundBelow(player.Center, 60);
                    Vector2 dest = ground + new Vector2(0f, 70f);
                    Vector2 toDest = dest - npc.Center;
                    float speed = Math.Min(14f + phaseTimer * 0.5f, 34f);
                    npc.velocity = toDest.SafeNormalize(Vector2.Zero) * Math.Min(speed, toDest.Length());

                    //地表隆起预兆
                    if (!VaultUtils.isServer) {
                        Vector2 surface = KingSlimeGelFX.FindGroundBelow(npc.Center - new Vector2(0f, 120f), 30);
                        if ((int)Timer % 2 == 0) {
                            Dust d = Dust.NewDustDirect(surface - new Vector2(20f, 8f), 40, 8,
                                DustID.Dirt, 0, 0, 110, default, Main.rand.NextFloat(1.1f, 1.9f));
                            d.velocity = new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(1.5f, 4f));
                        }
                        if ((int)Timer % 5 == 0) {
                            KingSlimeGelFX.BubbleFizz(surface - new Vector2(0f, 4f), 20f, 1);
                        }
                        if ((int)Timer % 11 == 0) {
                            SoundEngine.PlaySound(SoundID.WormDig with { Pitch = -0.3f, Volume = 0.55f, MaxInstances = 3 }, surface);
                        }
                    }

                    //到位或超时→爆出
                    if (Math.Abs(toDest.X) < 46f && Math.Abs(toDest.Y) < 90f) {
                        EnterErupt(context);
                    }
                    else if (phaseTimer > 190) {
                        EnterErupt(context);
                    }
                    break;
                }
                case 2: {
                    //爆出：先由喷泉警示，警示末帧本体破土跃出
                    context.ContactDamageScale = 0f;
                    context.SkipGravity = phaseTimer < GeyserWarnTime;
                    context.HideBodySprite = phaseTimer < GeyserWarnTime;
                    npc.dontTakeDamage = phaseTimer < GeyserWarnTime;

                    if (phaseTimer == GeyserWarnTime) {
                        //破土帧
                        npc.noTileCollide = false;
                        Vector2 surface = KingSlimeGelFX.FindGroundBelow(npc.Center - new Vector2(0f, 160f), 40);
                        npc.Bottom = surface + new Vector2(0f, 6f);
                        int dir = player.Alives() ? Math.Sign(player.Center.X - npc.Center.X) : 1;
                        LaunchHop(npc, dir * 4.5f, -15.5f);
                        context.BodyOpacity = 1f;
                        context.StretchImpulse(0.5f);
                        context.PendingLandingShockwave = 1;
                        SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.4f, Volume = 1.1f }, npc.Center);
                        KingSlimeGelFX.CameraPunch(npc.Center, 6f, 14, "BKSBurrowErupt", -Vector2.UnitY);
                        if (!VaultUtils.isServer) {
                            KingSlimeGelFX.LandingBurst(npc.Bottom, 18f, 1.4f);
                        }
                    }
                    else if (phaseTimer > GeyserWarnTime) {
                        //出土后恢复常规
                        context.ContactDamageScale = 1f;
                        if (context.JustLanded || phaseTimer > GeyserWarnTime + EruptRecover) {
                            if (!VaultUtils.isClient) {
                                return BackToHop(context);
                            }
                        }
                    }
                    break;
                }
            }

            if (Timer > 380 && !VaultUtils.isClient) {
                //看门狗：强制归位收招
                NPC n = context.Npc;
                n.noTileCollide = false;
                n.dontTakeDamage = false;
                return BackToHop(context);
            }

            return null;
        }

        /// <summary>转入爆出：服务端生成警示喷泉</summary>
        private void EnterErupt(KingSlimeStateContext context) {
            if (phase == 2) {
                return;
            }
            phase = 2;
            phaseTimer = 0;

            NPC npc = context.Npc;
            npc.velocity = Vector2.Zero;
            if (!geyserSpawned && !VaultUtils.isClient) {
                geyserSpawned = true;
                Vector2 surface = KingSlimeGelFX.FindGroundBelow(npc.Center - new Vector2(0f, 160f), 40);
                Projectile.NewProjectile(npc.GetSource_FromAI(), surface, Vector2.Zero,
                    ModContent.ProjectileType<BKSGeyserProj>(), (int)(npc.defDamage * 0.45f), 0f, Main.myPlayer,
                    1f, GeyserWarnTime);
            }
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            context.Npc.noTileCollide = false;
            context.Npc.dontTakeDamage = false;
        }
    }
}
