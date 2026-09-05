using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Bows
{
    /// <summary>
    /// 弓·蓄力族共享框架（前困难 21 弓）：三级蓄力（半蓄/满蓄/过满）箭矢质变。<br/>
    /// 接管路径为手持弹幕（<see cref="GsChargeBowHeld"/>）：GsCanUseItem 在 owner 侧生成 held 并压掉原版射击，
    /// 松手释放时 held 恰好调一次 PickAmmo，按档位换型/打标后生成箭矢；衍生弹幕一律免费。<br/>
    /// 档位阈值按 useTime 与攻速折算（2026-09-05 提速：T1/T2/T3/失稳 = U×0.7/1.5/2.3/3.5，原 1.0/2.2/3.4/5.0）；
    /// 伤害倍率走 DPS 锚定公式（蓄力周期补偿 × DpsTarget × 族级 <see cref="ChargePower"/> × 档位奖励），
    /// 弹速/击退/穿透随档位加码——蓄得越满，箭越疾越沉。<br/>
    /// 路由约定：MarkData = 档位（0~3），MarkData2 = kind + index×1000（kind=0 为主箭，非 0 为衍生弹）。
    /// 原版弹幕自生的子弹幕（如蜂箭放蜂）经承签兜底标记为 <see cref="KindVanillaChild"/>，不再触发质变。
    /// </summary>
    internal abstract class GsChargeBowScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Bows";

        //==================== 档位与衍生弹类别常量 ====================

        /// <summary>主箭</summary>
        internal const int KindMain = 0;
        /// <summary>木弓组 T3 命中分裂木箭</summary>
        internal const int KindSplit = 1;
        /// <summary>矿弓组出膛伴射副箭</summary>
        internal const int KindSideArrow = 2;
        /// <summary>恶魔弓追魂魔焰</summary>
        internal const int KindSoulFlame = 3;
        /// <summary>血雨弓追加天矢</summary>
        internal const int KindBloodRain = 4;
        /// <summary>熔岩之怒熔浆珠</summary>
        internal const int KindMagmaBead = 5;
        /// <summary>熔岩之怒熔雨</summary>
        internal const int KindMoltenRain = 6;
        /// <summary>狱蝠 V 编队翼蝠（T2）</summary>
        internal const int KindBatWing = 7;
        /// <summary>狱蝠螺旋风暴（T3）</summary>
        internal const int KindBatSpiral = 8;
        /// <summary>原版弹幕自生子弹幕的承签兜底（不触发任何质变逻辑）</summary>
        internal const int KindVanillaChild = 99;

        //==================== 子类可调参数 ====================

        /// <summary>DPS 锚定系数（逐弓对账用）：蓄力循环 DPS ≈ 原版 × 本值 × <see cref="ChargePower"/>，余下强度由质变 rider 补足</summary>
        internal virtual float DpsTarget => 1.05f;

        /// <summary>蓄力时长整体缩放（狱蝠弓 0.85 全档提速）</summary>
        internal virtual float ChargeScale => 1f;

        /// <summary>
        /// 族级蓄力威力系数（用户裁定 2026-09-05「蓄力更快、力度更大」）：叠在 DPS 锚定之上，
        /// 蓄力射击整体比锚定值再高两成；连同档位奖励，T3 循环 DPS ≈ 原版 × DpsTarget × 1.34
        /// </summary>
        internal const float ChargePower = 1.2f;

        //==================== 档位折算 ====================

        /// <summary>搭箭相基准帧数</summary>
        internal const int NockFrames = 3;
        /// <summary>释放相基准帧数</summary>
        internal const int LooseFrames = 3;
        /// <summary>失稳疲劳附加帧（固定值，不吃攻速）</summary>
        internal const int FatigueFrames = 8;

        /// <summary>T1 半蓄阈值倍数（×U）</summary>
        internal const float Tier1Mul = 0.7f;
        /// <summary>T2 满蓄阈值倍数（×U）</summary>
        internal const float Tier2Mul = 1.5f;
        /// <summary>T3 过满阈值倍数（×U）</summary>
        internal const float Tier3Mul = 2.3f;
        /// <summary>过满窗口终点倍数（×U，失稳读秒）</summary>
        internal const float OverloadMul = 3.5f;

        /// <summary>按 useTime、攻速与族缩放折算蓄力阈值帧数</summary>
        internal int ChargeFrames(Item item, Player player, float mul) {
            float speed = player.GetWeaponAttackSpeed(item);
            if (speed <= 0f) {
                speed = 1f;
            }
            return Math.Max(1, (int)MathF.Round(Math.Max(1, item.useTime) * mul * ChargeScale / speed));
        }

        /// <summary>原版一发的周期帧数 U（T0 轻放连点向它对齐，不让快速点射白赚射速）</summary>
        internal int BaseFrames(Item item, Player player) => ChargeFrames(item, player, 1.0f);
        /// <summary>T1 半蓄阈值：U×0.7</summary>
        internal int Tier1Frames(Item item, Player player) => ChargeFrames(item, player, Tier1Mul);
        /// <summary>T2 满蓄阈值：U×1.5</summary>
        internal int Tier2Frames(Item item, Player player) => ChargeFrames(item, player, Tier2Mul);
        /// <summary>T3 过满阈值：U×2.3</summary>
        internal int Tier3Frames(Item item, Player player) => ChargeFrames(item, player, Tier3Mul);
        /// <summary>过满窗口终点（失稳读秒）：U×3.5</summary>
        internal int OverloadFrames(Item item, Player player) => ChargeFrames(item, player, OverloadMul);

        /// <summary>
        /// 档位伤害倍率（DPS 锚定 × 族级威力）：Mul(T) = (U×阈值倍数 + 6) / U × DpsTarget × ChargePower × 档位奖励，
        /// +6 为搭箭与释放开销，档位奖励 T1 1.0 / T2 1.05 / T3 1.12（越满越值）。T0 轻放 0.85 保底。<br/>
        /// 以 U=28、D=1.05 为例：T1 ≈1.15（周期 25.6f）、T2 ≈2.27（48f）、T3 ≈3.55（70.4f），
        /// 循环 DPS 分别约为原版 1.26 / 1.32 / 1.41 倍
        /// </summary>
        internal float TierDamageMul(Item item, int tier) {
            if (tier <= 0) {
                return 0.85f;
            }
            float u = Math.Max(1, item.useTime) * ChargeScale;
            float thresholdMul = tier switch { 1 => Tier1Mul, 2 => Tier2Mul, _ => Tier3Mul };
            float bonus = tier switch { 1 => 1f, 2 => 1.05f, _ => 1.12f };
            return (u * thresholdMul + NockFrames + LooseFrames) / u * DpsTarget * ChargePower * bonus;
        }

        /// <summary>档位初速倍率：弦愈满箭愈疾（过满近乎倍速，配合穿透读作贯穿箭）</summary>
        internal static float TierSpeedMul(int tier) => tier switch {
            1 => 1.25f,
            2 => 1.45f,
            3 => 1.7f,
            _ => 1f,
        };

        /// <summary>档位击退倍率：满蓄以上的箭把敌人真正推开</summary>
        internal static float TierKnockbackMul(int tier) => tier switch {
            1 => 1.15f,
            2 => 1.5f,
            3 => 2f,
            _ => 1f,
        };

        //==================== 发射管线（held 调用，全在 owner 端） ====================

        /// <summary>按档位置换弹种（特色弓的木箭转化也在这复刻，原版转化被 held 接管绕过）</summary>
        internal virtual int TransformShootType(int pickedType, int tier) => pickedType;

        /// <summary>该档位与弹种是否由 <see cref="OnLoose"/> 全权生成（跳过默认主箭）</summary>
        internal virtual bool CustomLoose(int tier, int shootType) => false;

        /// <summary>释放追加钩子：出膛伴射/编队/天矢等在这生成（内部自行 StampNext）；damage 已含档位倍率</summary>
        internal virtual void OnLoose(Player player, Item item, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback, int tier) { }

        //==================== 打标编码（pending 由发射/衍生路径写，回调同栈消费） ====================

        private int pendingTier;
        private int pendingKind;

        /// <summary>下一发 NewProjectile 的档位与类别戳（owner 端生成前调用，OnSpawn 回调同栈消费）</summary>
        internal void StampNext(int tier, int kind) {
            pendingTier = tier;
            pendingKind = kind;
        }

        /// <summary>清空戳（发射流程收尾调用，防 NewProjectile 满员未消费的残留污染下一发）</summary>
        internal void ClearStamp() {
            pendingTier = 0;
            pendingKind = 0;
        }

        public sealed override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            router.MarkData = pendingTier;
            router.MarkData2 = pendingKind;
            int kind = pendingKind;
            pendingKind = 0;
            OnArrowSpawned(proj, router, pendingTier, DecodeKind(kind));
        }

        public sealed override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            if (pendingKind != 0) {
                //族内命中衍生：消费戳
                router.MarkData2 = pendingKind;
                int kind = pendingKind;
                pendingKind = 0;
                OnArrowSpawned(proj, router, (int)router.MarkData, DecodeKind(kind));
                return;
            }
            //原版弹幕自生子弹幕（蜂箭放蜂等）：兜底降级，不触发质变
            router.MarkData2 = KindVanillaChild;
        }

        /// <summary>解出类别（去掉 index×1000 编码）</summary>
        internal static int DecodeKind(float markData2) => (int)markData2 % 1000;

        /// <summary>解出编队索引</summary>
        internal static int DecodeIndex(float markData2) => (int)markData2 / 1000;

        /// <summary>
        /// 箭矢出生窗口（生成端，先于生成包发出）。默认：满蓄主箭穿透 +1、过满 +2（带 >0 守卫，-1 无限穿禁碰）。
        /// 子类覆写时按需调用基类
        /// </summary>
        internal virtual void OnArrowSpawned(Projectile proj, GodSmithProjRouter router, int tier, int kind) {
            if (kind == KindMain && tier >= 2 && proj.penetrate > 0) {
                proj.penetrate += tier >= 3 ? 2 : 1;
            }
        }

        //==================== 路由回调分发（族内虚钩子） ====================

        public sealed override void GsProjPostAI(Projectile proj, GodSmithProjRouter router)
            => ArrowPostAI(proj, router, (int)router.MarkData, DecodeKind(router.MarkData2));

        /// <summary>族内 AI 追加钩子（各端都跑：权威改动守 owner）</summary>
        internal virtual void ArrowPostAI(Projectile proj, GodSmithProjRouter router, int tier, int kind) { }

        public sealed override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router)
            => ModifyArrowHit(proj, target, ref modifiers, (int)router.MarkData, DecodeKind(router.MarkData2));

        /// <summary>族内命中修饰钩子（攻击方端）</summary>
        internal virtual void ModifyArrowHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, int tier, int kind) { }

        public sealed override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            int tier = (int)router.MarkData;
            int kind = DecodeKind(router.MarkData2);
            if (kind == KindVanillaChild) {
                return;
            }
            if (kind != KindMain) {
                OnRiderHit(proj, target, hit, damageDone, router, tier, kind);
                return;
            }
            if (tier >= 2) {
                OnQualityHit(proj, target, hit, damageDone, router, tier);
            }
        }

        /// <summary>T2+ 主箭命中（攻击方端）：质变命中效果与 T3 rider 在这实现</summary>
        internal virtual void OnQualityHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier) { }

        /// <summary>衍生弹命中（攻击方端）：kind 非 0 的分支，默认无事</summary>
        internal virtual void OnRiderHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router, int tier, int kind) { }

        //==================== 使用接管 ====================

        public sealed override bool? GsCanUseItem(Item item, Player player) {
            //手持在场即蓄力中；生成只在 owner 端，且没箭不拉弓
            if (!HeldAlive<GsChargeBowHeld>(player) && player.whoAmI == Main.myPlayer
                && player.GetShootState().HasAmmo) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsChargeBowHeld>(), 1, 0f, player.whoAmI);
            }
            //全端压掉原版射击，远端靠 held 弹幕同步看到拉弓
            return false;
        }

        //==================== 通用小工具（owner 端命中钩子内使用） ====================

        /// <summary>目标是否为有效的 rider 结算对象（排除假人/雕像怪/友方）</summary>
        internal static bool ValidRiderTarget(NPC target)
            => target.active && !target.friendly && target.lifeMax > 5
               && target.type != NPCID.TargetDummy && !target.SpawnedFromStatue;

        /// <summary>找 range 内最近的可追击敌人（确定性，不掷随机；exclude 为排除目标）</summary>
        internal static NPC FindNearestEnemy(Vector2 center, float range, Projectile proj, int excludeWhoAmI = -1) {
            NPC best = null;
            float bestSq = range * range;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.whoAmI == excludeWhoAmI || !npc.CanBeChasedBy(proj)) {
                    continue;
                }
                float distSq = Vector2.DistanceSquared(center, npc.Center);
                if (distSq < bestSq) {
                    bestSq = distSq;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>owner 端小范围溅射（矿弓淬火等）：对 range 内其他敌人直接结算 ratio 倍伤害</summary>
        internal static void SplashDamage(Player player, Projectile proj, NPC center, float range, int damage) {
            if (damage <= 0) {
                return;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.whoAmI == center.whoAmI || npc.friendly || npc.dontTakeDamage
                    || !npc.CanBeChasedBy(proj)) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, center.Center) > range * range) {
                    continue;
                }
                player.ApplyDamageToNPC(npc, damage, 0f, 0, false);
            }
        }
    }
}
