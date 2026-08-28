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
    /// 游戏模式的通用敌怪增强，血量与伤害的档位增幅作用于全体敌人，重制 Boss 也不例外。
    /// 属性缩放在 <see cref="SetDefaults"/> 绑定：世界旗标已全端同步，两端确定性执行零网络，
    /// 切换模式只影响此后生成的个体（与 AI 覆盖同语义）。
    /// Brutal 重制类型多走一层大师基线锚定（<see cref="GameModeTuning.MasterAnchorCompensation"/>），
    /// 把世界难度补足到大师后再乘档位增幅，保证任何世界难度下的强度一致。
    /// 提速与常态狂暴只作用于非 Boss 且非重制类型，保护 Boss 招式编排的可读性
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

            float statMult = GameModeTuning.StatMult(tier);

            //重制类型：先把世界难度补到大师基线，再和其余敌人一样吃档位增幅。
            //提速与常态狂暴仍不给（boundTier 留 0），招式编排的节奏由重制自己掌控
            if (GameModeNPCLoader.BrutalOverriddenTypes.Contains(npc.type)) {
                (float lifeComp, float damageComp) = GameModeTuning.MasterAnchorCompensation(npc.type);
                ScaleStats(npc, lifeComp * statMult, damageComp * statMult);
                return;
            }

            if (!Eligible(npc)) {
                return;
            }
            boundTier = tier;

            ScaleStats(npc, statMult, statMult);
            if (RageEligible(npc)) {
                //常态狂暴：越来越推不动
                npc.knockBackResist *= GameModeTuning.KnockbackMult(tier);
            }
        }

        /// <summary>
        /// 缩放血量与伤害并恢复 SetDefaults 不变量。
        /// 此钩子先于原版 ScaleStats 执行，乘法可交换故与世界缩放顺序无关；
        /// 重制类型的大师基线补偿也从这里乘进去（补偿 × 世界缩放 == 大师缩放）
        /// </summary>
        private static void ScaleStats(NPC npc, float lifeMult, float damageMult) {
            if (lifeMult > 1f) {
                npc.lifeMax = (int)(npc.lifeMax * lifeMult);
            }
            if (damageMult > 1f && npc.damage > 0) {
                npc.damage = (int)(npc.damage * damageMult);
            }
            npc.life = npc.lifeMax;
            npc.defDamage = npc.damage;
            npc.defDefense = npc.defense;
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

            //滑步补偿：位置推进不走 velocity，原版 FindFrame 的步频感知不到额外位移，
            //腿慢身快读作滑行。对贴地行走个体按同一系数追加动画计数对齐步频；
            //frameCounter 纯视觉量，服务器不画不累加
            if (!Main.dedServ && !npc.noGravity && npc.velocity.Y == 0f && MathF.Abs(npc.velocity.X) > 0.05f) {
                npc.frameCounter += GameModeTuning.SpeedBonus(boundTier);
            }

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
        /// 这些类型照吃档位血量伤害增幅，只是多一层大师基线锚定，且不吃提速与常态狂暴。
        /// <see cref="BrutalNPCOverride.DisabledReworkTypes"/> 里的类型不进此表，走完整通用增强。
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
