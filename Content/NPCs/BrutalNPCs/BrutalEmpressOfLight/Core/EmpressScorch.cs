using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Rendering;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEmpressOfLight.Core
{
    /// <summary>女皇攻击弹幕自报身份：灼痕档、是否光束类（短无敌帧）、命中反馈强度</summary>
    internal interface IEmpressAttack
    {
        EmpressScorchTier ScorchTier { get; }
        /// <summary>光束类：无敌帧压到 30，持续接触会二次判定</summary>
        bool BeamType => false;
        /// <summary>命中链强度 0.4~1.5</summary>
        float FeedbackIntensity => 0.8f;
    }

    /// <summary>
    /// 灼痕：昼形态被女皇任何攻击命中即上，期间再被命中额外扣 50% 最大生命并刷新。
    /// 图标复用原版破晓（Buff_189），不存档，护士不可解
    /// </summary>
    internal class EmpressScorchBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Daybreak;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = false;
            BuffID.Sets.NurseCannotRemoveDebuff[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.GetModPlayer<EmpressScorchPlayer>().ScorchedThisFrame = true;
            //身上带灼痕的人自己发光，队友一眼看出谁在危险线上
            Lighting.AddLight(player.Center, new Vector3(1f, 0.85f, 0.6f) * 0.55f);
        }
    }

    /// <summary>
    /// 灼痕玩家端：命中来源识别、二次命中重罚、无敌帧规约、命中链触发。
    /// Player.Hurt 在受伤者本端结算，buff 走原版同步，无需自定义包
    /// </summary>
    internal class EmpressScorchPlayer : ModPlayer
    {
        /// <summary>本帧 buff 在身（buff.Update 置位，PostUpdate 清）</summary>
        public bool ScorchedThisFrame;
        /// <summary>灼痕重罚占最大生命比例</summary>
        public const float RepeatHitFraction = 0.5f;

        public static int ScorchTicks(EmpressScorchTier tier) => tier switch {
            EmpressScorchTier.Beam => 30 * 60,
            EmpressScorchTier.Grab => 25 * 60,
            EmpressScorchTier.Heavy => 22 * 60,
            EmpressScorchTier.Medium => 16 * 60,
            _ => 8 * 60,
        };

        /// <summary>命中来源是否女皇攻击；输出灼痕档、光束类、反馈强度、来源朝向</summary>
        internal static bool TryResolve(PlayerDeathReason source, Player victim,
            out EmpressScorchTier tier, out bool beamType, out float intensity, out Vector2 dir, out Vector2 sourcePos) {
            tier = EmpressScorchTier.Light;
            beamType = false;
            intensity = 0.8f;
            dir = Vector2.UnitY;
            sourcePos = victim.Center;
            if (source == null) {
                return false;
            }
            if (source.SourceProjectileLocalIndex >= 0 && source.SourceProjectileLocalIndex < Main.maxProjectiles) {
                Projectile proj = Main.projectile[source.SourceProjectileLocalIndex];
                if (proj.active && proj.ModProjectile is IEmpressAttack attack) {
                    tier = attack.ScorchTier;
                    beamType = attack.BeamType;
                    intensity = attack.FeedbackIntensity;
                    sourcePos = proj.Center;
                    dir = proj.velocity.LengthSquared() > 0.01f ? proj.velocity.SafeNormalize(Vector2.UnitY) : proj.DirectionTo(victim.Center);
                    return true;
                }
                return false;
            }
            if (source.SourceNPCIndex >= 0 && source.SourceNPCIndex < Main.maxNPCs) {
                NPC npc = Main.npc[source.SourceNPCIndex];
                if (!npc.active || npc.type != NPCID.HallowBoss) {
                    return false;
                }
                //接触：冲刺态按投技档，其余轻档
                bool dashing = (int)npc.ai[2] == (int)EmpressStateIndex.DashGrab;
                tier = dashing ? EmpressScorchTier.Grab : EmpressScorchTier.Light;
                intensity = dashing ? 1.5f : 0.4f;
                sourcePos = npc.Center;
                dir = npc.velocity.LengthSquared() > 0.01f ? npc.velocity.SafeNormalize(Vector2.UnitY) : npc.DirectionTo(victim.Center);
                return true;
            }
            return false;
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (!NPC.ShouldEmpressBeEnraged()) {
                return;
            }
            if (!TryResolve(modifiers.DamageSource, Player, out _, out _, out _, out _, out _)) {
                return;
            }
            if (!Player.HasBuff<EmpressScorchBuff>()) {
                return;
            }
            //灼痕期间的第二击：额外扣半管，不走防御与减伤
            modifiers.FinalDamage.Flat += Player.statLifeMax2 * RepeatHitFraction;
            modifiers.DisableSound();
            if (Player.whoAmI == Main.myPlayer) {
                EmpressHitFeedback.RepeatHitCue(Player.Center);
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (!TryResolve(info.DamageSource, Player, out EmpressScorchTier tier, out bool beamType,
                out float intensity, out Vector2 dir, out Vector2 sourcePos)) {
                return;
            }

            if (NPC.ShouldEmpressBeEnraged()) {
                int ticks = ScorchTicks(tier);
                int index = Player.FindBuffIndex(ModContent.BuffType<EmpressScorchBuff>());
                if (index >= 0) {
                    Player.buffTime[index] = Math.Max(Player.buffTime[index], ticks);
                }
                else {
                    Player.AddBuff(ModContent.BuffType<EmpressScorchBuff>(), ticks);
                }
            }

            //无敌帧规约：普通攻击保 60，光束类压到 30（持续接触会二次判定，这是绝对禁区的实现）
            if (beamType) {
                Player.immuneTime = Math.Min(Player.immuneTime, 30);
                for (int i = 0; i < Player.hurtCooldowns.Length; i++) {
                    Player.hurtCooldowns[i] = Math.Min(Player.hurtCooldowns[i], 30);
                }
            }
            else {
                Player.immune = true;
                Player.immuneTime = Math.Max(Player.immuneTime, 60);
                if (info.CooldownCounter >= 0 && info.CooldownCounter < Player.hurtCooldowns.Length) {
                    Player.hurtCooldowns[info.CooldownCounter] = Math.Max(Player.hurtCooldowns[info.CooldownCounter], 60);
                }
            }

            if (Player.whoAmI == Main.myPlayer) {
                EmpressHitFeedback.Trigger(Player.Center, dir, intensity, NPC.ShouldEmpressBeEnraged());
            }
        }

        public override void PostUpdate() {
            ScorchedThisFrame = false;
        }
    }
}
