using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第三门徒：雅各布（雷霆审判）
    /// 能力：释放连锁雷击，打击多个敌人
    /// 象征物：剑（雷霆之子的圣剑）
    /// 与Jude的审判雷电区别：
    /// - Jude: 从天而降的神圣审判，单体高伤害，庄严肃穆
    /// - James: 横向连锁的狂暴雷霆，多目标连锁，狂野迅捷
    /// </summary>
    internal class James : BaseDisciple
    {
        public override int DiscipleIndex => 2;
        public override string DiscipleName => "雅各布";
        public override Color DiscipleColor => new(255, 255, 100); //雷霆黄
        public override int AbilityCooldownTime => 150;

        //雅各布是雷霆之子，运动迅捷
        protected override float OrbitSpeedMultiplier => 1.4f;
        protected override float MovementSmoothness => 0.2f;

        //3D轨道：雅各布在上层
        protected override float OrbitTiltAngle => 0.35f;
        protected override float OrbitTiltDirection => MathHelper.Pi * 0.1f;
        protected override float OrbitHeightLayer => 0.7f;

        /// <summary>被动效果计时器</summary>
        private int passiveTimer = 0;

        /// <summary>雷霆蓄能（击中敌人时积累）</summary>
        private float thunderCharge = 0f;

        /// <summary>连锁次数（基础+蓄能加成）</summary>
        private int maxChainCount = 3;

        /// <summary>连锁范围</summary>
        private const float ChainRange = 300f;

        /// <summary>主攻击范围</summary>
        private const float AttackRange = 500f;

        /// <summary>已被雷击的敌人（防止重复连锁）</summary>
        private readonly HashSet<int> struckEnemies = [];

        protected override void ExecuteAbility() {
            struckEnemies.Clear();

            //寻找最近的敌人作为主目标
            NPC primaryTarget = FindNearestEnemy(AttackRange);

            if (primaryTarget != null) {
                //计算连锁次数
                int chainCount = maxChainCount + (int)(thunderCharge * 2);
                chainCount = Math.Min(chainCount, 6); //最多6次连锁

                //计算伤害
                int baseDamage = 80 + Owner.GetWeaponDamage(Owner.HeldItem) / 2;
                int damage = baseDamage + (int)(thunderCharge * 30);

                //对主目标造成雷击
                StrikeTarget(primaryTarget, damage, 0);

                //生成主雷击特效
                if (!VaultUtils.isServer) {
                    JamesThunderEffects.SpawnThunderJudgmentEffect(Projectile.Center, primaryTarget, chainCount);
                    JamesThunderEffects.SpawnDiscipleGlow(Projectile.Center);
                }

                //执行连锁雷击
                ExecuteChainLightning(primaryTarget, damage, chainCount, 1);

                //显示连锁信息
                CombatText.NewText(Owner.Hitbox, DiscipleColor, $"雷霆连锁 x{struckEnemies.Count}", true);

                //消耗蓄能
                thunderCharge = Math.Max(0f, thunderCharge - 0.5f);

                //根据击中数量调整冷却
                int cooldown = Math.Max(60, 120 - struckEnemies.Count * 10);
                SetCooldown(cooldown);
            }
            else {
                //没有敌人时，释放警示雷电
                SpawnWarningThunder();
                SetCooldown(30);
            }
        }

        /// <summary>
        /// 执行连锁雷击
        /// </summary>
        private void ExecuteChainLightning(NPC currentTarget, int damage, int remainingChains, int chainIndex) {
            if (remainingChains <= 0) return;

            //寻找下一个连锁目标
            NPC nextTarget = FindChainTarget(currentTarget.Center);
            if (nextTarget == null) return;

            //连锁伤害递减
            int chainDamage = (int)(damage * (0.8f - chainIndex * 0.1f));
            chainDamage = Math.Max(chainDamage, damage / 3);

            //对目标造成伤害
            StrikeTarget(nextTarget, chainDamage, chainIndex);

            //生成连锁特效
            if (!VaultUtils.isServer) {
                JamesThunderEffects.SpawnChainLightningEffect(currentTarget.Center, nextTarget.Center, chainIndex);
            }

            //继续连锁
            ExecuteChainLightning(nextTarget, damage, remainingChains - 1, chainIndex + 1);
        }

        /// <summary>
        /// 寻找连锁目标
        /// </summary>
        private NPC FindChainTarget(Vector2 fromPosition) {
            NPC closest = null;
            float closestDist = ChainRange;

            foreach (NPC npc in Main.npc) {
                if (!npc.CanBeChasedBy()) continue;
                if (struckEnemies.Contains(npc.whoAmI)) continue;

                float dist = Vector2.Distance(npc.Center, fromPosition);
                if (dist < closestDist) {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }

        /// <summary>
        /// 对目标造成雷击伤害
        /// </summary>
        private void StrikeTarget(NPC target, int damage, int chainIndex) {
            struckEnemies.Add(target.whoAmI);

            //造成伤害
            target.SimpleStrikeNPC(damage, 0, false, 0, DamageClass.Magic);

            //添加电击debuff（如果有的话）
            target.AddBuff(BuffID.Electrified, 120);

            //积累蓄能
            thunderCharge = Math.Min(thunderCharge + 0.1f, 1.5f);

            //播放电击音效
            if (chainIndex == 0) {
                SoundEngine.PlaySound(SoundID.Item122 with {
                    Volume = 0.9f,
                    Pitch = 0.3f
                }, target.Center);
            }
            else {
                SoundEngine.PlaySound(SoundID.Item93 with {
                    Volume = 0.5f + chainIndex * 0.1f,
                    Pitch = 0.4f + chainIndex * 0.1f
                }, target.Center);
            }
        }

        /// <summary>
        /// 释放警示雷电（无敌人时）
        /// </summary>
        private void SpawnWarningThunder() {
            if (VaultUtils.isServer) return;

            //在门徒周围生成小型闪电
            for (int i = 0; i < 3; i++) {
                float angle = MathHelper.TwoPi * i / 3f + Main.rand.NextFloat(-0.3f, 0.3f);
                Vector2 from = Projectile.Center + angle.ToRotationVector2() * 20f;
                Vector2 to = Projectile.Center + angle.ToRotationVector2() * 60f;

                JamesThunderEffects.SpawnChainLightningEffect(from, to, i);
            }

            JamesThunderEffects.SpawnDiscipleGlow(Projectile.Center);

            SoundEngine.PlaySound(SoundID.Item93 with {
                Volume = 0.6f,
                Pitch = 0.5f
            }, Projectile.Center);
        }

        protected override void PassiveEffect() {
            passiveTimer++;

            //被动：增加电击伤害
            Owner.GetArmorPenetration(DamageClass.Magic) += 5;

            //蓄能缓慢自然恢复
            if (passiveTimer % 60 == 0 && thunderCharge < 0.5f) {
                thunderCharge = Math.Min(thunderCharge + 0.05f, 0.5f);
            }

            //更新连锁次数
            maxChainCount = 3 + (int)(thunderCharge);

            //生成被动雷霆光环
            if (!VaultUtils.isServer) {
                JamesThunderEffects.SpawnPassiveThunderAura(Projectile.Center, passiveTimer);

                //蓄能可视化
                if (thunderCharge > 0.3f) {
                    JamesThunderEffects.SpawnChargeEffect(Projectile.Center, thunderCharge);
                }
            }

            //当蓄能满时加速冷却
            if (thunderCharge > 1f && abilityCooldown > 30) {
                abilityCooldown -= 1;
            }
        }

        protected override Vector2 CalculateCustomOffset() {
            //雷霆之子在战斗中会更加活跃
            int nearbyEnemies = 0;
            foreach (NPC npc in Main.npc) {
                if (npc.active && !npc.friendly && npc.CanBeChasedBy() &&
                    Vector2.Distance(npc.Center, Projectile.Center) < AttackRange) {
                    nearbyEnemies++;
                }
            }

            if (nearbyEnemies > 0 || thunderCharge > 0.5f) {
                //战斗状态下的快速移动
                float intensity = Math.Min(nearbyEnemies * 0.3f + thunderCharge, 2f);
                float zigzag = MathF.Sin(passiveTimer * 0.25f) * 8f * intensity;
                float bounce = MathF.Cos(passiveTimer * 0.2f) * 5f * intensity;
                return new Vector2(zigzag, bounce);
            }

            return Vector2.Zero;
        }

        protected override void CustomDraw(SpriteBatch sb, Vector2 drawPos) {
            //绘制蓄能指示
            if (thunderCharge > 0.2f) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    float pulse = 0.7f + MathF.Sin(passiveTimer * 0.2f) * 0.3f;
                    float chargeAlpha = thunderCharge * 0.6f * pulse;

                    //外层雷霆光晕
                    Color outerColor = JamesThunderEffects.ThunderYellow with { A = 0 } * chargeAlpha;
                    sb.Draw(glow, drawPos, null, outerColor, 0, glow.Size() / 2, 0.5f + thunderCharge * 0.3f, SpriteEffects.None, 0);

                    //内层电弧光晕
                    Color innerColor = JamesThunderEffects.ArcWhite with { A = 0 } * chargeAlpha * 0.5f;
                    sb.Draw(glow, drawPos, null, innerColor, 0, glow.Size() / 2, 0.25f + thunderCharge * 0.15f, SpriteEffects.None, 0);
                }
            }

            //满蓄能时绘制闪电符号
            if (thunderCharge > 1f) {
                Texture2D px = CWRAsset.Placeholder_White?.Value;
                if (px != null) {
                    float flash = MathF.Sin(passiveTimer * 0.3f) * 0.3f + 0.7f;
                    Color boltColor = JamesThunderEffects.ThunderYellow with { A = 100 } * flash;

                    //简单闪电形状
                    Vector2 boltPos = drawPos + new Vector2(15f, -20f);
                    sb.Draw(px, boltPos, null, boltColor, MathHelper.ToRadians(-15f), new Vector2(0.5f), new Vector2(8f, 2f), SpriteEffects.None, 0);
                    sb.Draw(px, boltPos + new Vector2(3f, 4f), null, boltColor, MathHelper.ToRadians(15f), new Vector2(0.5f), new Vector2(8f, 2f), SpriteEffects.None, 0);
                    sb.Draw(px, boltPos + new Vector2(6f, 8f), null, boltColor, MathHelper.ToRadians(-15f), new Vector2(0.5f), new Vector2(6f, 2f), SpriteEffects.None, 0);
                }
            }
        }
    }
}
