using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Flails
{
    /// <summary>
    /// 【连枷·谧海旋锤 ★A】谧海兽钢泡锤：猪鲨甲壳锻的锤，链身挤压海水成泡刃。<br/>
    /// 签名行为：①泡刃巡群：甩转充能过四成每 9 帧、掷出/收链每 6 帧，沿链身挤出追踪泡刃
    /// （40% 伤害，全场上限 8，缓加速缓转向，触敌爆裂）②满转环放：满转实打命中以目标为心
    /// 环放 6 枚泡刃 ③水爆命中：闷水声
    /// </summary>
    internal class GsFlairon : GsFlailScheme
    {
        public override int TargetItemID => ItemID.Flairon;

        protected override int FlailProjType => ModContent.ProjectileType<GsFlaironHead>();

        protected override string GsDescFallback =>
            "Reforged: the chain squeezes out homing bubble blades while swinging and flying (up to 8 afield, 40% damage each)\nA full-spin strike bursts a ring of six bubble blades around the target";
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.02f;
    }

    /// <summary>
    /// 谧海旋锤锤头。挤泡三时机：甩转充能>0.4 每 9 帧、掷出每 6 帧、收链回卷段每 6 帧，
    /// 生成位置取链身中后段采样点、初速沿链法线；满转实打命中环放 6 枚（identity 播种起相）。
    /// 泡刃全走 owner 端生成随包广播
    /// </summary>
    internal class GsFlaironHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.Flairon;
        public override int VanillaProjID => ProjectileID.Flairon;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain37;

        public override float MaxChainLength => 360f;
        public override float LaunchSpeed => 17.5f;
        public override int ChargeFrames => 42;

        /// <summary>泡刃伤害系数</summary>
        private const float BubbleDamageMul = 0.4f;

        protected override void OnSpinTick(float charge) {
            //挤泡：转速上来后链身开始出泡
            if (charge > 0.4f && spinTimer % 9 == 0) {
                SqueezeBubble();
            }
        }

        protected override void OnLaunch(float charge) {
            if (VaultUtils.isServer) {
                return;
            }
            //水压蓄旋音
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = 0.85f,
                Pitch = 0.15f + charge * 0.2f
            }, Owner.Center);
        }

        protected override void PostStateAI() {
            //掷出/收链沿链挤泡（回卷段才挤，回坠塌垂时链是松的挤不出）
            if ((State == StateLaunch && flightTimer % 6 == 0)
                || (State == StateRetract && retractTimer > RetractSagFrames && retractTimer % 6 == 0)) {
                SqueezeBubble();
            }
        }

        /// <summary>沿链身中后段挤出一枚泡刃：位置取 chainPoints 采样点，初速沿链法线</summary>
        private void SqueezeBubble() {
            if (!Projectile.IsOwnedByLocalPlayer() || chainPoints.Count < 6) {
                return;
            }
            int type = ModContent.ProjectileType<GsFlaironBubbleProj>();
            //全场上限 8，超限不挤
            if (Owner.ownedProjectileCounts[type] >= GsFlaironBubbleProj.FieldCap) {
                return;
            }
            int i = Main.rand.Next((int)(chainPoints.Count * 0.45f), (int)(chainPoints.Count * 0.8f));
            Vector2 at = chainPoints[i];
            Vector2 seg = chainPoints[Math.Min(i + 1, chainPoints.Count - 1)]
                - chainPoints[Math.Max(i - 1, 0)];
            Vector2 normal = seg.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                * (Main.rand.NextBool() ? 1f : -1f);
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), at,
                normal * Main.rand.NextFloat(1.6f, 2.6f), type,
                Math.Max(1, (int)(Projectile.damage * BubbleDamageMul)), 0.5f, Projectile.owner);
        }

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //满转环放：以目标为心 6 枚泡刃，起相由 identity 播种（各端方向一致）
            if (LaunchCharge >= 0.99f && State == StateLaunch) {
                const int ringCount = 6;
                int type = ModContent.ProjectileType<GsFlaironBubbleProj>();
                float basePhase = Projectile.identity * 0.61f;
                for (int i = 0; i < ringCount; i++) {
                    Vector2 dir = (MathHelper.TwoPi * i / ringCount + basePhase).ToRotationVector2();
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        target.Center + dir * 26f, dir * 2.4f, type,
                        Math.Max(1, (int)(Projectile.damage * BubbleDamageMul)), 0.5f, Projectile.owner);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Splash with {
                        Volume = 0.7f,
                        Pitch = 0.35f,
                        MaxInstances = 3
                    }, target.Center);
                }
            }
        }

        /// <summary>水爆命中：闷水声，替换族默认铁感音</summary>
        protected override void PlayHitSound(NPC target, float charge) {
            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.6f,
                Pitch = -0.35f,
                MaxInstances = 3
            }, target.Center);
        }
    }

    /// <summary>
    /// 泡刃：链身挤出的追踪水泡（40% 伤害，存续 96 帧）。飘出段沿链法线滑行减速，
    /// 追踪段缓加速缓转向（速度上限 7）外加 identity 播种游摆，绝不匀速直飞；触敌爆裂。<br/>
    /// 原版谧海泡贴图默认绘制，淡入淡出走 Opacity
    /// </summary>
    internal class GsFlaironBubbleProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlaironBubble;

        /// <summary>全场泡刃上限（挤泡侧查询）</summary>
        internal const int FieldCap = 8;

        private const int LifeFrames = 96;
        private const int DriftFrames = 14;
        private const int FadeInFrames = 6;
        private const int FadeOutFrames = 14;
        private const float MaxSpeed = 7f;

        /// <summary>identity 播种相位，游摆不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = LifeFrames;
        }

        private float Opacity {
            get {
                if (Projectile.timeLeft > LifeFrames - FadeInFrames) {
                    return (LifeFrames - Projectile.timeLeft) / (float)FadeInFrames;
                }
                if (Projectile.timeLeft < FadeOutFrames) {
                    return Projectile.timeLeft / (float)FadeOutFrames;
                }
                return 1f;
            }
        }

        public override void AI() {
            int age = LifeFrames - Projectile.timeLeft;
            if (age < DriftFrames) {
                //飘出段：沿链法线滑出并减速
                Projectile.velocity *= 0.95f;
            }
            else {
                NPC target = FindTarget();
                if (target != null) {
                    //缓加速+缓转向：速度慢慢爬到上限，方向小步拐
                    float speed = MathF.Min(MaxSpeed, Projectile.velocity.Length() + 0.14f);
                    Vector2 cur = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
                    Vector2 want = Projectile.Center.To(target.Center).SafeNormalize(cur);
                    Vector2 dir = Vector2.Lerp(cur, want, 0.08f).SafeNormalize(want);
                    Projectile.velocity = dir * speed;
                }
                else {
                    //无标的：水泡本性缓缓上浮
                    Projectile.velocity *= 0.97f;
                    Projectile.velocity.Y -= 0.015f;
                }
                //identity 播种游摆：轨迹永远带弧，杜绝匀速直线
                Projectile.velocity = Projectile.velocity.RotatedBy(
                    MathF.Sin(Main.GameUpdateCount * 0.12f + Seed) * 0.03f);
            }
            //淡入淡出交给默认绘制的 alpha
            Projectile.Opacity = Opacity;
        }

        /// <summary>就近锁定可追踪敌人（各端同源数据，结果一致）</summary>
        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 560f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = npc.Center.Distance(Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>淡入淡出段不结伤</summary>
        public override bool? CanDamage() => Opacity > 0.4f ? null : false;

        public override void OnKill(int timeLeft) {
            //爆裂水声
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = 0.4f,
                Pitch = 0.55f,
                MaxInstances = 5
            }, Projectile.Center);
        }
    }
}
