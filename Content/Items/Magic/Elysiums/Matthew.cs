using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Items.Magic.Elysiums
{
    /// <summary>
    /// 第八门徒：圣马修（财富祝福）
    /// 能力：祝福敌人，使其死亡时掉落更多金币并产生神圣财富特效
    /// 象征物：钱袋（原为税吏）
    /// </summary>
    internal class Matthew : BaseDisciple
    {
        public override int DiscipleIndex => 7;
        public override string DiscipleName => "圣马修";
        public override Color DiscipleColor => new(255, 215, 100); //财富金
        public override int AbilityCooldownTime => 60; //缩短冷却，更频繁地祝福敌人

        //马修原为税吏，运动精确流畅
        protected override float OrbitSpeedMultiplier => 1.0f;
        protected override float VerticalWaveAmplitude => 10f;
        protected override float HorizontalWaveAmplitude => 10f;
        protected override float MovementSmoothness => 0.16f;

        //3D轨道：马修在中层
        protected override float OrbitTiltAngle => 0.18f;
        protected override float OrbitTiltDirection => MathHelper.Pi * 1.3f;
        protected override float OrbitHeightLayer => -0.1f;

        /// <summary>被动效果计时器</summary>
        private int passiveTimer = 0;

        /// <summary>祝福范围</summary>
        private const float BlessingRadius = 400f;

        /// <summary>祝福强度（随已祝福敌人数量递增）</summary>
        private float blessingStrength = 1f;

        /// <summary>已祝福的敌人数量</summary>
        private int blessedCount = 0;

        protected override void ExecuteAbility() {
            //寻找范围内未被祝福的敌人
            bool blessedAny = false;
            int newBlessedCount = 0;

            foreach (NPC npc in Main.npc) {
                if (!npc.active || npc.friendly || npc.CountsAsACritter) continue;
                if (Vector2.Distance(npc.Center, Projectile.Center) > BlessingRadius) continue;

                //获取NPC的全局实例
                if (npc.TryGetGlobalNPC<MatthewGlobalNPC>(out var matthewNpc)) {
                    if (!matthewNpc.BlessedByMatthew) {
                        //祝福这个敌人
                        matthewNpc.BlessedByMatthew = true;
                        matthewNpc.BlessingOwner = Owner.whoAmI;
                        matthewNpc.BlessingStrength = blessingStrength;

                        //生成祝福标记效果
                        if (!VaultUtils.isServer) {
                            MatthewWealthEffects.SpawnBlessingMark(npc);
                        }

                        blessedAny = true;
                        newBlessedCount++;

                        //限制每次祝福的敌人数量
                        if (newBlessedCount >= 5) break;
                    }
                }
            }

            if (blessedAny) {
                blessedCount += newBlessedCount;

                //更新祝福强度
                blessingStrength = 1f + Math.Min(blessedCount * 0.05f, 0.5f);

                //播放祝福音效
                SoundEngine.PlaySound(SoundID.CoinPickup with {
                    Volume = 0.7f,
                    Pitch = 0.3f
                }, Projectile.Center);

                //金币视觉效果
                SpawnBlessingEffect();

                //显示祝福文字
                CombatText.NewText(Projectile.Hitbox, DiscipleColor, $"财富祝福 x{newBlessedCount}", true);
            }

            SetCooldown(40); //成功祝福后冷却较短
        }

        /// <summary>
        /// 生成祝福时的视觉效果
        /// </summary>
        private void SpawnBlessingEffect() {
            if (VaultUtils.isServer) return;

            //金币环绕爆发
            for (int i = 0; i < 15; i++) {
                float angle = MathHelper.TwoPi * i / 15f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(3f, 6f);

                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + angle.ToRotationVector2() * 10f,
                    DustID.GoldCoin,
                    vel,
                    100, DiscipleColor, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }

            //向外扩散的祝福波
            for (int ring = 0; ring < 3; ring++) {
                float radius = 20f + ring * 15f;
                int count = 8 + ring * 4;

                for (int i = 0; i < count; i++) {
                    float angle = MathHelper.TwoPi * i / count + ring * 0.1f;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius;

                    Dust d = Dust.NewDustPerfect(pos, DustID.GoldFlame,
                        angle.ToRotationVector2() * (2f + ring),
                        100, Color.Lerp(DiscipleColor, Color.White, ring * 0.2f),
                        0.8f - ring * 0.15f);
                    d.noGravity = true;
                }
            }
        }

        protected override void PassiveEffect() {
            passiveTimer++;

            //被动：增加金币掉落(通过增加玩家幸运值)
            Owner.luck += 0.15f;

            //额外幸运基于已祝福敌人数量
            Owner.luck += Math.Min(blessedCount * 0.02f, 0.2f);

            //生成被动祝福光环效果
            if (!VaultUtils.isServer) {
                MatthewWealthEffects.SpawnPassiveBlessingAura(Projectile.Center, passiveTimer);
            }

            //定期重置祝福计数（防止无限增长）
            if (passiveTimer % 600 == 0) { //每10秒
                blessedCount = Math.Max(0, blessedCount - 5);
                blessingStrength = 1f + Math.Min(blessedCount * 0.05f, 0.5f);
            }
        }

        protected override Vector2 CalculateCustomOffset() {
            //当附近有很多敌人时，马修会兴奋地移动
            int nearbyEnemies = 0;
            foreach (NPC npc in Main.npc) {
                if (npc.active && !npc.friendly && !npc.CountsAsACritter &&
                    Vector2.Distance(npc.Center, Projectile.Center) < BlessingRadius) {
                    nearbyEnemies++;
                }
            }

            if (nearbyEnemies > 0) {
                //敌人越多，运动幅度越大
                float excitement = Math.Min(nearbyEnemies / 5f, 1f);
                float wobble = MathF.Sin(passiveTimer * 0.15f) * 5f * excitement;
                return new Vector2(wobble, MathF.Cos(passiveTimer * 0.12f) * 3f * excitement);
            }

            return Vector2.Zero;
        }
    }
}
