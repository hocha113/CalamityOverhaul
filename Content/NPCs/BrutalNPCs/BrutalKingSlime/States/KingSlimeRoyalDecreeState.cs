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
    /// 低血大招·皇权审判(一次性)：王冠升空指挥→光柱行军→双向涨潮+中央塔洒雨→王冠终槌→脱力窗口
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)KingSlimeStateIndex.RoyalDecree, typeof(KingSlimeStateContext))]
    internal class KingSlimeRoyalDecreeState : KingSlimeStateBase
    {
        public override string StateName => "RoyalDecree";
        public override KingSlimeStateIndex StateIndex => KingSlimeStateIndex.RoyalDecree;

        #region 节拍(整体压缩：波次间贴紧，脱力窗保留)
        private const int OvertureEnd = 44;
        private const int PillarsEnd = 216;
        private const int TidesEnd = 430;
        private const int FinaleEnd = 478;
        private const int ExhaustEnd = 548;
        #endregion

        #region 公平阀(契约3)：发射循环直接读取的净空常量
        /// <summary>行军柱间距：柱打击半宽46px(BKSRoyalPillarProj)，柱间净空约98px可站立</summary>
        private const float PillarSpacingPx = 190f;
        /// <summary>行军起点离锚横距</summary>
        private const float MarchStartOffsetPx = 500f;
        /// <summary>行军柱出膛间隔帧(压短，行军更逼人但每柱警示42帧不变)</summary>
        private const int PillarIntervalFrames = 22;
        /// <summary>终拍三柱间距：柱缝净空约78px，34帧警示内一次位移可达</summary>
        private const float FinaleRingSpacingPx = 170f;
        /// <summary>潮墙起点横距；与速度/寿命共同保证中央净空带 2*(860-4.4*165)=268px，改动须保持此带&gt;0</summary>
        private const float TideWallStartPx = 860f;
        private const float TideWallSpeed = 4.4f;
        private const float TideWallTravelFrames = 165f;
        /// <summary>塔顶胶雨最小横速：中央塔周(净空带)内不落雨</summary>
        private const float RainMinVx = 5f;
        #endregion

        private int pillarsFired;
        /// <summary>行军方向：首柱发射时锁定(公平阀，玩家中途穿过锚点不许柱列瞬间换边)</summary>
        private int marchSide;
        private bool tidesSpawned;
        private bool finaleFired;
        private Vector2 anchor;
        private bool anchorInit;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            pillarsFired = 0;
            marchSide = 0;
            tidesSpawned = false;
            finaleFired = false;
            anchorInit = false;
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.1f }, context.Npc.Center);
            KingSlimeGelFX.CrownChime(context.Npc.Top, 0.6f, 1.1f);

            //王冠脱冕升空赴指挥位(升空模式见宿主处于审判态自动转指挥)
            if (!VaultUtils.isClient && context.FindCrown() == null) {
                NPC npc = context.Npc;
                Projectile.NewProjectile(npc.GetSource_FromAI(),
                    KingSlimeRenderer.CrownAnchorWorld(npc, context),
                    Vector2.Zero, ModContent.ProjectileType<BKSCrownProj>(),
                    (int)(npc.defDamage * 0.55f), 0f, Main.myPlayer,
                    npc.whoAmI, BKSCrownProj.ModeLaunch);
            }
        }

        public override IKingSlimeState OnUpdate(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;
            Timer++;

            if (!anchorInit) {
                anchorInit = true;
                anchor = KingSlimeGelFX.FindGroundBelow(npc.Bottom + new Vector2(0f, -20f));
            }

            //全程狂暴光环
            context.AuraMode = 2;
            context.AuraProgress = MathHelper.Clamp(Timer / (float)OvertureEnd, 0f, 1f);

            if (Timer <= OvertureEnd) {
                UpdateOverture(context);
            }
            else if (Timer <= PillarsEnd) {
                UpdatePillarMarch(context);
            }
            else if (Timer <= TidesEnd) {
                UpdateTides(context);
            }
            else if (Timer <= FinaleEnd) {
                UpdateFinale(context);
            }
            else if (Timer <= ExhaustEnd) {
                UpdateExhaust(context);
            }
            else {
                context.DecreeDone = true;
                npc.defense = npc.defDefense;
                if (!VaultUtils.isClient) {
                    return BackToHop(context);
                }
            }

            return null;
        }

        /// <summary>序曲：沉身蓄势，金光渐盛(王冠自行飞赴指挥位)</summary>
        private void UpdateOverture(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.X *= 0.8f;
            context.ContactDamageScale = 0f;
            float t = Timer / (float)OvertureEnd;
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 0.8f, 0.15f);

            if (!VaultUtils.isServer) {
                if ((int)Timer % 5 == 0) {
                    KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.5f, 2);
                }
                if ((int)Timer % 14 == 0) {
                    KingSlimeGelFX.CameraPunch(npc.Center, 1f + t * 2.4f, 12, "BKSDecreeRumble");
                }
                if ((int)Timer % 18 == 0) {
                    KingSlimeGelFX.GoldGlint(npc.Top + new Vector2(0f, -30f), 4, 4f);
                }
            }
        }

        /// <summary>波次一：光柱行军扫过战场，本体重跳压迫</summary>
        private void UpdatePillarMarch(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            //本体缓重跳(保持在场压力但不夺走光柱主舞台)
            if (Grounded(npc)) {
                npc.velocity.X *= 0.8f;
                if ((int)Timer % 64 == 20) {
                    float dx = player.Center.X - npc.Center.X;
                    LaunchHop(npc, MathHelper.Clamp(dx / 70f, -5.5f, 5.5f), -9.5f);
                    context.StretchImpulse(0.24f);
                }
            }

            //光柱行军：从战场一侧行军到另一侧，跨过玩家；
            //方向在首柱锁定(修复：原实现每柱重读玩家方位，玩家横穿锚点会让柱列瞬移换边)
            int maxPillars = context.IsDeathMode ? 8 : 7;
            if (pillarsFired < maxPillars && (int)Timer % PillarIntervalFrames == 10 && !VaultUtils.isClient) {
                if (marchSide == 0) {
                    marchSide = player.Center.X >= anchor.X ? 1 : -1;
                }
                float startX = anchor.X - marchSide * MarchStartOffsetPx;
                float x = startX + marchSide * pillarsFired * PillarSpacingPx;
                Vector2 ground = KingSlimeGelFX.FindGroundBelow(new Vector2(x, anchor.Y - 200f));
                Projectile.NewProjectile(npc.GetSource_FromAI(), ground, Vector2.Zero,
                    ModContent.ProjectileType<BKSRoyalPillarProj>(), (int)(npc.defDamage * 0.55f), 0f, Main.myPlayer,
                    42f);
                pillarsFired++;
            }
        }

        /// <summary>波次二：两侧涨潮夹击+本体化中央塔洒凝胶雨</summary>
        private void UpdateTides(KingSlimeStateContext context) {
            NPC npc = context.Npc;

            //本体立中央塔
            context.SkipGravity = true;
            npc.velocity = Vector2.Zero;
            npc.Bottom = new Vector2(MathHelper.Lerp(npc.Bottom.X, anchor.X, 0.08f), anchor.Y);
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 1.7f, 0.12f);

            if (!tidesSpawned && !VaultUtils.isClient) {
                tidesSpawned = true;
                int dmg = (int)(npc.defDamage * 0.5f);
                //两道慢速高墙从两侧向中央合拢；行程常量保证两墙在中央前268px净空带内消散(公平阀)
                for (int side = -1; side <= 1; side += 2) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        anchor + new Vector2(side * TideWallStartPx, -30f), new Vector2(-side * TideWallSpeed, 0f),
                        ModContent.ProjectileType<BKSTideWaveProj>(), dmg, 0f, Main.myPlayer,
                        -1f, 2f, TideWallTravelFrames);
                }
                SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.6f, Volume = 1.1f }, npc.Center);
            }

            //塔顶洒凝胶雨：横速下限保证中央净空带内无雨，左右交替(公平阀)
            if ((int)Timer % 15 == 4 && !VaultUtils.isClient) {
                int dmg = (int)(npc.defDamage * 0.36f);
                Vector2 top = npc.Top + new Vector2(0f, -20f);
                float side = (int)Timer / 15 % 2 == 0 ? 1f : -1f;
                float vx = side * Main.rand.NextFloat(RainMinVx, 9f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), top, new Vector2(vx, -Main.rand.NextFloat(5f, 9f)),
                    ModContent.ProjectileType<BKSGelGlobProj>(), dmg, 0f, Main.myPlayer,
                    Main.rand.NextBool(4) ? 1f : 0f);
            }
            if (!VaultUtils.isServer && (int)Timer % 8 == 0) {
                KingSlimeGelFX.BubbleFizz(npc.Center, npc.width * 0.4f, 1);
            }
        }

        /// <summary>终拍：王冠三柱围杀+本体砸地大冲击</summary>
        private void UpdateFinale(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            Player player = context.Target;

            if (!finaleFired) {
                finaleFired = true;
                //王冠终槌即令归位：俯冲砸扣回脱力的王头上，作为大招收束拍
                if (!VaultUtils.isClient) {
                    Projectile crown = context.FindCrown();
                    if (crown != null && (int)crown.ai[1] == BKSCrownProj.ModeDecree) {
                        crown.ai[1] = BKSCrownProj.ModeReturn;
                        crown.netUpdate = true;
                    }
                }
                //塔身砸落
                context.SquashVelocity -= 0.5f;
                KingSlimeGelFX.ThudSound(npc.Bottom, 24f);
                KingSlimeGelFX.CameraPunch(npc.Bottom, 10f, 20, "BKSDecreeFinale", Vector2.UnitY);
                if (!VaultUtils.isServer) {
                    KingSlimeGelFX.LandingBurst(npc.Bottom, 24f, 1.7f);
                }
                if (!VaultUtils.isClient) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Bottom, Vector2.Zero,
                        ModContent.ProjectileType<BKSShockwaveProj>(), 0, 0f, Main.myPlayer, 2f);
                    //玩家脚下三柱围杀(短警示，考验位移)：间距常量保证柱缝可站立(公平阀)
                    if (player.Alives()) {
                        int dmg = (int)(npc.defDamage * 0.55f);
                        for (int i = -1; i <= 1; i++) {
                            Vector2 ground = KingSlimeGelFX.FindGroundBelow(player.Center + new Vector2(i * FinaleRingSpacingPx, -60f));
                            Projectile.NewProjectile(npc.GetSource_FromAI(), ground, Vector2.Zero,
                                ModContent.ProjectileType<BKSRoyalPillarProj>(), dmg, 0f, Main.myPlayer,
                                34f);
                        }
                    }
                }
            }
        }

        /// <summary>脱力窗口：软瘫+防御跌落，奖励存活者</summary>
        private void UpdateExhaust(KingSlimeStateContext context) {
            NPC npc = context.Npc;
            npc.velocity.X *= 0.85f;
            context.ContactDamageScale = 0f;
            context.VisualSquash = MathHelper.Lerp(context.VisualSquash, 0.68f, 0.12f);
            context.AuraMode = 0;
            context.AuraProgress = 0f;
            npc.defense = Math.Max(0, npc.defDefense - 10);

            if (!VaultUtils.isServer && (int)Timer % 9 == 0) {
                KingSlimeGelFX.BubbleFizz(npc.Bottom - new Vector2(0f, 12f), npc.width * 0.4f, 1);
            }
        }

        public override void OnExit(KingSlimeStateContext context) {
            base.OnExit(context);
            context.Npc.defense = context.Npc.defDefense;
            context.DecreeDone = true;
        }
    }
}
