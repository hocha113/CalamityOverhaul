using CalamityOverhaul.Content.NPCs;
using InnoVault.GameSystem;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 游戏模式的通用敌怪增强，作用于没有 Brutal AI 重制的敌人。
    /// 属性缩放在 <see cref="SetDefaults"/> 绑定：世界旗标已全端同步，两端确定性执行零网络，
    /// 切换模式只影响此后生成的个体（与 AI 覆盖同语义）。
    /// Brutal 重制类型不吃通用增幅，改走大师基线锚定（<see cref="GameModeTuning.MasterAnchorCompensation"/>），
    /// 保证重制 Boss 在任何世界难度下都不低于原版大师的血量伤害。
    /// 提速与常态狂暴只作用于非 Boss，保护 Boss 招式编排的可读性
    /// </summary>
    internal class GameModeNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>本个体生成时绑定的档位，0 = 未增强</summary>
        private int boundTier;

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }

            if (GameModeNPCLoader.BrutalOverriddenTypes.Contains(npc.type)) {
                ApplyMasterAnchor(npc);
                return;
            }

            if (!Eligible(npc)) {
                return;
            }
            boundTier = tier;

            float statMult = GameModeTuning.StatMult(tier);
            npc.lifeMax = (int)(npc.lifeMax * statMult);
            npc.damage = (int)(npc.damage * statMult);
            if (RageEligible(npc)) {
                //常态狂暴：越来越推不动
                npc.knockBackResist *= GameModeTuning.KnockbackMult(tier);
            }
            //恢复 SetDefaults 不变量；顺序无关（原版 ScaleStats 是乘法，先后可交换）
            npc.life = npc.lifeMax;
            npc.defDamage = npc.damage;
            npc.defDefense = npc.defense;
        }

        /// <summary>
        /// 重制 Boss 的大师基线补偿：世界难度不足大师时把血量伤害补足到大师基线，
        /// 重制自身的系数（各 SetProperty 里的乘法）永远乘在这个底上。
        /// 此钩子先于原版 ScaleStats 执行，补偿 × 世界缩放 == 大师缩放，乘法可交换故顺序无关
        /// </summary>
        private static void ApplyMasterAnchor(NPC npc) {
            (float lifeComp, float damageComp) = GameModeTuning.MasterAnchorCompensation(npc.type);
            if (lifeComp > 1f) {
                npc.lifeMax = (int)(npc.lifeMax * lifeComp);
                npc.life = npc.lifeMax;
            }
            if (damageComp > 1f && npc.damage > 0) {
                npc.damage = (int)(npc.damage * damageComp);
                npc.defDamage = npc.damage;
            }
        }

        /// <summary>通用增强资格：敌对、非小动物、非假人（重制类型已在上游分流）</summary>
        private static bool Eligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage) {
                return false;
            }
            //镜像原版 ScaleStats 的跳过口径：小动物与零接触伤害载体不参与
            return npc.lifeMax > 5 && npc.damage > 0;
        }

        /// <summary>提速与狂暴资格：非 Boss，且不是共享血池的体节（避免蠕虫链被推散）</summary>
        private static bool RageEligible(NPC npc) => !npc.boss && npc.realLife < 0;

        public override void PostAI(NPC npc) {
            if (boundTier <= 0 || !RageEligible(npc)) {
                return;
            }

            //提速：位置推进。两端本地同跑 AI，模拟一致。
            //吃物块碰撞的个体（史莱姆/僵尸等）必须把推进量过一遍碰撞钳制，
            //否则额外位移会把它们推进墙里；穿墙类（noTileCollide）保持原样
            Vector2 advance = npc.velocity * GameModeTuning.SpeedBonus(boundTier);
            if (!npc.noTileCollide) {
                advance = Collision.TileCollision(npc.position, advance, npc.width, npc.height);
            }
            npc.position += advance;

            //狂暴余烬：低频血色怒火，纯客户端表现
            if (!Main.dedServ && Main.rand.NextBool(28)) {
                Dust dust = Dust.NewDustDirect(npc.position, npc.width, npc.height,
                    DustID.RedTorch, 0f, -0.8f, 120, default, 0.9f + 0.15f * boundTier);
                dust.noGravity = true;
                dust.velocity *= 0.6f;
            }
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers) {
            if (boundTier <= 0 || !RageEligible(npc)) {
                return;
            }
            //常态狂暴的接触伤害追加（在全局属性增幅之上）
            modifiers.FinalDamage *= GameModeTuning.ContactMult(boundTier);
        }

        public override Color? GetAlpha(NPC npc, Color drawColor) {
            if (boundTier <= 0 || !RageEligible(npc)) {
                return null;
            }
            //红光脉动：个体错拍，保留原光照与透明度
            float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 4.2f + npc.whoAmI * 0.7f);
            float amount = (0.08f + 0.05f * boundTier) * (0.6f + 0.4f * pulse);
            return Color.Lerp(drawColor, new Color(255, 58, 48, drawColor.A), amount);
        }
    }

    /// <summary>建/清 Brutal 重制类型排除表</summary>
    internal class GameModeNPCLoader : ICWRLoader
    {
        /// <summary>
        /// 实际会接管的 Brutal 重制 NPC 类型。
        /// 模式开启时这些类型由 AI 重制承担难度，不吃通用增强。
        /// <see cref="BrutalNPCOverride.DisabledReworkTypes"/> 里的类型不进此表，留给通用增强。
        /// </summary>
        internal static readonly HashSet<int> BrutalOverriddenTypes = [];

        void ICWRLoader.SetupData() {
            BrutalOverriddenTypes.Clear();
            foreach (var pair in NPCOverride.ByID) {
                if (BrutalNPCOverride.DisabledReworkTypes.Contains(pair.Key)) {
                    continue;
                }
                foreach (var inds in pair.Value.Values) {
                    if (inds is BrutalNPCOverride) {
                        BrutalOverriddenTypes.Add(pair.Key);
                        break;
                    }
                }
            }
        }

        void ICWRLoader.UnLoadData() => BrutalOverriddenTypes.Clear();
    }
}
