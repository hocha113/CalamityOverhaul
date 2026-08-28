using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    /// 环放 6 枚泡刃 ③水爆命中：水花锥+青波纹环+闷水声。<br/>
    /// A 档四相预算：出手=水压蓄旋音+离心水花+族满转脉冲；飞行=族速度残影+泡沫尾（每 3 帧）
    /// +链身挤泡可见（泡口水花）；命中=水爆锥+PRT_StarPulseRing 青波纹+闷水声+满转环放；
    /// 余痕=残泡继续巡游至多 96 帧（1.6s）+爆泡水雾短驻
    /// </summary>
    internal class GsFlairon : GsFlailScheme
    {
        public override int TargetItemID => ItemID.Flairon;

        protected override int FlailProjType => ModContent.ProjectileType<GsFlaironHead>();

        protected override string GsDescFallback =>
            "Reforged: the chain squeezes out homing bubble blades while swinging and flying (up to 8 afield, 40% damage each)" +
            "\nA full-spin strike bursts a ring of six bubble blades around the target";

        //原版 Flairon 本就强势，泡群收益大（8×40% 驻场+满转 6 枚环放），底伤几乎不动
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
        //谧海泡锤色板
        internal static readonly Color SeaTeal = new(88, 205, 182);      //谧海青绿
        internal static readonly Color DeepIndigo = new(64, 84, 186);    //靛蓝
        internal static readonly Color FoamWhite = new(232, 246, 250);   //泡沫白

        public override int SourceItemID => ItemID.Flairon;
        public override int VanillaProjID => ProjectileID.Flairon;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain37;
        public override Color GlowColor => SeaTeal;

        public override float MaxChainLength => 360f;
        public override float LaunchSpeed => 17.5f;
        public override int ChargeFrames => 42;

        /// <summary>泡刃伤害系数</summary>
        private const float BubbleDamageMul = 0.4f;
        /// <summary>identity 播种相位，绘制抖动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        protected override void OnSpinTick(float charge) {
            //挤泡：转速上来后链身开始出泡
            if (charge > 0.4f && spinTimer % 9 == 0) {
                SqueezeBubble();
            }
            //离心水花：高转速链梢甩水
            if (!VaultUtils.isServer && charge > 0.5f && spinTimer % 4 == 0) {
                Vector2 tang = (spinAngle + MathHelper.PiOver2 * swingSign).ToRotationVector2();
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Water,
                    tang * Main.rand.NextFloat(2f, 5f), 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
        }

        protected override void OnLaunch(float charge) {
            if (VaultUtils.isServer) {
                return;
            }
            //水压蓄旋音 + 出手瞬间离心水花甩一圈
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = 0.85f, Pitch = 0.15f + charge * 0.2f
            }, Owner.Center);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center,
                    DustID.Water, Main.rand.NextVector2Circular(4f, 4f),
                    100, default, Main.rand.NextFloat(1f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
        }

        protected override void PostStateAI() {
            //掷出/收链沿链挤泡（回卷段才挤，回坠塌垂时链是松的挤不出）
            if ((State == StateLaunch && flightTimer % 6 == 0)
                || (State == StateRetract && retractTimer > RetractSagFrames && retractTimer % 6 == 0)) {
                SqueezeBubble();
            }
            //飞行泡沫尾
            if (!VaultUtils.isServer && State != StateSpin && Main.GameUpdateCount % 3 == 0) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.Water,
                    -Projectile.velocity * 0.15f, 110, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
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
            //挤泡可见：泡口一撮水花
            if (!VaultUtils.isServer) {
                Dust d = Dust.NewDustPerfect(at, DustID.Water, normal * 1.5f, 100, default, 1f);
                d.noGravity = true;
            }
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
                        Volume = 0.7f, Pitch = 0.35f, MaxInstances = 3
                    }, target.Center);
                }
            }
        }

        /// <summary>水爆命中：闷水声+水花锥+青波纹环+泡沫火花，满转波纹升级；替换族默认铁感反馈</summary>
        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            SoundEngine.PlaySound(SoundID.Splash with {
                Volume = 0.6f, Pitch = -0.35f, MaxInstances = 3
            }, target.Center);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            int drops = 6 + (int)(charge * 5f);
            for (int i = 0; i < drops; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Water,
                    -dir.RotatedByRandom(0.7) * Main.rand.NextFloat(2.5f, 7f),
                    100, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = Main.rand.NextBool();
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    -dir.RotatedByRandom(0.9) * Main.rand.NextFloat(2f, 5f),
                    FoamWhite, Main.rand.NextFloat(0.28f, 0.45f))?.Configure(true, Main.rand.Next(8, 14));
            }
            //青波纹环：满转命中放大一号
            bool full = charge >= 0.99f && State == StateLaunch;
            PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero,
                SeaTeal, full ? 0.55f : 0.32f);
            //水雾短驻（余痕）
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_CampfireBubble>(
                    target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f),
                    SeaTeal * 0.7f, Main.rand.NextFloat(0.3f, 0.5f));
            }
        }

        /// <summary>链身近头透出海水青：链条挤压海水的材质提示</summary>
        public override Color ChainLinkColor(int linkIndex, float t, Color light)
            => Color.Lerp(light, SeaTeal, t * 0.3f);

        /// <summary>兽钢湿面反光：青绿薄衬 + identity 播种的游走水珠高光</summary>
        protected override void PostDrawHead(Color lightColor, float headRotation, Rectangle frame, Vector2 origin) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || star == null) {
                return;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //湿润薄衬（加色），呼吸由 identity 播种
            float sheen = 0.14f + 0.08f * MathF.Sin(Main.GameUpdateCount * 0.07f + Seed);
            Color wet = SeaTeal * sheen;
            wet.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, wet, 0f, glow.Size() / 2f,
                frame.Height * 1.4f / glow.Height, SpriteEffects.None, 0);
            //游走水珠高光（加色）：沿锤面缓慢绕行
            float ph = Main.GameUpdateCount * 0.045f + Seed;
            Vector2 off = new Vector2(MathF.Cos(ph), MathF.Sin(ph * 1.7f)) * 8f;
            Color glint = FoamWhite * (0.3f + 0.15f * MathF.Sin(ph * 3f));
            glint.A = 0;
            Main.EntitySpriteDraw(star, pos + off, null, glint, ph, star.Size() / 2f,
                0.09f, SpriteEffects.None, 0);
        }
    }

    /// <summary>
    /// 泡刃：链身挤出的追踪水泡（40% 伤害，存续 96 帧）。飘出段沿链法线滑行减速，
    /// 追踪段缓加速缓转向（速度上限 7）外加 identity 播种游摆，绝不匀速直飞；触敌爆裂。<br/>
    /// 自绘四层：靛蓝深水衬+青绿泡体+扩散环泡壁+泡面白高光，x/y 反相呼吸形变
    /// </summary>
    internal class GsFlaironBubbleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>全场泡刃上限（挤泡侧查询）</summary>
        internal const int FieldCap = 8;

        private const int LifeFrames = 96;
        private const int DriftFrames = 14;
        private const int FadeInFrames = 6;
        private const int FadeOutFrames = 14;
        private const float MaxSpeed = 7f;

        /// <summary>identity 播种相位，绘制与游摆不掷 Main.rand</summary>
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
            //巡游泡沫屑
            if (!VaultUtils.isServer && Main.rand.NextBool(9)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Water,
                    -Projectile.velocity * 0.2f, 120, default, 0.8f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GsFlaironHead.SeaTeal.ToVector3() * 0.1f * Opacity);
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
            //爆裂水花 + 水雾余痕
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with {
                Volume = 0.4f, Pitch = 0.55f, MaxInstances = 5
            }, Projectile.Center);
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Water,
                    Main.rand.NextVector2Circular(3f, 3f), 100, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
            for (int i = 0; i < 2; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BubbleBurst_Blue,
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 100, default, 1f);
                d.noGravity = true;
            }
            PRTLoader.NewParticle<PRT_CampfireBubble>(Projectile.Center,
                -Vector2.UnitY * 0.5f, GsFlaironHead.SeaTeal * 0.7f, Main.rand.NextFloat(0.25f, 0.4f));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || ring == null || star == null) {
                return false;
            }
            float alpha = Opacity;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //identity 播种形变：x/y 反相呼吸，泡是软的
            float wob = MathF.Sin(Main.GameUpdateCount * 0.13f + Seed);
            Vector2 deform = new(1f + 0.14f * wob, 1f - 0.14f * wob);

            //靛蓝深水衬（加色）：泡身后的水深
            Color depth = GsFlaironHead.DeepIndigo * (0.22f * alpha);
            depth.A = 0;
            Main.spriteBatch.Draw(glow, pos, null, depth, 0f, glow.Size() / 2f,
                deform * 0.55f, SpriteEffects.None, 0f);
            //青绿泡体（加色低强度垫底）
            Color body = GsFlaironHead.SeaTeal * (0.3f * alpha);
            body.A = 0;
            Main.spriteBatch.Draw(glow, pos, null, body, 0f, glow.Size() / 2f,
                deform * 0.38f, SpriteEffects.None, 0f);
            //泡壁 rim（加色）：扩散环即泡膜，跟着形变呼吸
            Color rim = Color.Lerp(GsFlaironHead.SeaTeal, GsFlaironHead.FoamWhite, 0.45f)
                * (0.75f * alpha);
            rim.A = 0;
            float rimScale = Projectile.width * 1.4f / ring.Width;
            Main.spriteBatch.Draw(ring, pos, null, rim, Seed % MathHelper.TwoPi,
                ring.Size() / 2f, deform * rimScale, SpriteEffects.None, 0f);
            //泡面高光（加色）：左上白点随呼吸游移
            Color glint = GsFlaironHead.FoamWhite * ((0.38f + 0.18f * wob) * alpha);
            glint.A = 0;
            Main.spriteBatch.Draw(star, pos + new Vector2(-4f, -4f) * deform, null, glint,
                Seed, star.Size() / 2f, 0.1f + 0.02f * wob, SpriteEffects.None, 0f);
            return false;
        }
    }
}
