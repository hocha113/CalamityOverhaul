using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Creative;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 档位数值表：通用增强与修罗强化的全部可调常量收拢在此，机制文件不散写数字。
    /// 档位口径见 <see cref="GameModeSystem.EffectiveTier"/>：1 残酷 / 2 修罗 / 3 毁灭（传奇世界的修罗）
    /// </summary>
    internal static class GameModeTuning
    {
        //——全体敌对 NPC：血量与伤害倍率（SetDefaults 时绑定，Boss 与重制 Boss 一并计入）——
        private const float BrutalStatMult = 1.5f;
        private const float AsuraStatMult = 2.0f;
        private const float AnnihilationStatMult = 3.0f;

        //——仅非 Boss：提速（PostAI 位置推进系数）——
        private const float BrutalSpeedBonus = 0.45f;
        private const float AsuraSpeedBonus = 0.60f;
        private const float AnnihilationSpeedBonus = 0.8f;

        //——仅非 Boss 常态狂暴：击退抗性衰减（乘在 knockBackResist 上，越小越推不动）——
        private const float BrutalKnockbackMult = 0.6f;
        private const float AsuraKnockbackMult = 0.35f;
        private const float AnnihilationKnockbackMult = 0.1f;

        //——仅非 Boss 常态狂暴：接触伤害追加（在全局属性增幅之上）——
        private const float BrutalContactMult = 1.10f;
        private const float AsuraContactMult = 1.20f;
        private const float AnnihilationContactMult = 1.35f;

        //——毁灭下的修罗机制强化——

        /// <summary>毁灭下敌怪每次同类命中积累的免疫层数（其余档位 1 层）</summary>
        public const float AnnihilationAdaptStacksPerHit = 2f;
        /// <summary>毁灭下伤害下限相对最近命中的倍率（其余档位 1 倍）</summary>
        public const float AnnihilationFloorMult = 2f;

        //——修罗：近战是适应的裂隙——

        /// <summary>真近战（物品挥击与刀刃本体弹幕）承受的适应减伤比例</summary>
        public const float AsuraTrueMeleeAdaptTaken = 0.4f;
        /// <summary>其余近战类弹幕（剑气等）承受的适应减伤比例</summary>
        public const float AsuraMeleeProjAdaptTaken = 0.7f;
        /// <summary>贴身增幅上限：贴着目标碰撞箱出手时的额外伤害比例</summary>
        public const float AsuraCloseRangeMaxBonus = 0.35f;
        /// <summary>贴身增幅满额距离（像素，玩家中心到目标碰撞箱最近点）</summary>
        public const float AsuraCloseRangeFullDist = 120f;
        /// <summary>贴身增幅归零距离（像素），由满额线性衰减到此为止</summary>
        public const float AsuraCloseRangeZeroDist = 400f;

        /// <summary>档位血量伤害倍率</summary>
        public static float StatMult(int tier) => tier switch {
            1 => BrutalStatMult,
            2 => AsuraStatMult,
            3 => AnnihilationStatMult,
            _ => 1f,
        };

        /// <summary>档位提速系数（仅非 Boss）</summary>
        public static float SpeedBonus(int tier) => tier switch {
            1 => BrutalSpeedBonus,
            2 => AsuraSpeedBonus,
            3 => AnnihilationSpeedBonus,
            _ => 0f,
        };

        /// <summary>档位击退抗性衰减（仅非 Boss）</summary>
        public static float KnockbackMult(int tier) => tier switch {
            1 => BrutalKnockbackMult,
            2 => AsuraKnockbackMult,
            3 => AnnihilationKnockbackMult,
            _ => 1f,
        };

        /// <summary>档位接触伤害追加（仅非 Boss）</summary>
        public static float ContactMult(int tier) => tier switch {
            1 => BrutalContactMult,
            2 => AsuraContactMult,
            3 => AnnihilationContactMult,
            _ => 1f,
        };

        //——重制 Boss 的大师基线锚定——
        //重制 Boss 的血量/伤害系数写在各自 SetProperty，乘在"当前世界"的原版缩放之上。
        //世界低于大师（经典/旅途低强度/专家）时原版系数不足，会出现"开残酷反而比原版
        //大师血少"。此处按原版口径精确补足：补偿 × 世界系数 == 大师系数，使重制系数永远
        //乘在大师基线上，全难度强度一致。档位增幅（StatMult）另行乘在这个底之上，
        //与其余敌人同等对待——锚定只管补齐地板，不替代增幅。
        //数据转录自原版 NPC.ScaleStats：ApplyGameMode（专家 ×2 / 大师 ×3 / FTW 再 +1）
        //与 ApplyMultiplayerStats（各 Boss 专属系数、大师 bossAdjustment 0.85）。
        //多人 balance 两侧同源，比值中恒约去，故不参与计算；防御的小额修正不锚定

        /// <summary>单个 Boss 的原版专属缩放：血量系数、伤害系数、是否吃大师 0.85 调整</summary>
        private readonly struct VanillaBossScale(float life, float damage, bool bossAdj)
        {
            public readonly float Life = life;
            public readonly float Damage = damage;
            public readonly bool BossAdj = bossAdj;
        }

        /// <summary>
        /// 需要锚定的重制 Boss 及其原版专属系数
        /// 不在表内的接管类型（蜂群/探测怪/孢子/泡泡等战斗物）血量由战斗代码自管，不锚定
        /// </summary>
        private static readonly Dictionary<int, VanillaBossScale> AnchorRows = new() {
            [NPCID.KingSlime] = new(0.7f, 0.8f, true),
            [NPCID.EyeofCthulhu] = new(0.65f, 1f, true),
            [NPCID.EaterofWorldsHead] = new(0.7f, 1.1f, true),
            [NPCID.EaterofWorldsBody] = new(0.7f, 0.8f, true),
            [NPCID.EaterofWorldsTail] = new(0.7f, 0.8f, true),
            [NPCID.BrainofCthulhu] = new(0.85f, 0.9f, true),
            [NPCID.Creeper] = new(0.85f, 0.9f, true),
            [NPCID.QueenBee] = new(0.7f, 0.9f, true),
            [NPCID.SkeletronHead] = new(1f, 1.1f, true),
            [NPCID.SkeletronHand] = new(1.3f, 1.1f, true),
            [NPCID.Deerclops] = new(0.85f, 1f, true),
            [NPCID.WallofFlesh] = new(0.7f, 1.5f, true),
            [NPCID.WallofFleshEye] = new(0.7f, 1.5f, true),
            [NPCID.TheHungry] = new(0.7f, 1f, false),
            [NPCID.QueenSlimeBoss] = new(0.8f, 1f, true),
            [NPCID.QueenSlimeMinionBlue] = new(0.75f, 1f, true),
            [NPCID.QueenSlimeMinionPink] = new(0.75f, 1f, true),
            [NPCID.QueenSlimeMinionPurple] = new(0.75f, 1f, true),
            [NPCID.Retinazer] = new(0.75f, 0.85f, true),
            [NPCID.Spazmatism] = new(0.75f, 0.85f, true),
            [NPCID.TheDestroyer] = new(0.75f, 2f, true),
            [NPCID.TheDestroyerBody] = new(0.75f, 0.85f, true),
            [NPCID.TheDestroyerTail] = new(0.75f, 0.85f, true),
            [NPCID.SkeletronPrime] = new(0.75f, 0.85f, true),
            [NPCID.PrimeCannon] = new(0.75f, 0.85f, true),
            [NPCID.PrimeSaw] = new(0.75f, 0.85f, true),
            [NPCID.PrimeVice] = new(0.75f, 0.85f, true),
            [NPCID.PrimeLaser] = new(0.75f, 0.85f, true),
            [NPCID.Plantera] = new(0.7f, 1.15f, true),
            [NPCID.PlanterasHook] = new(1f, 1f, false),
            [NPCID.PlanterasTentacle] = new(1f, 1.15f, true),
            [NPCID.Golem] = new(0.75f, 0.8f, true),
            [NPCID.GolemHead] = new(0.75f, 0.8f, true),
            [NPCID.GolemHeadFree] = new(0.75f, 0.8f, true),
            [NPCID.GolemFistLeft] = new(0.75f, 0.8f, true),
            [NPCID.GolemFistRight] = new(0.75f, 0.8f, true),
            [NPCID.DukeFishron] = new(0.65f, 0.7f, true),
            [NPCID.MoonLordHead] = new(0.75f, 0.75f, true),
            [NPCID.MoonLordHand] = new(0.75f, 0.75f, true),
            [NPCID.MoonLordCore] = new(0.75f, 0.75f, true),
            [NPCID.MoonLordFreeEye] = new(1f, 1f, false),
            [NPCID.MoonLordLeechBlob] = new(1f, 1f, false),
        };

        /// <summary>一次难度取值：档位倍率、旅途强度、专家层是否生效、大师调整是否生效、FTW 大师追加伤害</summary>
        private readonly struct DifficultyContext(float modeLifeMult, float modeDamageMult, float strengthMult,
            bool expertLayer, bool masterAdj, bool ftwExtraDamage)
        {
            public readonly float ModeLifeMult = modeLifeMult;
            public readonly float ModeDamageMult = modeDamageMult;
            public readonly float StrengthMult = strengthMult;
            public readonly bool ExpertLayer = expertLayer;
            public readonly bool MasterAdj = masterAdj;
            public readonly bool FtwExtraDamage = ftwExtraDamage;
        }

        /// <summary>当前世界的难度语境，镜像原版 ScaleStats + NPCStrengthHelper 的判定口径</summary>
        private static DifficultyContext ResolveWorldContext() {
            GameModeData data = Main.GameModeInfo;
            bool ftw = Main.getGoodWorld;

            //旅途：敌怪强度滑条承担缩放，FTW 再 +1（镜像原版 ScaleStats 开头）
            float strength = 1f;
            if (data.IsJourneyMode) {
                CreativePowers.DifficultySliderPower power =
                    CreativePowerManager.Instance.GetPower<CreativePowers.DifficultySliderPower>();
                if (power != null && power.GetIsUnlocked()) {
                    strength = power.StrengthMultiplierToGiveNPCs;
                }
                if (ftw) {
                    strength += 1f;
                }
            }

            //非旅途的 FTW 把档位倍率 +1（镜像 ApplyGameMode 的 num2）
            int ftwModeBonus = !data.IsJourneyMode && ftw ? 1 : 0;

            //NPCStrengthHelper 口径：难度层级 = 1 + 专家 + 大师 + FTW，滑条可直接顶层级
            float difficulty = 1f + (data.IsExpertMode ? 1f : 0f) + (data.IsMasterMode ? 1f : 0f) + (ftw ? 1f : 0f);
            bool expertLayer = strength >= 2f || difficulty >= 2f;
            bool masterAdj = strength >= 3f || difficulty >= 3f;
            bool ftwExtraDamage = ftw && (strength >= 4f || difficulty >= 4f);

            return new(data.EnemyMaxLifeMultiplier + ftwModeBonus, data.EnemyDamageMultiplier + ftwModeBonus,
                strength, expertLayer, masterAdj, ftwExtraDamage);
        }

        /// <summary>大师基线语境（锚定目标）：大师档全系数，FTW 同步计入保持两侧对称</summary>
        private static DifficultyContext ResolveMasterContext() {
            bool ftw = Main.getGoodWorld;
            int ftwModeBonus = ftw ? 1 : 0;
            //大师 + FTW 难度层级为 4，触发原版的 FTW 追加伤害
            return new(3f + ftwModeBonus, 3f + ftwModeBonus, 1f, true, true, ftw);
        }

        /// <summary>语境下该 Boss 的血量总系数（镜像原版乘算链）</summary>
        private static float LifeFactor(in DifficultyContext ctx, in VanillaBossScale row) {
            float factor = ctx.ModeLifeMult * ctx.StrengthMult;
            if (ctx.ExpertLayer) {
                factor *= row.Life;
                if (ctx.MasterAdj && row.BossAdj) {
                    factor *= 0.85f;
                }
            }
            return factor;
        }

        /// <summary>语境下该 Boss 的伤害总系数（镜像原版乘算链）</summary>
        private static float DamageFactor(in DifficultyContext ctx, in VanillaBossScale row) {
            float factor = ctx.ModeDamageMult * ctx.StrengthMult;
            if (ctx.ExpertLayer) {
                factor *= row.Damage;
            }
            if (ctx.FtwExtraDamage) {
                factor *= 4f / 3f;
            }
            return factor;
        }

        /// <summary>
        /// 重制 Boss 的大师基线补偿系数（血量, 伤害）
        /// 世界缩放已达大师时恒为 1；不在锚定表内的类型恒为 1
        /// 在 SetDefaults 期乘上后，原版缩放跑完即恰好落在大师基线，档位增幅再乘于其上
        /// </summary>
        public static (float life, float damage) MasterAnchorCompensation(int npcType) {
            if (!AnchorRows.TryGetValue(npcType, out VanillaBossScale row)) {
                return (1f, 1f);
            }
            DifficultyContext world = ResolveWorldContext();
            DifficultyContext master = ResolveMasterContext();
            float life = Math.Max(1f, LifeFactor(master, row) / LifeFactor(world, row));
            float damage = Math.Max(1f, DamageFactor(master, row) / DamageFactor(world, row));
            return (life, damage);
        }
    }
}
