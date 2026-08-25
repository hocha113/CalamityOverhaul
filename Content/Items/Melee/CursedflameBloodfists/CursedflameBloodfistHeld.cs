using CalamityOverhaul.Common;
using CalamityOverhaul.Content.DamageModify;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.CursedflameBloodfists
{
    /// <summary>
    /// 咒焰血拳握持弹幕。按住左键把玩家钉在原地灌拳，左右手交替，
    /// 每一拳结算一次身周范围伤害，并沿准星轰出 2 到 3 只飞行火焰拳
    /// </summary>
    internal class CursedflameBloodfistHeld : BaseHeldProj
    {
        public override string Texture => CursedflameFX.FistTexture;
        public override LocalizedText DisplayName => VaultUtils.GetLocalizedItemName<CursedflameBloodfist>();

        /// <summary>基础出拳间隔，实际值再除以近战攻速</summary>
        private const int BasePunchInterval = 7;
        /// <summary>出拳瞬间开判定，持续这么多帧</summary>
        private const int HitWindow = 2;
        /// <summary>身周护体判定半径，被围住时背后也打得到</summary>
        private const float GuardRadius = 72f;
        /// <summary>拳锋前方判定长度，比护体圈再远一截</summary>
        private const float PunchLine = 100f;
        /// <summary>收拳时拳锋离手心的距离</summary>
        private const float FistRest = 20f;
        /// <summary>出拳行程</summary>
        private const float FistReach = 62f;
        /// <summary>每拳飞拳的随机散射半角</summary>
        private const float FistSpread = 0.22f;
        /// <summary>连打期间每隔这么多帧扣一次蓝</summary>
        private const int ManaInterval = 30;
        private const int ManaPerInterval = 5;
        /// <summary>松手后的收拳帧数</summary>
        private const int WindDownFrames = 8;
        /// <summary>手臂残像寿命</summary>
        private const int TraceLife = 7;

        /// <summary>一段拳影，连打的速度感全靠它堆出来</summary>
        private struct FistTrace
        {
            public Vector2 Fist;
            public float Angle;
            public int Age;
            public float Scale;
            public bool Active;
        }

        private readonly FistTrace[] traces = new FistTrace[10];
        private readonly HashSet<int> punchHits = [];

        private int punchInterval = BasePunchInterval;
        private int punchTimer;
        private int punchIndex;
        private int hitWindow;
        private int manaTimer = ManaInterval;
        private int windDownTimer = -1;
        private int syncTimer;
        private int traceHead;
        private int facingDir = 1;
        private float aimAngle;
        private float extendT;
        private int armSide = 1;
        private bool started;

        /// <summary>
        /// 是否还按着左键。<see cref="Player.channel"/> 需要 itemAnimation 续着才为真，
        /// 而本武器的 CanUseItem 会挡掉重复使用，所以并上同样过网的 controlUseItem 兜底
        /// </summary>
        private bool Holding => Owner.channel || Owner.controlUseItem;
        private Vector2 Hand => Owner.GetPlayerStabilityCenter();
        private Vector2 AimDir => aimAngle.ToRotationVector2();
        private Vector2 PerpDir => (aimAngle + MathHelper.PiOver2).ToRotationVector2();
        private Vector2 FistPos => Hand + (AimDir * (FistRest + (extendT * FistReach))) + (PerpDir * (armSide * 9f));
        private float FistScale => 0.78f + (extendT * 0.12f);

        public override void SetDefaults() {
            //判定几何全部走 Colliding，这里只是给宽相一个够大的框
            Projectile.width = Projectile.height = 190;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = MeleeMagicDamageClass.Instance;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = BasePunchInterval;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Item.type != ModContent.ItemType<CursedflameBloodfist>() || Owner.dead || !Owner.active) {
                Projectile.Kill();
                return;
            }

            float speed = Owner.GetWeaponAttackSpeed(Item);
            if (speed <= 0f) {
                speed = 1f;
            }
            punchInterval = Math.Max(3, (int)MathF.Round(BasePunchInterval / speed));
            //同一目标每拳只吃一次，免疫冷却跟着攻速一起走
            Projectile.localNPCHitCooldown = punchInterval;

            UpdateAim();
            UpdatePose();

            if (windDownTimer < 0 && (!Holding || Owner.CCed || Owner.noItems || !DrainMana())) {
                windDownTimer = WindDownFrames;
            }

            if (windDownTimer < 0) {
                if (!started) {
                    started = true;
                    Punch();
                }
                else if (++punchTimer >= punchInterval) {
                    punchTimer = 0;
                    Punch();
                }
            }

            if (hitWindow > 0) {
                hitWindow--;
            }
            //拳收回来的速度比伸出去慢，留一点残影时间
            extendT = MathF.Max(0f, extendT - (1.25f / punchInterval));
            AgeTraces();
            //伸出段逐帧留像，连打的速度感靠这层堆出来
            if (extendT > 0.25f) {
                PushTrace();
            }
            AmbientFlame();
            Lighting.AddLight(FistPos, CursedflameFX.FlameGreen.ToVector3() * 0.75f);

            Projectile.Center = Hand;
            Projectile.rotation = aimAngle;
            Projectile.timeLeft = 30;

            if (windDownTimer >= 0 && --windDownTimer < 0) {
                Projectile.Kill();
            }
        }

        /// <summary>瞄准存在 velocity 里，本机算、按需同步</summary>
        private void UpdateAim() {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 want = (Main.MouseWorld - Hand).SafeNormalize(Vector2.UnitX);
                Vector2 prev = Projectile.velocity.SafeNormalize(want);
                //转向做平滑，连打时手不跟着鼠标抖
                Projectile.velocity = Vector2.Lerp(prev, want, 0.4f).SafeNormalize(want);
                if (++syncTimer >= 6) {
                    syncTimer = 0;
                    if (Vector2.Dot(prev, Projectile.velocity) < 0.998f) {
                        Projectile.netUpdate = true;
                    }
                }
            }

            aimAngle = Projectile.velocity.ToRotation();
            float cos = MathF.Cos(aimAngle);
            facingDir = MathF.Abs(cos) < 0.08f ? Owner.direction : Math.Sign(cos);
        }

        private void UpdatePose() {
            Owner.heldProj = Projectile.whoAmI;
            Owner.itemTime = Owner.itemAnimation = 2;
            Owner.itemRotation = aimAngle;
            Owner.ChangeDir(facingDir);

            float punchArm = aimAngle - MathHelper.PiOver2;
            float guardArm = aimAngle - MathHelper.PiOver2 - (facingDir * 0.55f);
            Player.CompositeArmStretchAmount thrust = extendT > 0.6f
                ? Player.CompositeArmStretchAmount.Full
                : extendT > 0.25f
                    ? Player.CompositeArmStretchAmount.ThreeQuarters
                    : Player.CompositeArmStretchAmount.Quarter;

            //出拳手打满，另一只手收在胸前当护手，逐拳交换
            if (armSide > 0) {
                Owner.SetCompositeArmFront(true, thrust, punchArm);
                Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Quarter, guardArm);
            }
            else {
                Owner.SetCompositeArmBack(true, thrust, punchArm);
                Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Quarter, guardArm);
            }
        }

        /// <summary>连打期间的持续蓝耗，断蓝就收手</summary>
        private bool DrainMana() {
            if (--manaTimer > 0) {
                return true;
            }
            manaTimer = ManaInterval;
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return true;
            }
            return Owner.CheckMana(Item, ManaPerInterval, true);
        }

        private void Punch() {
            punchIndex++;
            armSide = -armSide;
            //拳是瞬间捅出去的，动画只负责往回收
            extendT = 1f;
            hitWindow = HitWindow;
            punchHits.Clear();
            PlayPunchSound();
            SpawnPunchFlame();
            FireFists();

            if (!VaultUtils.isServer && punchIndex % 4 == 0 && CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    Owner.Center, AimDir, 2.4f, 9f, 5, 700f, FullName));
            }
        }

        private void PlayPunchSound() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item1 with {
                Pitch = 0.55f + (punchIndex % 3 * 0.07f),
                Volume = 0.22f,
                MaxInstances = 4,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, Owner.Center);

            if (punchIndex % 3 == 0) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with {
                    Pitch = 0.45f,
                    Volume = 0.28f,
                    MaxInstances = 3,
                    SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
                }, Owner.Center);
            }
        }

        private void FireFists() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //每三拳多塞一只，成串而不是成排
            int count = punchIndex % 3 == 0 ? 3 : 2;
            Vector2 origin = FistPos;
            for (int i = 0; i < count; i++) {
                float ang = aimAngle + Main.rand.NextFloat(-FistSpread, FistSpread);
                Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(14f, 19f);
                //沿飞行方向往回错开，读成前后追着走的一串拳
                Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), origin - (vel * (i * 0.34f)), vel,
                    ModContent.ProjectileType<CursedflameFistProj>(),
                    (int)(Projectile.damage * 0.4f), Projectile.knockBack * 0.6f, Owner.whoAmI,
                    ai0: armSide);
            }
        }

        private void SpawnPunchFlame() {
            if (VaultUtils.isServer) {
                return;
            }
            Vector2 fist = FistPos;
            Vector2 dir = AimDir;
            for (int i = 0; i < 3; i++) {
                Vector2 lick = dir.RotatedByRandom(0.55);
                PRTLoader.NewParticle<PRT_CursedTongue>(fist + (lick * 5f)
                    , lick * Main.rand.NextFloat(1.3f, 3.1f)
                    , CursedflameFX.FlameGreen, Main.rand.NextFloat(0.34f, 0.56f))
                    .Configure(lick, Main.rand.NextFloat(0.7f, 1.25f), Main.rand.Next(4, 8));
            }
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_CursedEmber>(fist + Main.rand.NextVector2Circular(7f, 7f)
                    , (dir * Main.rand.NextFloat(2f, 5.5f)).RotatedByRandom(0.7)
                    , CursedflameFX.FlameGreen, Main.rand.NextFloat(0.7f, 1.15f))
                    .Configure(Main.rand.Next(14, 24));
            }
        }

        /// <summary>不出拳的间隙也让拳锋烧着，火不能只在出手那一帧存在</summary>
        private void AmbientFlame() {
            if (VaultUtils.isServer || windDownTimer >= 0 || !Main.rand.NextBool(3)) {
                return;
            }
            Vector2 up = -Vector2.UnitY.RotatedByRandom(0.6);
            PRTLoader.NewParticle<PRT_CursedTongue>(FistPos + Main.rand.NextVector2Circular(5f, 5f)
                , up * Main.rand.NextFloat(0.6f, 1.5f)
                , CursedflameFX.FlameMoss, Main.rand.NextFloat(0.22f, 0.4f))
                .Configure(up, Main.rand.NextFloat(0.6f, 1f), Main.rand.Next(4, 7));
        }

        private void PushTrace() {
            traceHead = (traceHead + 1) % traces.Length;
            traces[traceHead] = new FistTrace {
                Fist = FistPos,
                Angle = aimAngle,
                Age = 0,
                Scale = FistScale,
                Active = true
            };
        }

        private void AgeTraces() {
            for (int i = 0; i < traces.Length; i++) {
                if (!traces[i].Active) {
                    continue;
                }
                if (++traces[i].Age > TraceLife) {
                    traces[i].Active = false;
                }
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (hitWindow <= 0) {
                return false;
            }
            Vector2 hand = Hand;
            //身周护体，画面明明重叠却打不到最伤手感
            if (targetHitbox.Distance(hand) <= GuardRadius) {
                return true;
            }
            float collisionPoint = 0f;
            Vector2 tip = hand + (AimDir * PunchLine);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , hand, tip, 48f, ref collisionPoint);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = facingDir;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //同一拳对同一目标只转发一次外部近战命中钩子
            if (punchHits.Add(target.whoAmI)) {
                ItemLoader.OnHitNPC(Item, Owner, target, hit, damageDone);
                NPCLoader.OnHitByItem(target, Owner, Item, hit, damageDone);
                PlayerLoader.OnHitNPC(Owner, target, hit, damageDone);
            }
            target.AddBuff(BuffID.CursedInferno, 240);

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with {
                Pitch = 0.75f,
                Volume = 0.16f,
                MaxInstances = 3,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, target.Center);

            Vector2 back = -AimDir;
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_CursedEmber>(target.Center + Main.rand.NextVector2Circular(10f, 10f)
                    , (back * Main.rand.NextFloat(1.6f, 4.4f)).RotatedByRandom(0.9)
                    , CursedflameFX.FlameGreen, Main.rand.NextFloat(0.75f, 1.2f))
                    .Configure(Main.rand.Next(12, 22));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            if (tex == null) {
                return false;
            }

            Vector2 handPos = Hand;
            Color light = Lighting.GetColor(handPos.ToTileCoordinates());
            light.A = 255;

            //旧残像先画，越旧越淡越向绿焰偏，堆出连打的速度线
            for (int i = 1; i <= traces.Length; i++) {
                ref FistTrace t = ref traces[(traceHead + i) % traces.Length];
                if (!t.Active) {
                    continue;
                }
                float fade = 1f - (t.Age / (float)(TraceLife + 1));
                Color ghost = Color.Lerp(light, CursedflameFX.FlameMoss, 0.55f + (0.35f * (1f - fade)));
                ghost.A = 0;
                DrawFist(sb, tex, t.Fist, t.Angle, t.Scale * (0.88f + (0.12f * fade)), ghost * (fade * fade * 0.5f));
            }

            DrawFist(sb, tex, FistPos, aimAngle, FistScale, light);
            DrawFistFlame(sb);
            return false;
        }

        /// <summary>拳锋在贴图下端，原点压在拳锋附近，旋转补 <see cref="CursedflameFX.FistRotationOffset"/></summary>
        private void DrawFist(SpriteBatch sb, Texture2D tex, Vector2 fist, float angle, float scale, Color color) {
            //原点横向居中，镜像时无需另算，只翻 SpriteEffects 保住拳头正反面
            var origin = new Vector2(tex.Width * 0.5f, tex.Height * 0.72f);
            SpriteEffects flip = facingDir == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            sb.Draw(tex, fist - Main.screenPosition, null, color
                , angle + CursedflameFX.FistRotationOffset, origin, scale, flip, 0f);
        }

        private void DrawFistFlame(SpriteBatch sb) {
            Texture2D glow = CursedflameFX.SoftGlow;
            if (glow == null) {
                return;
            }
            Vector2 pos = FistPos - Main.screenPosition;
            Vector2 origin = glow.Size() * 0.5f;
            float pulse = 0.82f + (0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 22f));
            float heat = 0.45f + (extendT * 0.55f);

            Color outer = CursedflameFX.FlameOrange with { A = 0 };
            sb.Draw(glow, pos, null, outer * (0.45f * heat * pulse), 0f, origin, 0.52f, SpriteEffects.None, 0f);
            Color mid = CursedflameFX.FlameGreen with { A = 0 };
            sb.Draw(glow, pos, null, mid * (0.7f * heat * pulse), 0f, origin, 0.3f, SpriteEffects.None, 0f);
            Color core = CursedflameFX.FlameCore with { A = 0 };
            sb.Draw(glow, pos, null, core * (0.6f * heat), 0f, origin, 0.13f, SpriteEffects.None, 0f);
        }
    }
}
