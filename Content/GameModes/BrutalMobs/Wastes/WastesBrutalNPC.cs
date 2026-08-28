using CalamityOverhaul.Content.GameModes.BrutalMobs.Wastes.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Events;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Wastes
{
    /// <summary>
    /// 残酷模式荒地组（沙漠+雪原）行为机制层，主题：环境武器化。
    /// 叠加在原版 AI 之上，不接管：遁地伏击地涌（尘柱预告→破土）、沙喷锥形幕（具名角度缺口）、
    /// 冰面打滑区（场地实体，寒颤玩家、加速雪原兽）、死亡径向冰晶碎裂（凝晶预告→放射，具名安全扇区）、
    /// 元素近身环（可见环=判定环）。
    /// 决策与生成只在权威端跑，客户端一律通过同步弹幕实体看到状态；数值增强由 GameModeNPC 统一负责，此处只加行为
    /// </summary>
    internal class WastesBrutalNPC : GlobalNPC
    {
        //==== 遁地伏击地涌（蠕虫头） ====
        /// <summary>地涌冷却，档位只调频率不换机制</summary>
        private static readonly int[] GeyserCooldownByTier = [330, 290, 250];
        /// <summary>连锁地涌间隔（tier≥2 追加，每发都有完整预告并重新锁点）</summary>
        private const int GeyserChainGap = 46;
        private const float GeyserMinRange = 150f;
        private const float GeyserMaxRange = 880f;
        /// <summary>地涌伤害 = 已缩放 npc.damage × 此值</summary>
        private const float GeyserDamageFrac = 0.65f;
        /// <summary>地涌全局并发上限，超限跳过本次触发</summary>
        private const int GeyserCap = 6;

        //==== 沙喷锥形幕（参数档见 WastesSandConeTelegraph.Profiles） ====
        /// <summary>沙弹伤害 = 已缩放 npc.damage × 此值</summary>
        private const float ConeDamageFrac = 0.55f;
        /// <summary>锥幕预告全局并发上限</summary>
        private const int ConeCap = 6;
        /// <summary>锥幕冷却的档位缩减量（每级）</summary>
        private const int ConeCooldownStepPerTier = 40;

        //==== 冰面打滑区（冰龟震地） ====
        private static readonly int[] StompCooldownByTier = [520, 460, 400];
        private const float StompMinRange = 120f;
        private const float StompMaxRange = 520f;
        /// <summary>打滑区半宽，档位只加强度</summary>
        private static readonly int[] ZoneHalfWidthByTier = [110, 130, 150];
        /// <summary>打滑区存续帧</summary>
        private static readonly int[] ZoneActiveByTier = [420, 540, 660];
        /// <summary>打滑区全局并发上限</summary>
        private const int ZoneCap = 4;

        //==== 死亡径向冰晶碎裂 ====
        /// <summary>常规冰壳怪的碎片数，档位每级 +2（安全扇区不变）</summary>
        private const int ShardCountBase = 8;
        /// <summary>冰蝙蝠小型化碎片数</summary>
        private const int BatShardCount = 4;
        /// <summary>碎片伤害 = 已缩放 npc.damage × 此值</summary>
        private const float ShardDamageFrac = 0.55f;
        /// <summary>凝晶核全局并发上限</summary>
        private const int ShatterCap = 6;

        //==== 元素近身环 ====
        private const int RingRadiusIce = 140;
        private const int RingRadiusSand = 150;
        /// <summary>环半径的档位增量（每级）</summary>
        private const int RingRadiusStepPerTier = 10;

        //==== 蝎类接触毒液（类型风味） ====
        /// <summary>沙漠蝎接触中毒时长，档位每级 +60</summary>
        private const int ScorpionPoisonBase = 300;

        //==== 通用 ====
        /// <summary>向下寻找地表的最大瓦格数（超出视为目标悬空，放弃地面机制）</summary>
        private const int GroundSearchTiles = 12;
        /// <summary>触发条件不满足时的复查间隔</summary>
        private const int TriggerRetryFrames = 30;

        /// <summary>遁地伏击拥有者（蠕虫只挂头，身/尾不在类型表且被 realLife 双保险排除）</summary>
        private static readonly HashSet<int> GeyserTypes = [NPCID.TombCrawlerHead, NPCID.DuneSplicerHead];

        /// <summary>沙喷家族：类型 → 锥幕参数档</summary>
        private static readonly Dictionary<int, int> ConeProfileByType = new() {
            [NPCID.Antlion] = WastesSandConeTelegraph.ProfileSpitter,
            [NPCID.FlyingAntlion] = WastesSandConeTelegraph.ProfileFlyer,
            [NPCID.GiantFlyingAntlion] = WastesSandConeTelegraph.ProfileGiantFlyer,
            [NPCID.WalkingAntlion] = WastesSandConeTelegraph.ProfileKick,
            [NPCID.GiantWalkingAntlion] = WastesSandConeTelegraph.ProfileKick,
            [NPCID.DesertLamiaLight] = WastesSandConeTelegraph.ProfileKick,
            [NPCID.DesertLamiaDark] = WastesSandConeTelegraph.ProfileKick,
            [NPCID.DesertGhoul] = WastesSandConeTelegraph.ProfileBreath,
            [NPCID.DesertGhoulCorruption] = WastesSandConeTelegraph.ProfileBreath,
            [NPCID.DesertGhoulCrimson] = WastesSandConeTelegraph.ProfileBreath,
            [NPCID.DesertGhoulHallow] = WastesSandConeTelegraph.ProfileBreath,
        };

        /// <summary>死亡径向碎裂拥有者（冰壳类）</summary>
        private static readonly HashSet<int> ShatterTypes = [
            NPCID.IceBat, NPCID.UndeadViking, NPCID.ArmoredViking,
            NPCID.IceTortoise, NPCID.IceElemental, NPCID.IcyMerman,
        ];

        /// <summary>元素近身环拥有者</summary>
        internal static readonly HashSet<int> RingTypes = [NPCID.IceElemental, NPCID.DesertDjinn];

        /// <summary>踏冰滑行受益者（打滑区对雪原兽是加速带，由区实体扫描，不需要本类实例）</summary>
        internal static readonly HashSet<int> SlideTypes = [
            NPCID.Wolf, NPCID.SnowFlinx, NPCID.CorruptPenguin, NPCID.CrimsonPenguin,
            NPCID.UndeadViking, NPCID.ArmoredViking,
        ];

        /// <summary>本类实际挂载的类型全集（声明顺序在各分表之后，静态初始化按序执行）</summary>
        private static readonly HashSet<int> AllTypes = BuildAllTypes();

        private static HashSet<int> BuildAllTypes() {
            HashSet<int> all = [NPCID.DesertScorpionWalk, NPCID.DesertScorpionWall];
            all.UnionWith(GeyserTypes);
            all.UnionWith(ConeProfileByType.Keys);
            all.UnionWith(ShatterTypes);
            all.UnionWith(RingTypes);
            return all;
        }

        public override bool InstancePerEntity => true;

        /// <summary>本个体生成时绑定的档位，0 = 未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        /// <summary>主动机制冷却；服务端决策私产，客户端不得用它驱动画面</summary>
        private int actionTimer;
        /// <summary>本轮地涌连锁剩余次数</summary>
        private int comboLeft;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && AllTypes.Contains(entity.type);

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            boundTier = tier;
            //出生错拍：避免同屏群体同帧齐射（M7 密度预算：60~180 帧窗）。
            //SetDefaults 期 whoAmI 尚未赋值（NewNPC 之后才写入），不可用作错拍源；
            //冷却是权威端决策私产，随机数无同步语义
            actionTimer = 60 + Main.rand.Next(121);
        }

        /// <summary>
        /// 沙隐联动（DuneStorm×Wastes 试点）：沙暴事件中地表荒地怪获得沙隐。
        /// 只读原版天气（全端同步的世界状态），不读氛围包的本机观察量；
        /// 隐身换来的公平回款在锥幕缺口加宽（<see cref="WastesSandConeTelegraph.CurrentGapHalfAngle"/>）
        /// </summary>
        internal static bool SandVeilActive(NPC npc)
            => GameModeSystem.EffectiveTier > 0 && Sandstorm.Happening && Sandstorm.Severity > 0.4f
            && npc.Center.Y < Main.worldSurface * 16f;

        /// <summary>机制资格（每个机制入口都要过；雕像怪在此排除）</summary>
        private static bool MechEligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.boss || npc.realLife >= 0 || npc.SpawnedFromStatue) {
                return false;
            }
            return true;
        }

        /// <summary>统计某类弹幕的活动实例数（到 stopAt 提前退出；只在冷却尽头调用，非每帧）</summary>
        internal static int CountActive(int projType, int stopAt = 32) {
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == projType && ++count >= stopAt) {
                    break;
                }
            }
            return count;
        }

        /// <summary>从目标脚下向下找可站立地表，返回柱底锚点（找不到视为悬空，放弃）</summary>
        private static bool TryFindGround(Player target, out Vector2 basePos) {
            basePos = default;
            Point feet = target.Bottom.ToTileCoordinates();
            for (int dy = 0; dy < GroundSearchTiles; dy++) {
                int tileY = feet.Y + dy;
                if (!WorldGen.InWorld(feet.X, tileY, 10)) {
                    return false;
                }
                if (WorldGen.SolidTile(feet.X, tileY)) {
                    basePos = new Vector2(feet.X * 16f + 8f, tileY * 16f);
                    return true;
                }
            }
            return false;
        }

        private static int GhoulTint(int type) {
            if (type == NPCID.DesertGhoulCorruption) {
                return 1;
            }
            if (type == NPCID.DesertGhoulCrimson) {
                return 2;
            }
            return type == NPCID.DesertGhoulHallow ? 3 : 0;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            UpdateSandVeil(npc);//全端确定性模拟，须在客户端早退之前
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                return;//决策只在权威端
            }
            if (actionTimer > 0) {
                actionTimer--;
                return;
            }
            if (!MechEligible(npc) || !npc.HasValidTarget) {
                actionTimer = 60;
                return;
            }

            if (GeyserTypes.Contains(npc.type)) {
                TryGeyser(npc);
                return;
            }
            if (ConeProfileByType.TryGetValue(npc.type, out int profileId)) {
                TryCone(npc, profileId);
                return;
            }
            if (npc.type == NPCID.IceTortoise) {
                TryStomp(npc);
                return;
            }
            if (RingTypes.Contains(npc.type)) {
                TryRing(npc);
                return;
            }
            //纯死亡机制/接触风味类型无主动触发
            actionTimer = 600;
        }

        /// <summary>本个体沙隐强度 0..1（各端从同步天气确定性推得，无需同步）</summary>
        private float veil;

        /// <summary>
        /// 沙隐：沙暴里荒地怪半透明并获得微幅推进。透明度只抬不压（出生淡入等原版
        /// 自管值取更大者），退隐期把本层抬上去的余量收回；推进量过碰撞钳制（镜像 GameModeNPC 口径）
        /// </summary>
        private void UpdateSandVeil(NPC npc) {
            bool active = SandVeilActive(npc);
            if (!active && veil <= 0f) {
                return;
            }
            veil = MathHelper.Clamp(veil + (active ? 0.03f : -0.05f), 0f, 1f);
            if (veil > 0.02f) {
                npc.alpha = Math.Max(npc.alpha, (int)(veil * 130f));
                Vector2 advance = npc.velocity * (veil * 0.10f);
                if (!npc.noTileCollide) {
                    advance = Collision.TileCollision(npc.position, advance, npc.width, npc.height);
                }
                npc.position += advance;
                if (!Main.dedServ && Main.rand.NextBool(6)) {
                    Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                        DustID.Sand, npc.velocity.X * 0.3f, 0f, 140, default, 0.9f);
                    dust.noGravity = true;
                    dust.velocity *= 0.4f;
                }
            }
            else if (!active && npc.alpha > 0 && npc.alpha <= 131) {
                //退隐余量回收：只收本层可能抬上去的区间，不碰原版更高的自管值
                npc.alpha = Math.Max(0, npc.alpha - 6);
            }
        }

        /// <summary>遁地伏击：蠕虫头在地下且目标近地面时，在目标脚下锁点起一根地涌沙柱</summary>
        private void TryGeyser(NPC npc) {
            actionTimer = TriggerRetryFrames;
            Point head = npc.Center.ToTileCoordinates();
            if (!WorldGen.InWorld(head.X, head.Y, 10) || !WorldGen.SolidTile(head.X, head.Y)) {
                comboLeft = 0;//浮出地表则中断连锁
                return;
            }
            Player target = Main.player[npc.target];
            float dist = npc.Distance(target.Center);
            if (dist < GeyserMinRange || dist > GeyserMaxRange) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<WastesSandGeyserProj>()) >= GeyserCap) {
                return;
            }
            if (!TryFindGround(target, out Vector2 basePos)) {
                return;
            }

            float scale = npc.type == NPCID.DuneSplicerHead ? 1.25f : 1f;
            int damage = (int)(npc.damage * GeyserDamageFrac);
            //ai[2] 带上来源类型：槽位被新怪复用时取消检查不被骗过
            Projectile.NewProjectile(npc.GetSource_FromAI(), basePos, Vector2.Zero,
                ModContent.ProjectileType<WastesSandGeyserProj>(), damage, 2f, Main.myPlayer,
                scale, npc.whoAmI + 1, npc.type);

            //连锁：tier1 单发；tier2/3 追加 1/2 发，每发重新锁点且有完整预告
            if (comboLeft <= 0) {
                comboLeft = boundTier;
            }
            comboLeft--;
            actionTimer = comboLeft > 0 ? GeyserChainGap : GeyserCooldownByTier[boundTier - 1];
        }

        /// <summary>沙喷锥形幕：方向在预告生成帧锁死，缺口与虚影由预告实体统一保证</summary>
        private void TryCone(NPC npc, int profileId) {
            actionTimer = TriggerRetryFrames;
            Player target = Main.player[npc.target];
            WastesSandConeTelegraph.ConeProfile profile = WastesSandConeTelegraph.GetProfile(profileId);
            float dist = npc.Distance(target.Center);
            if (dist < profile.MinRange || dist > profile.MaxRange) {
                return;
            }
            if (!Collision.CanHitLine(npc.Center, 1, 1, target.Center, 1, 1)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<WastesSandConeTelegraph>()) >= ConeCap) {
                return;
            }

            //预告即承诺：原点与方向此帧锁死，此后不再重瞄
            float aim = (target.Center - npc.Center).ToRotation();
            int packed = WastesSandConeTelegraph.Pack(profileId, boundTier - 1,
                Main.rand.NextBool(), GhoulTint(npc.type));
            int damage = (int)(npc.damage * ConeDamageFrac);
            //ai[2] 低位=来源槽+1、高位=来源类型：槽位被新怪复用时取消检查不被骗过
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<WastesSandConeTelegraph>(), damage, 1f, Main.myPlayer,
                aim, packed, (npc.whoAmI + 1) | (npc.type << 8));
            actionTimer = Math.Max(120, profile.Cooldown - ConeCooldownStepPerTier * (boundTier - 1));
        }

        /// <summary>冰龟震地：在目标脚下锁点铺一片打滑区（无伤害的控制场地）</summary>
        private void TryStomp(NPC npc) {
            actionTimer = TriggerRetryFrames;
            if (npc.velocity.Y != 0f) {
                return;//落地才能震出冰面
            }
            Player target = Main.player[npc.target];
            float dist = npc.Distance(target.Center);
            if (dist < StompMinRange || dist > StompMaxRange) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<WastesIceSlickZone>()) >= ZoneCap) {
                return;
            }
            if (!TryFindGround(target, out Vector2 basePos)) {
                return;
            }

            Projectile.NewProjectile(npc.GetSource_FromAI(), basePos, Vector2.Zero,
                ModContent.ProjectileType<WastesIceSlickZone>(), 0, 0f, Main.myPlayer,
                ZoneHalfWidthByTier[boundTier - 1], ZoneActiveByTier[boundTier - 1]);
            actionTimer = StompCooldownByTier[boundTier - 1];
        }

        /// <summary>元素近身环：一体一环，环实体自行跟随宿主并在宿主消失后消散</summary>
        private void TryRing(NPC npc) {
            actionTimer = 300;//低频复查，环丢失时兜底重建
            int ringType = ModContent.ProjectileType<WastesElementRing>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.type == ringType && (int)proj.ai[0] == npc.whoAmI) {
                    return;
                }
            }
            bool ice = npc.type == NPCID.IceElemental;
            int radius = (ice ? RingRadiusIce : RingRadiusSand) + RingRadiusStepPerTier * (boundTier - 1);
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ringType, 0, 0f, Main.myPlayer, npc.whoAmI, ice ? 0f : 1f, radius);
        }

        /// <summary>死亡径向冰晶碎裂：先出无害凝晶核（≥30 帧预告），由核在提交帧放射碎片</summary>
        public override void OnKill(NPC npc) {
            if (boundTier <= 0 || Main.netMode == NetmodeID.MultiplayerClient) {
                return;
            }
            if (!ShatterTypes.Contains(npc.type) || !MechEligible(npc)) {
                return;
            }
            if (CountActive(ModContent.ProjectileType<WastesIceShatterCore>()) >= ShatterCap) {
                return;
            }

            bool small = npc.type == NPCID.IceBat;
            int count = (small ? BatShardCount : ShardCountBase) + 2 * (boundTier - 1);
            int damage = (int)(npc.damage * ShardDamageFrac);
            Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<WastesIceShatterCore>(), damage, 1f, Main.myPlayer,
                count, small ? 0.7f : 1f);
        }

        /// <summary>沙漠蝎接触毒液（类型风味；命中方本机结算，原生同步）</summary>
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo) {
            if (boundTier <= 0) {
                return;
            }
            if (npc.type != NPCID.DesertScorpionWalk && npc.type != NPCID.DesertScorpionWall) {
                return;
            }
            if (!MechEligible(npc)) {
                return;
            }
            target.AddBuff(BuffID.Poisoned, ScorpionPoisonBase + 60 * (boundTier - 1));
        }
    }
}
