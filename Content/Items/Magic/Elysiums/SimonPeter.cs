using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第一门徒：西门彼得（磐石之盾）
    /// 能力：为玩家生成神圣防护光环，减免伤害并产生护盾效果
    /// 象征物：天国之钥（彼得是教会的磐石，掌管天国之门）
    /// </summary>
    internal class SimonPeter : BaseDisciple
    {
        public override int DiscipleIndex => 0;
        public override string DiscipleName => "西门彼得";
        public override Color DiscipleColor => new(255, 215, 0); //金色
        public override int AbilityCooldownTime => 180;

        //彼得作为磐石，运动稳重但流畅
        protected override float OrbitSpeedMultiplier => 0.9f;
        protected override float VerticalWaveAmplitude => 8f;
        protected override float MovementSmoothness => 0.12f;

        //3D轨道：彼得在中心层（第一门徒，核心位置）
        protected override float OrbitTiltAngle => 0.1f;
        protected override float OrbitHeightLayer => 0f;

        /// <summary>被动效果计时器</summary>
        private int passiveTimer = 0;

        /// <summary>护盾强度（基于激活次数积累）</summary>
        private float shieldStrength = 1f;

        /// <summary>护盾激活状态</summary>
        private bool shieldActive = false;

        /// <summary>护盾持续时间</summary>
        private int shieldDuration = 0;

        /// <summary>最大护盾持续时间</summary>
        private const int MaxShieldDuration = 300; //5秒

        /// <summary>伤害减免比例</summary>
        private float damageReduction = 0f;

        protected override void ExecuteAbility() {
            //激活磐石之盾
            shieldActive = true;
            shieldDuration = MaxShieldDuration;

            //计算护盾强度和伤害减免
            shieldStrength = Math.Min(shieldStrength + 0.2f, 2f);
            damageReduction = 0.15f + shieldStrength * 0.05f; //15%-25%伤害减免

            //生成完整的磐石之盾特效
            if (!VaultUtils.isServer) {
                SimonPeterShieldEffects.SpawnRockShieldEffect(Owner, Projectile.Center, shieldStrength);
            }

            //给予玩家防御buff
            Owner.AddBuff(BuffID.Ironskin, shieldDuration);
            Owner.AddBuff(BuffID.Endurance, shieldDuration);

            //显示护盾信息
            int reductionPercent = (int)(damageReduction * 100);
            CombatText.NewText(Owner.Hitbox, DiscipleColor, $"磐石之盾 -{reductionPercent}%", true);

            SetCooldown(120);
        }

        protected override void PassiveEffect() {
            passiveTimer++;

            //更新护盾状态
            if (shieldActive) {
                shieldDuration--;
                if (shieldDuration <= 0) {
                    shieldActive = false;
                    damageReduction = 0f;
                    //护盾结束时减少强度
                    shieldStrength = Math.Max(1f, shieldStrength - 0.1f);
                }
            }

            //被动：基础防御加成
            int baseDefense = 8;
            //护盾激活时额外防御
            if (shieldActive) {
                baseDefense += (int)(5 * shieldStrength);
            }
            Owner.statDefense += baseDefense;

            //护盾激活时的伤害减免
            if (shieldActive && damageReduction > 0) {
                //通过endurance实现伤害减免
                Owner.endurance += damageReduction * 0.5f; //叠加到耐力
            }

            //生成被动护盾光环
            if (!VaultUtils.isServer) {
                SimonPeterShieldEffects.SpawnPassiveShieldAura(Owner.Center, Projectile.Center, passiveTimer, shieldStrength);
            }

            //彼得会在玩家生命低时加速冷却
            if ((float)Owner.statLife / Owner.statLifeMax2 < 0.5f && abilityCooldown > 30) {
                abilityCooldown -= 1; //加速冷却
            }
        }

        protected override Vector2 CalculateCustomOffset() {
            //彼得作为磐石，运动非常稳定
            //但在护盾激活时会略微靠近玩家以示保护
            if (shieldActive) {
                Vector2 toPlayer = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float pulse = MathF.Sin(passiveTimer * 0.1f) * 3f;
                return toPlayer * (10f + pulse);
            }

            //玩家受伤时也会靠近
            if ((float)Owner.statLife / Owner.statLifeMax2 < 0.7f) {
                Vector2 toPlayer = (Owner.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float healthRatio = (float)Owner.statLife / Owner.statLifeMax2;
                return toPlayer * ((0.7f - healthRatio) * 20f);
            }

            return Vector2.Zero;
        }

        protected override void CustomDraw(SpriteBatch sb, Vector2 drawPos) {
            //绘制护盾光环
            if (shieldActive) {
                float alpha = shieldDuration / (float)MaxShieldDuration;
                SimonPeterShieldEffects.DrawShieldAura(sb, Owner.Center, passiveTimer, shieldStrength, alpha);
            }

            //绘制天国之钥指示（护盾强度可视化）
            if (shieldStrength > 1.2f) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    float keyPulse = 0.7f + MathF.Sin(passiveTimer * 0.15f) * 0.3f;
                    float keyAlpha = (shieldStrength - 1f) * 0.5f * keyPulse;

                    //钥匙形光晕
                    Vector2 keyPos = drawPos + new Vector2(0, -20f);
                    Color keyColor = SimonPeterShieldEffects.HeavenlyGold with { A = 0 } * keyAlpha;
                    sb.Draw(glow, keyPos, null, keyColor, 0, glow.Size() / 2, 0.3f, SpriteEffects.None, 0);

                    //钥匙杆
                    Vector2 shaftPos = drawPos + new Vector2(0, -8f);
                    Color shaftColor = SimonPeterShieldEffects.HeavenlyGold with { A = 0 } * keyAlpha * 0.7f;
                    sb.Draw(glow, shaftPos, null, shaftColor, 0, glow.Size() / 2, new Vector2(0.1f, 0.3f), SpriteEffects.None, 0);
                }
            }
        }

        /// <summary>
        /// 当玩家受到伤害时触发（需要在ModPlayer中调用）
        /// </summary>
        public void OnPlayerHurt(Vector2 hitDirection) {
            if (shieldActive && !VaultUtils.isServer) {
                //生成护盾格挡特效
                SimonPeterShieldEffects.SpawnDamageBlockEffect(Owner.Center, hitDirection);
            }
        }
    }

    /// <summary>
    /// 西门彼得的护盾玩家效果
    /// </summary>
    internal class SimonPeterShieldPlayer : ModPlayer
    {
        /// <summary>是否有彼得门徒激活护盾</summary>
        public bool HasPeterShield { get; set; } = false;

        /// <summary>护盾伤害减免</summary>
        public float ShieldDamageReduction { get; set; } = 0f;

        public override void ResetEffects() {
            HasPeterShield = false;
            ShieldDamageReduction = 0f;
        }

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (HasPeterShield && ShieldDamageReduction > 0) {
                //应用护盾伤害减免
                modifiers.FinalDamage *= (1f - ShieldDamageReduction);
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (!HasPeterShield) return;

            //寻找彼得门徒并触发护盾特效
            if (Player.TryGetModPlayer<ElysiumPlayer>(out var ep)) {
                foreach (int projIdx in ep.ActiveDisciples) {
                    if (projIdx < 0 || projIdx >= Main.maxProjectiles) continue;
                    Projectile proj = Main.projectile[projIdx];
                    if (!proj.active) continue;

                    if (proj.ModProjectile is SimonPeter peter) {
                        Vector2 hitDir = Vector2.Zero;
                        if (info.DamageSource.TryGetCausingEntity(out Entity entity)) {
                            hitDir = (Player.Center - entity.Center).SafeNormalize(Vector2.UnitX);
                        }
                        else {
                            hitDir = Main.rand.NextVector2Unit();
                        }

                        peter.OnPlayerHurt(hitDir);
                        break;
                    }
                }
            }
        }
    }
}
