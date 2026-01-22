using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第七门徒：多马（怀疑之触）
    /// 能力：给予玩家"验证"状态，期间必定暴击，暴击会延长状态持续时间
    /// 象征物：长枪（多马用长枪验证了耶稣的伤口）
    /// </summary>
    internal class Thomas : BaseDisciple
    {
        public override int DiscipleIndex => 6;
        public override string DiscipleName => "多马";
        public override Color DiscipleColor => new(255, 165, 0); //怀疑橙
        public override int AbilityCooldownTime => 300; //5秒冷却

        //多马是怀疑者，运动有脉冲感
        protected override bool UsePulseMotion => true;
        protected override float MovementSmoothness => 0.12f;

        //3D轨道：多马在外层
        protected override float OrbitTiltAngle => 0.4f;
        protected override float OrbitTiltDirection => MathHelper.Pi * 1.1f;
        protected override float OrbitHeightLayer => 0.2f;

        /// <summary>被动效果计时器</summary>
        private int passiveTimer = 0;

        /// <summary>怀疑积累（看到敌人时积累）</summary>
        private float doubtAccumulation = 0f;

        /// <summary>基础验证持续时间（帧）</summary>
        private const int BaseVerificationDuration = 180; //3秒

        protected override void ExecuteAbility() {
            //获取玩家的验证状态
            if (!Owner.TryGetModPlayer<ThomasVerificationPlayer>(out var verifyPlayer)) {
                SetCooldown(60);
                return;
            }

            //如果已经在验证状态，则增强并延长
            if (verifyPlayer.IsVerified) {
                //延长验证时间
                verifyPlayer.VerificationTime = Math.Min(verifyPlayer.VerificationTime + 120, 360);

                //播放增强音效
                SoundEngine.PlaySound(SoundID.Item4 with {
                    Volume = 0.7f,
                    Pitch = 0.8f
                }, Owner.Center);

                CombatText.NewText(Owner.Hitbox, ThomasDoubtEffects.VerifyGold, "验证强化!", true);

                //增强特效
                SpawnEnhanceEffect();

                SetCooldown(120); //增强时冷却较短
            }
            else {
                //激活验证状态
                int duration = BaseVerificationDuration + (int)(doubtAccumulation * 60); //怀疑积累增加持续时间
                verifyPlayer.ActivateVerification(duration);

                //生成完整的验证特效
                if (!VaultUtils.isServer) {
                    ThomasDoubtEffects.SpawnVerificationEffect(Owner, Projectile.Center);
                }

                CombatText.NewText(Owner.Hitbox, DiscipleColor, "怀疑验证!", true);

                //重置怀疑积累
                doubtAccumulation = 0f;

                SetCooldown(240); //首次激活冷却较长
            }
        }

        /// <summary>
        /// 生成验证增强特效
        /// </summary>
        private void SpawnEnhanceEffect() {
            if (VaultUtils.isServer) return;

            //环形爆发
            for (int i = 0; i < 20; i++) {
                float angle = MathHelper.TwoPi * i / 20f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 8f);

                Dust d = Dust.NewDustPerfect(Owner.Center, DustID.Torch,
                    vel, 100, ThomasDoubtEffects.VerifyGold, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }

            //感叹号
            for (int i = 0; i < 4; i++) {
                Vector2 pos = Owner.Center + new Vector2(0, -20f - i * 8f);
                Dust d = Dust.NewDustPerfect(pos, DustID.Torch,
                    new Vector2(0, -2f), 100, ThomasDoubtEffects.InsightWhite, 1f);
                d.noGravity = true;
            }
        }

        protected override void PassiveEffect() {
            passiveTimer++;

            //被动：小幅增加暴击率
            Owner.GetCritChance(DamageClass.Generic) += 5;

            //检测是否处于验证状态，额外增加暴击伤害
            if (Owner.TryGetModPlayer<ThomasVerificationPlayer>(out var verifyPlayer) && verifyPlayer.IsVerified) {
                //验证状态下增加暴击伤害
                Owner.GetCritChance(DamageClass.Generic) += 100; //保证暴击
            }

            //怀疑积累：看到敌人时增加
            int nearbyEnemies = CountNearbyEnemies(500f);
            if (nearbyEnemies > 0) {
                //敌人越多，积累越快
                doubtAccumulation = Math.Min(doubtAccumulation + 0.002f * nearbyEnemies, 2f);

                //当怀疑积累达到一定程度，缩短冷却
                if (doubtAccumulation > 1f && abilityCooldown > 60) {
                    abilityCooldown -= 1;
                }
            }
            else {
                //没有敌人时缓慢消散
                doubtAccumulation = Math.Max(0f, doubtAccumulation - 0.001f);
            }

            //生成被动怀疑光环
            if (!VaultUtils.isServer) {
                ThomasDoubtEffects.SpawnPassiveDoubtAura(Projectile.Center, passiveTimer);
            }
        }

        /// <summary>
        /// 计算附近敌人数量
        /// </summary>
        private int CountNearbyEnemies(float range) {
            int count = 0;
            foreach (NPC npc in Main.npc) {
                if (npc.active && !npc.friendly && npc.CanBeChasedBy() &&
                    Vector2.Distance(npc.Center, Projectile.Center) < range) {
                    count++;
                }
            }
            return count;
        }

        protected override Vector2 CalculateCustomOffset() {
            //怀疑积累高时，多马会更加警觉地移动
            if (doubtAccumulation > 0.5f) {
                float intensity = Math.Min(doubtAccumulation, 1.5f);
                float pulse = MathF.Sin(passiveTimer * 0.2f) * 8f * intensity;
                float secondaryPulse = MathF.Cos(passiveTimer * 0.15f) * 5f * intensity;
                return new Vector2(pulse, secondaryPulse);
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// 自定义绘制 - 在验证状态时绘制眼睛
        /// </summary>
        protected override void CustomDraw(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, Vector2 drawPos) {
            if (!Owner.TryGetModPlayer<ThomasVerificationPlayer>(out var verifyPlayer)) return;

            if (verifyPlayer.IsVerified) {
                //在门徒上方绘制一个小眼睛指示
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    Vector2 eyePos = drawPos - new Vector2(0, 25f);
                    float pulse = 0.8f + MathF.Sin(passiveTimer * 0.15f) * 0.2f;

                    //眼睛光晕
                    Color eyeColor = ThomasDoubtEffects.InsightWhite with { A = 0 } * pulse * 0.6f;
                    sb.Draw(glow, eyePos, null, eyeColor, 0, glow.Size() / 2, 0.3f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);

                    //瞳孔
                    Color pupilColor = ThomasDoubtEffects.TruthBlue with { A = 0 } * pulse * 0.8f;
                    sb.Draw(glow, eyePos, null, pupilColor, 0, glow.Size() / 2, 0.15f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
                }
            }

            //怀疑积累可视化（问号透明度）
            if (doubtAccumulation > 0.3f) {
                Texture2D glow = CWRAsset.SoftGlow?.Value;
                if (glow != null) {
                    float alpha = Math.Min(doubtAccumulation / 2f, 0.5f);
                    Color questionColor = ThomasDoubtEffects.QuestionRed with { A = 0 } * alpha;
                    sb.Draw(glow, drawPos + new Vector2(15f, -15f), null, questionColor, 0, glow.Size() / 2, 0.2f, Microsoft.Xna.Framework.Graphics.SpriteEffects.None, 0);
                }
            }
        }
    }
}
