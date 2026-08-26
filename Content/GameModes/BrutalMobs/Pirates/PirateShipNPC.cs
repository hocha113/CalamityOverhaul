using CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates
{
    /// <summary>
    /// 荷兰飞船（小Boss）行为叠加层：原版 AI 继续跑，只追加两个签名技交替施放。<br/>
    /// 舷炮齐射：炮口自上而下顺序亮起，水平弹道车道制，带具名空车道（<see cref="PrtBroadsideOmen"/>）。<br/>
    /// 跳帮号令：升旗预告后遴选旗手，≤5 秒短脉冲给周围船员提速，打掉旗手即止（<see cref="PrtBoardingOmen"/>）。<br/>
    /// 旗标无关设计：飞船进层靠显式类型名单（AppliesToEntity 只放行 PirateShip），
    /// 不走通用资格口径（船体离线查证无伤害接触、疑似不可直接受击，boss 旗标未能离线证实——
    /// 本层不注入任何 NPC 速度，无提速补偿议题）；PirateShipCannon 部件不进本层，
    /// 炮口挂在船体几何位上，部件关系降级为不依赖（§2.2）。<br/>
    /// 签名技伤害用档位常量表并在运行时以 npc.damage 封顶（船体伤害为 0 时常量即为准，报告备案）。<br/>
    /// 每实例同一时刻至多一个签名技进行中：单计时器覆盖"预告+执行+冷却"全程，结构上保证不重叠。
    /// 死亡流程不加机制（事件计分依赖），入侵进度只读不改
    /// </summary>
    internal class PirateShipNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>首个签名技的等待窗（另加随机错拍）</summary>
        private const int FirstSigDelayMin = 240;
        private const int FirstSigDelayJitter = 120;
        /// <summary>签名技冷却（档位 1/2/3，从上一技完全结束起算）</summary>
        private static readonly int[] SigCooldownByTier = [560, 480, 400];
        /// <summary>目标脱离此距离不起手</summary>
        private const float SigEngageRange = 1150f;
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryDelay = 45;
        /// <summary>舷炮齐射的执行窗（铁弹飞完车道的时长上限，计时器随预告一并覆盖）</summary>
        private const int BroadsideStrikeWindow = 150;
        /// <summary>舷炮铁弹伤害（档位 1/2/3；生成时若船体 npc.damage&gt;0 则以其封顶）</summary>
        private static readonly int[] BroadsideDamageByTier = [36, 44, 52];

        /// <summary>本个体出生时绑定的档位，0=未绑定</summary>
        private int boundTier;
        /// <summary>签名技计时（权威端决策私产）：覆盖当前技的预告+执行+冷却全程</summary>
        private int sigTimer;
        /// <summary>下一个签名技（0 舷炮齐射 / 1 跳帮号令，交替）</summary>
        private int nextSig;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && entity.type == NPCID.PirateShip;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            //显式名单放行：能走到这里的只有 PirateShip（AppliesToEntity 过滤），
            //不做通用资格判定（船体接触伤害为 0、疑似 dontTakeDamage，通用口径会误杀）
            boundTier = tier;
            sigTimer = FirstSigDelayMin + Main.rand.Next(FirstSigDelayJitter + 1);
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在权威端；客户端可见状态全在预兆/战旗实体上（原生同步）
                return;
            }
            if (--sigTimer > 0) {
                return;
            }
            TrySignature(npc);
        }

        private void TrySignature(NPC npc) {
            if (!npc.HasValidTarget) {
                sigTimer = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives() || npc.Distance(player.Center) > SigEngageRange) {
                sigTimer = RetryDelay;
                return;
            }

            bool started = nextSig == 0 ? StartBroadside(npc, player) : StartBoardingCall(npc);
            if (!started) {
                sigTimer = RetryDelay;
                return;
            }
            nextSig ^= 1;
        }

        /// <summary>舷炮齐射：开火侧在此刻锁死（预告即承诺），车道几何随预兆生成瞬间冻结</summary>
        private bool StartBroadside(NPC npc, Player player) {
            int dir = player.Center.X >= npc.Center.X ? 1 : -1;
            int damage = BroadsideDamageByTier[boundTier - 1];
            if (npc.damage > 0) {
                //签名技伤害 ≤ npc.damage（已缩放值）；船体伤害为 0 时此封顶不可用，常量表即为准
                damage = Math.Min(damage, npc.damage);
            }
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<PrtBroadsideOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, dir, PrtBroadsideOmen.Pack(damage, boundTier));
            if (omen < 0 || omen >= Main.maxProjectiles) {
                return false;
            }
            sigTimer = PrtBroadsideOmen.TelegraphFrames + BroadsideStrikeWindow
                + SigCooldownByTier[boundTier - 1];
            return true;
        }

        /// <summary>跳帮号令：升旗预告，升满后旗手遴选与短脉冲由预兆实体在权威端接力</summary>
        private bool StartBoardingCall(NPC npc) {
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<PrtBoardingOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, boundTier, 0f);
            if (omen < 0 || omen >= Main.maxProjectiles) {
                return false;
            }
            sigTimer = PrtBoardingOmen.TelegraphFrames + PrtBannerMark.PulseFrames
                + SigCooldownByTier[boundTier - 1];
            return true;
        }
    }
}
