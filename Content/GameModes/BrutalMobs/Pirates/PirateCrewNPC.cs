using CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Pirates
{
    /// <summary>
    /// 海盗船员行为层（跳帮与舷炮·船员侧）：神射手/弩手跳弹（折线弹道预演+单次反弹）、
    /// 船长压制齐射（扇面具名缺口）、甲板水手/掠夺者劫掠钩索（钩中缓速）、鹦鹉零伤害盯梢标记。
    /// 只叠加行为不动数值（数值层归 <see cref="GameModeNPC"/>），原版 AI 全程继续跑。
    /// 决策全在权威端（客户端 PostAI 早退），客户端可见状态一律来自弹幕实体与 NPC 速度的原生同步。
    /// 入侵进度与计分只读不改
    /// </summary>
    internal class PirateCrewNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 通用节奏 ====
        /// <summary>条件未满足的重试间隔</summary>
        private const int RetryDelay = 30;
        /// <summary>资格不符（雕像怪等）的复查间隔</summary>
        private const int IneligibleDelay = 120;
        /// <summary>出生后首攻等待窗，随机错开避免同屏齐动（M7 密度预算：遭遇 ≤3 秒可见首个机制）</summary>
        private const int FirstCooldownMin = 60;
        private const int FirstCooldownMax = 180;
        /// <summary>冷却随机抖动</summary>
        private const int CooldownJitter = 50;

        //==== 神射手/弩手·跳弹 ====
        private const float MarksmanMinRange = 180f;
        private const float MarksmanMaxRange = 760f;
        /// <summary>跳弹冷却（[风味][档位]，风味 0 神射手 / 1 弩手）</summary>
        private static readonly int[][] RicochetCooldown = [[320, 270, 225], [390, 335, 280]];
        /// <summary>弹速（[风味][档位]）</summary>
        private static readonly float[][] RicochetSpeed = [[11f, 12f, 13f], [8.5f, 9.2f, 10f]];
        /// <summary>跳弹伤害 = npc.damage（已缩放值）× 此系数（风味 0/1）</summary>
        private static readonly float[] RicochetDamageFrac = [0.5f, 0.62f];
        /// <summary>跳弹全局并发上限（预演+在飞合计，超限跳过本次）</summary>
        private const int RicochetGlobalCap = 6;

        //==== 船长·压制齐射 ====
        private const float VolleyMinRange = 220f;
        private const float VolleyMaxRange = 700f;
        private static readonly int[] VolleyCooldownByTier = [360, 300, 250];
        private const float VolleyDamageFrac = 0.5f;
        /// <summary>齐射全局并发上限（军令+在飞铅弹合计）</summary>
        private const int VolleyGlobalCap = 6;

        //==== 甲板水手/掠夺者·劫掠钩索 ====
        private const float HookMinRange = 70f;
        private const float HookMaxRange = 380f;
        private static readonly int[] HookCooldownByTier = [300, 255, 210];
        private const float HookDamageFrac = 0.45f;
        /// <summary>钩索全局并发上限</summary>
        private const int HookGlobalCap = 6;

        //==== 鹦鹉·盯梢标记 ====
        /// <summary>掠过判定半径</summary>
        private const float ParrotTagRange = 240f;
        private static readonly int[] ParrotCooldownByTier = [360, 300, 240];
        /// <summary>同屏标记总数上限（纯视觉也要省）</summary>
        private const int ParrotMarkCap = 4;

        /// <summary>船员分工，由类型静态决定</summary>
        private enum CrewRole : byte
        {
            None,
            /// <summary>神射手/弩手：跳弹（风味 0/1）</summary>
            Marksman,
            /// <summary>船长：压制齐射</summary>
            Captain,
            /// <summary>甲板水手/掠夺者：劫掠钩索（风味 0/1）</summary>
            Boarder,
            /// <summary>鹦鹉：零伤害盯梢</summary>
            Parrot,
        }

        /// <summary>本个体出生时绑定的档位，0=未绑定（镜像 GameModeNPC；中途切模式不影响已出生个体）</summary>
        private int boundTier;
        private CrewRole role;
        /// <summary>类型风味位（神射手 0/弩手 1；甲板水手 0/掠夺者 1）</summary>
        private int flavor;
        /// <summary>攻击冷却（权威端决策私产，客户端可见状态全在弹幕实体上）</summary>
        private int cooldown;

        private static CrewRole ResolveRole(int type, out int flavor) {
            flavor = 0;
            switch (type) {
                case NPCID.PirateDeadeye:
                    return CrewRole.Marksman;
                case NPCID.PirateCrossbower:
                    flavor = 1;
                    return CrewRole.Marksman;
                case NPCID.PirateCaptain:
                    return CrewRole.Captain;
                case NPCID.PirateDeckhand:
                    return CrewRole.Boarder;
                case NPCID.PirateCorsair:
                    flavor = 1;
                    return CrewRole.Boarder;
                case NPCID.Parrot:
                    return CrewRole.Parrot;
                default:
                    return CrewRole.None;
            }
        }

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && ResolveRole(entity.type, out _) != CrewRole.None;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            role = ResolveRole(npc.type, out flavor);
            if (role == CrewRole.None) {
                return;
            }
            boundTier = tier;
            //雕像等排除项在攻击入口逐项复查（SpawnedFromStatue 在 SetDefaults 之后才置位）；
            //此刻 whoAmI 恒为 0，错拍只用权威端 Main.rand（冷却是决策私产，无同步语义）
            cooldown = FirstCooldownMin + Main.rand.Next(FirstCooldownMax - FirstCooldownMin + 1);
        }

        /// <summary>机制入口资格：友方/无敌/Boss/小动物载体/雕像怪/共享血池体节逐项排除</summary>
        private static bool Eligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage || npc.boss) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.SpawnedFromStatue) {
                return false;
            }
            return npc.realLife < 0;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            if (VaultUtils.isClient) {
                //决策只在服务端/单人；客户端画面全部来自同步原语
                return;
            }
            if (--cooldown > 0) {
                return;
            }
            TryStart(npc);
        }

        private void TryStart(NPC npc) {
            if (!Eligible(npc)) {
                cooldown = IneligibleDelay;
                return;
            }
            if (!npc.HasValidTarget) {
                cooldown = RetryDelay;
                return;
            }
            Player player = Main.player[npc.target];
            if (!player.Alives()) {
                cooldown = RetryDelay;
                return;
            }

            bool started = role switch {
                CrewRole.Marksman => TryMarksman(npc, player),
                CrewRole.Captain => TryCaptain(npc, player),
                CrewRole.Boarder => TryBoarder(npc, player),
                CrewRole.Parrot => TryParrot(npc, player),
                _ => false,
            };
            if (!started) {
                cooldown = RetryDelay;
            }
        }

        /// <summary>统计若干弹幕类型的全局存活数（只在触发帧调用，不进每帧路径）</summary>
        private static int CountLive(int typeA, int typeB = -1) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == typeA || proj.type == typeB) {
                    count++;
                }
            }
            return count;
        }

        private static bool CanSee(NPC npc, Player player)
            => Collision.CanHitLine(npc.Center, 1, 1, player.position, player.width, player.height);

        //==== 神射手/弩手 ====
        private bool TryMarksman(NPC npc, Player player) {
            float dist = npc.Distance(player.Center);
            if (dist < MarksmanMinRange || dist > MarksmanMaxRange || !CanSee(npc, player)) {
                return false;
            }
            if (CountLive(ModContent.ProjectileType<PrtRicochetShot>()) >= RicochetGlobalCap) {
                return false;
            }
            //预告即承诺：方向在此刻锁死进 velocity（随生成包原生同步），此后不再重瞄；
            //预演体冻结在枪口，射手走位不改折线几何
            Vector2 muzzle = npc.Center + new Vector2(Math.Sign(player.Center.X - npc.Center.X) * 14f, -2f);
            Vector2 aim = (player.Center - muzzle).SafeNormalize(Vector2.UnitX);
            int damage = Math.Max(1, (int)(npc.damage * RicochetDamageFrac[flavor]));
            int shot = Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle,
                aim * RicochetSpeed[flavor][boundTier - 1],
                ModContent.ProjectileType<PrtRicochetShot>(), damage, 0f, Main.myPlayer,
                npc.whoAmI, flavor, 0f);
            if (shot < 0 || shot >= Main.maxProjectiles) {
                return false;
            }
            cooldown = RicochetCooldown[flavor][boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            return true;
        }

        //==== 船长 ====
        private bool TryCaptain(NPC npc, Player player) {
            float dist = npc.Distance(player.Center);
            if (dist < VolleyMinRange || dist > VolleyMaxRange || !CanSee(npc, player)) {
                return false;
            }
            if (CountLive(ModContent.ProjectileType<PrtVolleyOmen>(),
                ModContent.ProjectileType<PrtFanShot>()) >= VolleyGlobalCap) {
                return false;
            }
            Vector2 muzzle = npc.Center + new Vector2(Math.Sign(player.Center.X - npc.Center.X) * 16f, -4f);
            float aim = (player.Center - muzzle).ToRotation();
            int damage = Math.Max(1, (int)(npc.damage * VolleyDamageFrac));
            int omen = Projectile.NewProjectile(npc.GetSource_FromAI(), muzzle, Vector2.Zero,
                ModContent.ProjectileType<PrtVolleyOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, aim, PrtVolleyOmen.Pack(damage, boundTier));
            if (omen < 0 || omen >= Main.maxProjectiles) {
                return false;
            }
            cooldown = VolleyCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            return true;
        }

        //==== 甲板水手/掠夺者 ====
        private bool TryBoarder(NPC npc, Player player) {
            if (npc.velocity.Y != 0f) {
                return false;
            }
            float dist = npc.Distance(player.Center);
            if (dist < HookMinRange || dist > HookMaxRange || !CanSee(npc, player)) {
                return false;
            }
            if (CountLive(ModContent.ProjectileType<PrtBoardHookProj>()) >= HookGlobalCap) {
                return false;
            }
            Vector2 hand = npc.Center + new Vector2(npc.direction * 12f, -14f);
            Vector2 aim = (player.Center - hand).SafeNormalize(Vector2.UnitX);
            int damage = Math.Max(1, (int)(npc.damage * HookDamageFrac));
            //速度读钩体自己的常量表，射程封顶两端算出的是同一个数
            float speed = PrtBoardHookProj.HookSpeedByFlavor[flavor];
            int hook = Projectile.NewProjectile(npc.GetSource_FromAI(), hand, aim * speed,
                ModContent.ProjectileType<PrtBoardHookProj>(), damage, 0f, Main.myPlayer,
                npc.whoAmI, PrtBoardHookProj.Pack(flavor, boundTier), 0f);
            if (hook < 0 || hook >= Main.maxProjectiles) {
                return false;
            }
            //刹车脉冲：出钩前站定蓄势（仅脉冲帧跟同步）
            npc.velocity.X *= 0.25f;
            npc.netUpdate = true;
            cooldown = HookCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            return true;
        }

        //==== 鹦鹉 ====
        private bool TryParrot(NPC npc, Player player) {
            float dist = npc.Distance(player.Center);
            //掠过才盯梢：贴近且正朝玩家俯冲
            if (dist > ParrotTagRange || Vector2.Dot(npc.velocity, player.Center - npc.Center) <= 0f) {
                return false;
            }
            if (PrtParrotMark.HasMarkOn(player.whoAmI)
                || CountLive(ModContent.ProjectileType<PrtParrotMark>()) >= ParrotMarkCap) {
                //已被盯上：本只鹦鹉歇口气再说
                cooldown = ParrotCooldownByTier[boundTier - 1] / 2;
                return true;
            }
            int mark = Projectile.NewProjectile(npc.GetSource_FromAI(), player.Top, Vector2.Zero,
                ModContent.ProjectileType<PrtParrotMark>(), 0, 0f, Main.myPlayer,
                player.whoAmI, 0f, 0f);
            if (mark < 0 || mark >= Main.maxProjectiles) {
                return false;
            }
            cooldown = ParrotCooldownByTier[boundTier - 1] + Main.rand.Next(CooldownJitter + 1);
            return true;
        }
    }
}
