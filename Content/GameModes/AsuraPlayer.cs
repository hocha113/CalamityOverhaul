using System;
using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 修罗模式：伤害下限镜像。
    /// 记录玩家最近一次对敌命中让敌人真实失去的生命；来自敌怪或敌对弹幕的受击
    /// 最终伤害不会低于该值（环境伤害如岩浆、摔落不受影响）。
    /// 记的是真实痛苦而不是结算数字：tML 的 damageDone 不按剩余血量截断，
    /// 即死命中更是把整条命当伤害返回，这两种都不是玩家打出来的数字，不入账。
    /// 记录只代表"刚才"：脱手过保鲜期后线性消退到零（数值见 <see cref="GameModeTuning"/>），
    /// 持续交战时每次命中都刷新，不会被几分钟前的一击索命。
    /// 训练靶不入账；进入世界、复活、修罗关闭期间出手都清账。
    /// 下限不越过其他系统经 <see cref="Player.HurtModifiers.SetMaxDamage"/> 声明的上限
    /// （投技留一口气、教程血量护栏），排在修罗之后声明的走 <see cref="CapHurt"/>。
    /// 命中与受伤判定都在本机进行，状态无需网络同步
    /// </summary>
    internal class AsuraPlayer : ModPlayer
    {
        /// <summary>最近一次对敌命中让敌人真实失去的生命（未计消退）；0 = 尚未出手</summary>
        private int recordedPain;
        /// <summary>上一次入账的帧</summary>
        private uint recordedTick;

        /// <summary>本次受击其他系统经 <see cref="CapHurt"/> 声明的伤害上限，下限委托消费后复位</summary>
        private int hurtCeiling = int.MaxValue;

        /// <summary>
        /// tML 把 SetMaxDamage 的上限存在私有字段里，钳制在 GetDamage 内完成、先于 ModifyHurtInfo 委托，
        /// 下限若不读它就会把别人的上限顶穿。字段缺失（上游改名）时退化为不设上限，与旧行为一致
        /// </summary>
        private static readonly FieldInfo DamageLimitField = typeof(Player.HurtModifiers)
            .GetField("_damageLimit", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>按消退折算的当前记录：保鲜期内原值，之后线性消退，退尽即清账</summary>
        internal int EffectivePain {
            get {
                if (recordedPain <= 0) {
                    return 0;
                }
                uint elapsed = Main.GameUpdateCount - recordedTick;
                uint grace = (uint)GameModeTuning.AsuraFloorGraceTicks;
                if (elapsed <= grace) {
                    return recordedPain;
                }
                float fade = (elapsed - grace) / (float)GameModeTuning.AsuraFloorFadeTicks;
                if (fade >= 1f) {
                    recordedPain = 0;
                    return 0;
                }
                return (int)(recordedPain * (1f - fade));
            }
        }

        public override void OnEnterWorld() => recordedPain = 0;

        public override void OnRespawn() => recordedPain = 0;

        public override void ResetEffects() => hurtCeiling = int.MaxValue;

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => RecordHit(target, in hit, damageDone);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
            => RecordHit(target, in hit, damageDone);

        private void RecordHit(NPC target, in NPC.HitInfo hit, int damageDone) {
            if (!GameModeSystem.AsuraActive) {
                //修罗关闭期间的出手不立死约，重开后从第一击重新记
                recordedPain = 0;
                return;
            }
            //友方与训练靶不计入：打靶不该给自己立死约。原版假人 immortal，灾厄超级假人靠回血活着，按类型认
            if (target.friendly || target.immortal || IsTrainingDummy(target)) {
                return;
            }
            //即死的语义是绕过一切把它杀掉，StrikeNPC 会把整条命当 damageDone 返回，且不显示数字
            if (hit.InstantKill) {
                return;
            }
            //StrikeNPC 不按剩余血量截断；命中后 life 已扣（部件已同步池主值），负值就是敌人并未承受的溢出
            int pain = damageDone + Math.Min(0, target.life);
            if (pain <= 0) {
                return;
            }
            recordedPain = pain;
            recordedTick = Main.GameUpdateCount;
        }

        private static bool IsTrainingDummy(NPC target) {
            int superDummy = CWRRef.Has ? CWRID.NPC_SuperDummyNPC : 0;
            return superDummy > 0 && target.type == superDummy;
        }

        /// <summary>
        /// 需要在修罗下仍然生效的伤害上限走这里，而不是直接 <c>modifiers.SetMaxDamage</c>：
        /// 上限钳制先于 ModifyHurtInfo 执行，下限委托不知情就会把它顶穿。
        /// 顺序无关：无论声明方的 ModifyHurt 排在修罗之前还是之后，下限都在 ToHurtInfo 时才读
        /// </summary>
        internal static void CapHurt(Player player, ref Player.HurtModifiers modifiers, int limit) {
            modifiers.SetMaxDamage(limit);
            AsuraPlayer asura = player.GetModPlayer<AsuraPlayer>();
            asura.hurtCeiling = Math.Min(asura.hurtCeiling, Math.Max(limit, 1));
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!GameModeSystem.AsuraActive || modifiers.PvP) {
                return;
            }
            int pain = EffectivePain;
            if (pain <= 1) {
                return;
            }
            if (modifiers.DamageSource == null
                || !modifiers.DamageSource.TryGetCausingEntity(out var source)) {
                return;
            }
            bool fromEnemy = source is NPC { friendly: false } || source is Projectile { hostile: true };
            if (!fromEnemy) {
                return;
            }

            //毁灭下苦痛双倍奉还
            float floorMult = GameModeSystem.AnnihilationActive ? GameModeTuning.AnnihilationFloorMult : 1f;
            int floor = (int)(pain * floorMult);
            //此前阶段（NPC/弹幕钩子与更早的 ModPlayer）已声明的上限在这里读一次，
            //排在修罗之后的声明走 CapHurt，两路在委托里取小
            int declaredLimit = ReadDamageLimit(modifiers);
            modifiers.ModifyHurtInfo += (ref Player.HurtInfo info) => {
                int ceiling = Math.Min(declaredLimit, hurtCeiling);
                hurtCeiling = int.MaxValue;
                int cap = Math.Min(floor, ceiling);
                if (info.Damage < cap) {
                    info.Damage = cap;
                }
            };
        }

        private static int ReadDamageLimit(Player.HurtModifiers modifiers)
            => DamageLimitField?.GetValue(modifiers) is int limit ? limit : int.MaxValue;
    }
}
