using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.Ambience.Verdant.Projectiles
{
    /// <summary>
    /// 沼雾伏影的雾团本体（自身永不造成伤害）。ai[0]=出生档位。
    /// 凝聚(雾丝聚拢+虫鸣骤停,≥45 帧预告)→浓雾(轻微遮蔽+滞留结算)→消散，
    /// 三段全部由 timeLeft 确定，各端一致。玩家在浓雾内滞留过久时，
    /// 权威端在雾心生成荆棘合拢圈；及时离开雾团则滞留计数清零，无事发生。
    /// 遮蔽主体画在 <see cref="VerdantAmbientRender"/>（实体层之上），本体只画淡底衬
    /// </summary>
    internal class VerdantMireFogProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>凝聚预告帧数（公平契约 ≥45）</summary>
        internal const int GatherFrames = 75;
        internal const int DenseFrames = 430;
        internal const int DisperseFrames = 55;
        internal const int TotalLife = GatherFrames + DenseFrames + DisperseFrames;
        /// <summary>雾团半径（遮蔽视觉与滞留判定共用）</summary>
        internal const float FogRadius = 190f;
        /// <summary>滞留触发帧数（浓雾内连续停留，离开即清零）</summary>
        private const int DwellTriggerFrames = 52;
        /// <summary>荆棘伤害 = 丛林原版敌怪接触伤害锚 × 此值（0.4~0.6 契约）</summary>
        private const float ThornDamageFrac = 0.5f;
        /// <summary>合拢圈全局并发上限</summary>
        private const int ThornCap = 3;
        /// <summary>虫鸣骤停的作用半径（听觉预告通道）</summary>
        private const float HushRange = 1150f;

        private int Tier => Math.Max(1, (int)Projectile.ai[0]);
        private int Elapsed => TotalLife - Projectile.timeLeft;

        /// <summary>权威端滞留计数（决策私产，客户端不读）</summary>
        private float[] dwell;
        /// <summary>本团已放出合拢圈（一团一圈）</summary>
        private bool committed;

        /// <summary>浓度包络 0~1，渲染层同源取值</summary>
        internal float Density {
            get {
                int e = Elapsed;
                if (e < GatherFrames) {
                    float x = e / (float)GatherFrames;
                    return x * x * (3f - 2f * x);
                }
                if (e < GatherFrames + DenseFrames) {
                    return 1f;
                }
                return MathHelper.Clamp(1f - (e - GatherFrames - DenseFrames) / (float)DisperseFrames, 0f, 1f);
            }
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 480;

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            VerdantAmbientRender.PresenceStamp.Stamp();
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                Projectile.timeLeft = TotalLife;
                if (!Main.dedServ) {
                    //雾息：低哑的聚拢气声，与虫鸣骤停共同构成听觉预告
                    SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.42f, Pitch = -0.55f, MaxInstances = 3 }, Projectile.Center);
                }
            }

            int elapsed = Elapsed;

            if (!Main.dedServ) {
                //凝聚与浓雾期间压停丛林虫鸣（骤然的安静即警告）
                if (elapsed < GatherFrames + DenseFrames
                    && Vector2.DistanceSquared(Projectile.Center, Main.LocalPlayer.Center) < HushRange * HushRange) {
                    VerdantAmbience.MuteChirps();
                }
                UpdateVisuals(elapsed);
            }

            if (VaultUtils.isServer || VaultUtils.isSinglePlayer) {
                TrackDwell(elapsed);
            }
        }

        /// <summary>权威端滞留结算：浓雾期内逐玩家累计，达阈则在雾心起荆棘合拢圈</summary>
        private void TrackDwell(int elapsed) {
            if (committed || elapsed < GatherFrames || elapsed >= GatherFrames + DenseFrames) {
                return;
            }
            if (CWRWorld.HasBoss) {
                return;//Boss 在场：雾只留视觉，不结算伤害机制
            }
            dwell ??= new float[Main.maxPlayers];
            float triggerRangeSq = FogRadius * 0.85f * (FogRadius * 0.85f);
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                if (!player.active || player.dead) {
                    dwell[i] = 0f;
                    continue;
                }
                if (Vector2.DistanceSquared(player.Center, Projectile.Center) > triggerRangeSq) {
                    dwell[i] = 0f;//离开雾团即无事
                    continue;
                }
                if (++dwell[i] >= DwellTriggerFrames) {
                    TryCommitThorns();
                    return;
                }
            }
        }

        /// <summary>提交荆棘合拢圈（城镇安宁与并发上限在此把关）</summary>
        private void TryCommitThorns() {
            if (VerdantAmbience.TownSanctuary(Projectile.Center)) {
                committed = true;//安宁区内这团雾放弃结算，不再反复扫描
                return;
            }
            if (VerdantAmbience.CountActive(ModContent.ProjectileType<VerdantThornRingProj>()) >= ThornCap) {
                return;//稍后重试（滞留计数保持）
            }
            committed = true;
            //藤隙方向在提交瞬间锁定，随 ai 进出生包同步
            float gapAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            //已预除原版敌对弹幕 ×2 结算系数：damage = 接触锚 × 0.5 ÷ 2（前困难 7 / 困难后 17）。
            //引擎命中时自乘 ×2/×4/×6（经典/专家/大师），经典档实收约为接触伤一半，禁再叠难度乘数
            int damage = (int)(VerdantAmbience.JungleContactBase() * ThornDamageFrac / 2f);
            Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                ModContent.ProjectileType<VerdantThornRingProj>(), damage, 2f, Main.myPlayer,
                FogRadius * 0.92f, Tier, gapAngle);
        }

        /// <summary>客户端粒子与相位音（预算：凝聚 ≤0.5 粒/帧，浓雾 ≤0.2 粒/帧）</summary>
        private void UpdateVisuals(int elapsed) {
            if (elapsed == GatherFrames) {
                //凝定拍：雾落成形
                SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.3f, Pitch = -0.75f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.3f, Pitch = -0.5f, MaxInstances = 3 }, Projectile.Center);
            }
            else if (elapsed == GatherFrames + DenseFrames) {
                //散拍：叶隙风带走雾
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.35f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
            }

            if (elapsed < GatherFrames) {
                //凝聚：雾丝自外缘向心聚拢（视觉预告通道）
                if (elapsed % 2 == 0) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float r = FogRadius * Main.rand.NextFloat(1.05f, 1.45f);
                    Vector2 pos = Projectile.Center + ang.ToRotationVector2() * r;
                    Vector2 inward = (Projectile.Center - pos).SafeNormalize(Vector2.Zero)
                        * Main.rand.NextFloat(1.4f, 2.4f);
                    PRTLoader.NewParticle<PRT_VerdantMist>(pos, inward,
                        new Color(156, 172, 146), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(46);
                }
                return;
            }

            if (elapsed < GatherFrames + DenseFrames) {
                //浓雾：内部缓涡
                if (elapsed % 5 == 0) {
                    Vector2 pos = Projectile.Center + Main.rand.NextVector2Circular(FogRadius * 0.85f, FogRadius * 0.7f);
                    PRTLoader.NewParticle<PRT_VerdantMist>(pos,
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.05f, 0.25f)),
                        new Color(150, 166, 142), Main.rand.NextFloat(0.42f, 0.68f))?.Configure(120);
                }
                return;
            }

            //消散：雾向外剥离
            if (elapsed % 3 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * FogRadius * Main.rand.NextFloat(0.3f, 0.8f);
                Vector2 outward = (pos - Projectile.Center).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(0.6f, 1.2f);
                PRTLoader.NewParticle<PRT_VerdantMist>(pos, outward,
                    new Color(150, 166, 142), Main.rand.NextFloat(0.3f, 0.5f))?.Configure(60);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //淡底衬（画在实体层之下，人走进雾里有前后夹层感；主体遮蔽在渲染层）
            float density = Density;
            if (density < 0.03f) {
                return false;
            }
            Texture2D fog = CWRAsset.Fog?.Value;
            if (fog == null) {
                return false;
            }
            Color veil = (Main.dayTime ? new Color(146, 162, 138) : new Color(112, 128, 110)) * (0.16f * density);
            float px = FogRadius * 1.7f;
            float scale = px / (fog.Width * 0.8f);
            for (int i = 0; i < 3; i++) {
                float t = Main.GlobalTimeWrappedHourly * 0.04f + Projectile.identity * 1.7f + i * 2.1f;
                Vector2 off = new(MathF.Sin(t) * FogRadius * 0.28f, MathF.Cos(t * 0.8f) * FogRadius * 0.14f);
                Main.EntitySpriteDraw(fog, Projectile.Center + off - Main.screenPosition, null,
                    veil, t * 0.3f, fog.Size() / 2f, scale * (0.8f + 0.15f * i), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
