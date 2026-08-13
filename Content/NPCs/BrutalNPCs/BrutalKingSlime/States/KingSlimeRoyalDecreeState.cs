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

        #region 节拍
        private const int OvertureEnd = 44;
        private const int PillarsEnd = 250;
        private const int TidesEnd = 470;
        private const int FinaleEnd = 520;
        private const int ExhaustEnd = 590;
        #endregion

        private int pillarsFired;
        private bool tidesSpawned;
        private bool finaleFired;
        private Vector2 anchor;
        private bool anchorInit;

        public override void OnEnter(KingSlimeStateContext context) {
            base.OnEnter(context);
            pillarsFired = 0;
            tidesSpawned = false;
            finaleFired = false;
            anchorInit = false;
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f, Volume = 1.1f }, context.Npc.Center);
            KingSlimeGelFX.CrownChime(context.Npc.Top, 0.6f, 1.1f);
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

            //7根光柱：从战场一侧行军到另一侧，跨过玩家
            int interval = 26;
            int maxPillars = context.IsDeathMode ? 8 : 7;
            if (pillarsFired < maxPillars && (int)Timer % interval == 10 && !VaultUtils.isClient) {
                int side = player.Center.X >= anchor.X ? 1 : -1;
                float startX = anchor.X - side * 500f;
                float x = startX + side * pillarsFired * 190f;
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
                //两道慢速高墙从两侧向中央合拢，中途消散留出安全窗
                for (int side = -1; side <= 1; side += 2) {
                    Projectile.NewProjectile(npc.GetSource_FromAI(),
                        anchor + new Vector2(side * 860f, -30f), new Vector2(-side * 4.4f, 0f),
                        ModContent.ProjectileType<BKSTideWaveProj>(), dmg, 0f, Main.myPlayer,
                        -1f, 2f, 165f);
                }
                SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.6f, Volume = 1.1f }, npc.Center);
            }

            //塔顶洒凝胶雨
            if ((int)Timer % 15 == 4 && !VaultUtils.isClient) {
                int dmg = (int)(npc.defDamage * 0.36f);
                Vector2 top = npc.Top + new Vector2(0f, -20f);
                float vx = Main.rand.NextFloat(-9f, 9f);
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
                    //玩家脚下三柱围杀(短警示，考验位移)
                    if (player.Alives()) {
                        int dmg = (int)(npc.defDamage * 0.55f);
                        for (int i = -1; i <= 1; i++) {
                            Vector2 ground = KingSlimeGelFX.FindGroundBelow(player.Center + new Vector2(i * 150f, -60f));
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
