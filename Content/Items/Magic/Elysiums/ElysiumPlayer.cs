using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 管理天国极乐武器的门徒系统
    /// </summary>
    internal class ElysiumPlayer : ModPlayer
    {
        //12门徒的类型数组(按顺序)
        public static readonly int[] DiscipleTypes = [
            ModContent.ProjectileType<SimonPeter>(),    //0: 西门彼得
            ModContent.ProjectileType<Andrew>(),         //1: 圣安德鲁
            ModContent.ProjectileType<James>(),          //2: 雅各布
            ModContent.ProjectileType<John>(),           //3: 圣约翰
            ModContent.ProjectileType<Philip>(),         //4: 腓力
            ModContent.ProjectileType<Bartholomew>(),    //5: 巴多罗买
            ModContent.ProjectileType<Thomas>(),         //6: 多马
            ModContent.ProjectileType<Matthew>(),        //7: 圣马修
            ModContent.ProjectileType<Lesser>(),         //8: 雅各(小)
            ModContent.ProjectileType<Jude>(),           //9: 达泰
            ModContent.ProjectileType<Zealot>(),         //10: 西门(狂热者)
            ModContent.ProjectileType<JudasIscariot>()   //11: 犹大
        ];

        //12门徒的名称
        public static readonly string[] DiscipleNames = [
            "西门彼得", "圣安德鲁", "雅各布", "圣约翰",
            "腓力", "巴多罗买", "多马", "圣马修",
            "雅各", "达泰", "西门", "犹大"
        ];

        //当前激活的门徒弹幕索引列表
        public List<int> ActiveDisciples = [];

        //犹大背刺阈值(生命百分比)
        public const float JudasBetrayalThreshold = 0.3f;

        //上次检查犹大背刺的时间
        private int judasBetrayalCooldown = 0;

        public override void ResetEffects() {
            //清理无效的门徒引用
            ActiveDisciples.RemoveAll(i => i < 0 || i >= Main.maxProjectiles || !Main.projectile[i].active
                || !IsDiscipleProjectile(Main.projectile[i])
                || Main.projectile[i].owner != Player.whoAmI);
        }

        /// <summary>
        /// 检查弹幕是否是门徒类型
        /// </summary>
        private static bool IsDiscipleProjectile(Projectile proj) {
            return proj.ModProjectile is BaseDisciple;
        }

        public override void PostUpdate() {
            //犹大背刺检测
            if (judasBetrayalCooldown > 0) {
                judasBetrayalCooldown--;
            }

            if (GetDiscipleCount() == 12 && judasBetrayalCooldown <= 0) {
                CheckJudasBetrayal();
            }

            //门徒增益效果
            ApplyDiscipleBonuses();
        }

        /// <summary>
        /// 获取当前门徒数量
        /// </summary>
        public int GetDiscipleCount() {
            return ActiveDisciples.Count(i => i >= 0 && i < Main.maxProjectiles
                && Main.projectile[i].active
                && IsDiscipleProjectile(Main.projectile[i])
                && Main.projectile[i].owner == Player.whoAmI);
        }

        /// <summary>
        /// 检查是否已拥有指定类型的门徒
        /// </summary>
        public bool HasDiscipleOfType(int projectileType) {
            return ActiveDisciples.Any(i => i >= 0 && i < Main.maxProjectiles
                && Main.projectile[i].active
                && Main.projectile[i].type == projectileType
                && Main.projectile[i].owner == Player.whoAmI);
        }

        /// <summary>
        /// 尝试将最近的城镇NPC转化为门徒
        /// </summary>
        public void TryConvertNearestNPC(Player player) {
            int currentCount = GetDiscipleCount();
            if (currentCount >= 12) {
                CombatText.NewText(player.Hitbox, Color.Gold, "门徒已满");
                return;
            }

            //寻找最近的城镇NPC
            float maxDist = 300f;
            NPC targetNPC = null;
            float closestDist = maxDist;

            foreach (NPC npc in Main.npc) {
                if (!npc.active || !npc.townNPC || npc.homeless) continue;
                float dist = Vector2.Distance(player.Center, npc.Center);
                if (dist < closestDist) {
                    closestDist = dist;
                    targetNPC = npc;
                }
            }

            if (targetNPC == null) {
                CombatText.NewText(player.Hitbox, Color.Gray, "附近没有可转化的居民");
                return;
            }

            //找到下一个可用的门徒类型
            int nextDiscipleType = GetNextAvailableDiscipleType();
            if (nextDiscipleType == -1) {
                CombatText.NewText(player.Hitbox, Color.Gold, "门徒已满");
                return;
            }

            //转化NPC为门徒
            ConvertToDisciple(player, targetNPC, nextDiscipleType, currentCount);
        }

        /// <summary>
        /// 获取下一个可用的门徒类型
        /// </summary>
        private int GetNextAvailableDiscipleType() {
            for (int i = 0; i < DiscipleTypes.Length; i++) {
                if (!HasDiscipleOfType(DiscipleTypes[i])) {
                    return DiscipleTypes[i];
                }
            }
            return -1;
        }

        /// <summary>
        /// 获取门徒类型的索引
        /// </summary>
        private static int GetDiscipleIndex(int projectileType) {
            for (int i = 0; i < DiscipleTypes.Length; i++) {
                if (DiscipleTypes[i] == projectileType) {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// 将NPC转化为门徒
        /// </summary>
        private void ConvertToDisciple(Player player, NPC npc, int discipleType, int discipleIndex) {
            //播放转化音效
            SoundEngine.PlaySound(SoundID.Item29 with { Volume = 1.5f, Pitch = 0.2f }, npc.Center);

            //生成圣光特效
            for (int i = 0; i < 50; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustPerfect(npc.Center, DustID.GoldFlame, vel, 100, default, 2f);
                d.noGravity = true;
            }

            //生成对应类型的门徒弹幕
            int proj = Projectile.NewProjectile(
                player.GetSource_ItemUse(player.HeldItem),
                npc.Center,
                Vector2.Zero,
                discipleType,
                0,
                0,
                player.whoAmI,
                npc.whoAmI //ai[0]存储原NPC索引用于位置参考
            );

            if (proj >= 0 && proj < Main.maxProjectiles) {
                ActiveDisciples.Add(proj);

                //获取门徒名称
                int idx = GetDiscipleIndex(discipleType);
                string name = idx >= 0 && idx < DiscipleNames.Length ? DiscipleNames[idx] : "门徒";
                CombatText.NewText(npc.Hitbox, Color.Gold, $"{name} 已加入");

                //让原NPC消失(进入门徒状态)
                npc.active = false;
                npc.netUpdate = true;
            }
        }

        /// <summary>
        /// 检测犹大背刺(12门徒时的斩杀机制)
        /// </summary>
        private void CheckJudasBetrayal() {
            //检查是否拥有犹大
            if (!HasDiscipleOfType(ModContent.ProjectileType<JudasIscariot>())) {
                return;
            }

            float healthPercent = (float)Player.statLife / Player.statLifeMax2;

            if (healthPercent <= JudasBetrayalThreshold) {
                //犹大的背叛！
                judasBetrayalCooldown = 600; //10秒冷却

                //播放背叛音效
                SoundEngine.PlaySound(SoundID.NPCDeath59 with { Volume = 2f, Pitch = -0.5f }, Player.Center);

                //显示背叛文字
                CombatText.NewText(Player.Hitbox, Color.DarkRed, "犹大的背叛!", true);

                //造成斩杀伤害
                int betrayalDamage = Player.statLife + 100;
                Player.Hurt(PlayerDeathReason.ByCustomReason($"{Player.name} 被犹大背叛了"), betrayalDamage, 0);

                //犹大门徒消失
                RemoveDiscipleByType(ModContent.ProjectileType<JudasIscariot>());
            }
        }

        /// <summary>
        /// 移除指定类型的门徒
        /// </summary>
        public void RemoveDiscipleByType(int projectileType) {
            for (int i = ActiveDisciples.Count - 1; i >= 0; i--) {
                int projIndex = ActiveDisciples[i];
                if (projIndex >= 0 && projIndex < Main.maxProjectiles) {
                    Projectile proj = Main.projectile[projIndex];
                    if (proj.active && proj.type == projectileType) {
                        proj.Kill();
                        ActiveDisciples.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 应用门徒增益效果
        /// </summary>
        private void ApplyDiscipleBonuses() {
            int count = GetDiscipleCount();
            if (count == 0) return;

            //11门徒时的强力增益(没有犹大的危险)
            if (count == 11) {
                Player.GetDamage(DamageClass.Generic) += 0.25f;
                Player.GetCritChance(DamageClass.Generic) += 15;
                Player.statDefense += 20;
                Player.lifeRegen += 5;
            }
            //12门徒时的超强增益(但有犹大背刺风险)
            else if (count == 12) {
                Player.GetDamage(DamageClass.Generic) += 0.50f;
                Player.GetCritChance(DamageClass.Generic) += 30;
                Player.statDefense += 40;
                Player.lifeRegen += 10;
                Player.moveSpeed += 0.3f;
            }
            //其他数量按比例增益
            else {
                float ratio = count / 12f;
                Player.GetDamage(DamageClass.Generic) += 0.15f * ratio;
                Player.GetCritChance(DamageClass.Generic) += (int)(10 * ratio);
                Player.statDefense += (int)(15 * ratio);
            }
        }

        /// <summary>
        /// 当玩家被Boss伤害时，门徒可能会死亡
        /// </summary>
        public override void OnHurt(Player.HurtInfo info) {
            //检查是否被Boss攻击
            if (info.DamageSource.TryGetCausingEntity(out Entity entity)) {
                if (entity is NPC npc && (npc.boss || NPCID.Sets.ShouldBeCountedAsBoss[npc.type])) {
                    //Boss攻击有几率杀死一个门徒
                    if (Main.rand.NextBool(3) && ActiveDisciples.Count > 0) {
                        int randomIndex = Main.rand.Next(ActiveDisciples.Count);
                        int projIndex = ActiveDisciples[randomIndex];
                        if (projIndex >= 0 && projIndex < Main.maxProjectiles && Main.projectile[projIndex].active) {
                            Projectile proj = Main.projectile[projIndex];
                            //获取门徒名称
                            string name = "门徒";
                            if (proj.ModProjectile is BaseDisciple disciple) {
                                name = disciple.DiscipleName;
                            }
                            CombatText.NewText(proj.Hitbox, Color.Red, $"{name} 殉道了");
                            proj.Kill();
                            ActiveDisciples.RemoveAt(randomIndex);
                        }
                    }
                }
            }
        }
    }
}
