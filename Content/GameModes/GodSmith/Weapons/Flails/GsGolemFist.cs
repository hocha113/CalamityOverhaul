using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 【连枷·石巨人之拳 ★A】玄武岩熔核重拳：玄武岩拳壳包着熔岩核，越打越烫，烫到岩缝渗熔光。<br/>
    /// 签名行为：①热量双相：实打命中攒热 0~3 层（每层 +8% 伤害，8 秒不打冷却），
    /// 满层进炽熔态：熔纹皮肤+熔滴尾+命中崩 3 枚熔石 ②岩震：每记砸中敌人或撞砖都轰出
    /// 扩张岩震环（35% 小 AOE）+ 距离衰减屏震 ③直拳弹道：Brace 蓄压后平射不掉弧。<br/>
    /// A 档四相预算：出手=蓄压震颤（基类 Brace 臂姿）+拳风音+岩尘爆；
    /// 飞行=热度皮肤（heat 经 ai[2] 过线各端同源）+炽熔熔滴尾+族速度残影；
    /// 命中=岩震环+炽熔熔石+屏震+族反冲顿感；余痕=熔石落地熔斑 ~64 帧+PRT_LavaFire 驻留 1.5s+岩震岩尘
    /// </summary>
    internal class GsGolemFist : GsFlailScheme
    {
        public override int TargetItemID => ItemID.GolemFist;

        protected override int FlailProjType => ModContent.ProjectileType<GsGolemFistHead>();

        protected override string GsDescFallback =>
            "Reforged: solid hits stack heat (up to 3, +8% damage each, fades after 8 seconds); at full heat the fist runs molten and impacts hurl 3 magma chunks" +
            "\nEvery punch that lands on a foe or a wall slams out a stone shockwave";

        //玄武岩熔核色板
        internal static readonly Color BasaltGray = new(122, 114, 108);   //玄武灰
        internal static readonly Color MagmaOrange = new(255, 148, 58);   //熔橙
        internal static readonly Color CoreRed = new(178, 44, 32);        //暗红核

        internal const int HeatMax = 3;
        /// <summary>热量衰减窗口（8 秒）</summary>
        private const int DecayFrames = 480;

        /// <summary>当前热量 0~3；方案单例，只在 myPlayer 守门路径读写</summary>
        private int heat;
        /// <summary>衰减倒计时，只在 myPlayer 守门路径读写</summary>
        private int decayTimer;

        /// <summary>实打命中回写热量（锤头 owner 端调用，owner==myPlayer 即守门达成）</summary>
        internal void AddHeat() {
            heat = Math.Min(heat + 1, HeatMax);
            decayTimer = DecayFrames;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //8 秒没有新命中：热量整段冷掉
            if (decayTimer > 0 && --decayTimer == 0) {
                heat = 0;
            }
        }

        /// <summary>出手时把当前热量写进 ai[2] 随生成包过线，各端呈现同样的热度皮肤</summary>
        protected override float LaunchAi2(Player player, int index) => heat;

        //热层至 +24%、岩震 35% 小 AOE、炽熔熔石 3×50%，机制收益大，底伤克制在 8%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 石巨人之拳锤头。直拳弹道：SelfSpinHead 关、蓄压 Brace 姿态、
    /// PostStateAI 抵掉大半基类微重力；热量皮肤按 ai[2] 各端同源绘制；
    /// 命中/撞砖轰岩震环，炽熔态命中追加 3 枚熔石
    /// </summary>
    internal class GsGolemFistHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.GolemFist;
        public override int VanillaProjID => ProjectileID.GolemFist;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain22;
        public override Color GlowColor => GsGolemFist.MagmaOrange;

        //直拳手感：快、直、收得干脆
        public override int HeadSize => 34;
        public override float MaxChainLength => 380f;
        public override float LaunchSpeed => 19.5f;
        public override int LaunchFrames => 16;
        public override int RetractSagFrames => 5;
        public override int ChargeFrames => 44;
        public override GsFlailSpinMode SpinMode => GsFlailSpinMode.Brace;
        public override bool SelfSpinHead => false;

        /// <summary>出手时锁定的热量层数（ai[2] 过线，各端一致）</summary>
        private int Heat => Math.Clamp((int)WeaponAi2, 0, GsGolemFist.HeatMax);
        /// <summary>炽熔态：满 3 层</summary>
        private bool Molten => Heat >= GsGolemFist.HeatMax;
        /// <summary>identity 播种相位，绘制抖动不掷 Main.rand</summary>
        private float Seed => Projectile.identity * 0.917f;

        private const float QuakeDamageMul = 0.35f;
        private const float MagmaDamageMul = 0.5f;

        /// <summary>热层加成：每层 +8%</summary>
        protected override void ModifyFlailHit(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.SourceDamage *= 1f + 0.08f * Heat;

        protected override void PostStateAI() {
            if (State == StateLaunch) {
                //直拳弹道：抵掉大半基类微重力（0.09），只留一丝下压
                Projectile.velocity.Y -= 0.06f;
            }
            if (State == StateSpin) {
                //蓄压期拳面咬住出手朝向（direction 已随原版同步）
                Projectile.rotation = Owner.direction >= 0 ? 0f : MathHelper.Pi;
            }
            //炽熔态飞行熔滴尾：熔滴带重力下坠，偶发火舌
            if (VaultUtils.isServer || !Molten || State == StateSpin) {
                return;
            }
            if (Main.rand.NextBool(State == StateLaunch ? 1 : 2)) {
                Dust drip = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), DustID.Torch,
                    -Projectile.velocity * 0.08f + Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.2f),
                    80, default, Main.rand.NextFloat(1.1f, 1.6f));
                drip.noGravity = false;
            }
            if (Main.rand.NextBool(7)) {
                Dust lava = Dust.NewDustPerfect(Projectile.Center, DustID.Lava,
                    -Projectile.velocity * 0.1f, 60, default, Main.rand.NextFloat(0.8f, 1.1f));
                lava.noGravity = false;
            }
        }

        protected override void OnSpinTick(float charge) {
            //蓄压岩尘：拳壳越震颤崩得越密；炽熔态改崩火星
            if (VaultUtils.isServer || charge < 0.35f || spinTimer % 5 != 0) {
                return;
            }
            Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f),
                Molten ? DustID.Torch : DustID.Stone,
                Main.rand.NextVector2Circular(1.2f, 1.2f) - Vector2.UnitY * 0.5f,
                110, default, Main.rand.NextFloat(0.8f, 1.2f));
            d.noGravity = true;
        }

        protected override void OnLaunch(float charge) {
            if (VaultUtils.isServer) {
                return;
            }
            //拳风音 + 蓄压岩尘爆（沿出手向喷）
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                Volume = 0.9f,
                Pitch = -0.3f + charge * 0.25f
            }, Owner.Center);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX * Owner.direction);
            for (int i = 0; i < 10; i++) {
                Dust d = Dust.NewDustPerfect(Owner.MountedCenter + dir * 22f, DustID.Stone,
                    dir.RotatedByRandom(0.5) * Main.rand.NextFloat(2f, 7f),
                    120, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = Main.rand.NextBool();
            }
            if (Molten) {
                for (int i = 0; i < 4; i++) {
                    Dust d = Dust.NewDustPerfect(Owner.MountedCenter + dir * 22f, DustID.Torch,
                        dir.RotatedByRandom(0.4) * Main.rand.NextFloat(3f, 6f), 80, default, 1.3f);
                    d.noGravity = true;
                }
            }
        }

        protected override void OnHeadHit(NPC target, NPC.HitInfo hit, int damageDone, bool headHit) {
            if (!headHit || !Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            //热量回写：owner==myPlayer，方案单例守门路径
            if (GodSmithScheme.TryGetScheme(ItemID.GolemFist, out GodSmithScheme scheme)
                && scheme is GsGolemFist fist) {
                fist.AddHeat();
            }
            //岩震：命中点轰扩张震环（35% 小 AOE，屏震在震环首帧各端自算）
            SpawnQuake(target.Center);
            //炽熔态命中：崩 3 枚带重力熔石
            if (Molten) {
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = (-Vector2.UnitY * Main.rand.NextFloat(6f, 9f))
                        .RotatedBy(Main.rand.NextFloat(-0.6f, 0.6f));
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(),
                        target.Center - Vector2.UnitY * 12f, vel,
                        ModContent.ProjectileType<GsGolemFistMagmaProj>(),
                        Math.Max(1, (int)(Projectile.damage * MagmaDamageMul)), 2f, Projectile.owner);
                }
            }
        }

        protected override void OnTileImpact(Vector2 oldVelocity) {
            //撞砖岩震（tileCollide 只在掷出态开，owner 端生成随包广播）
            if (Projectile.IsOwnedByLocalPlayer() && State == StateLaunch) {
                SpawnQuake(Projectile.Center);
            }
        }

        private void SpawnQuake(Vector2 at) {
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), at, Vector2.Zero,
                ModContent.ProjectileType<GsGolemFistQuakeProj>(),
                Math.Max(1, (int)(Projectile.damage * QuakeDamageMul)), 3f, Projectile.owner);
        }

        /// <summary>族反馈之上补岩拳材质：石屑迸溅，带热再加熔火星</summary>
        protected override void SpawnHitBurst(NPC target, NPC.HitInfo hit, float charge) {
            base.SpawnHitBurst(target, hit, charge);
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(target.Center,
                    DustID.Stone, Main.rand.NextVector2Circular(4f, 3f) - Vector2.UnitY,
                    110, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
            for (int i = 0; i < Heat * 2; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Torch,
                    Main.rand.NextVector2Circular(3.5f, 3.5f), 80, default, Main.rand.NextFloat(1f, 1.5f));
                d.noGravity = true;
            }
        }

        /// <summary>玄武岩链条近头渗熔光，热越高透得越亮</summary>
        public override Color ChainLinkColor(int linkIndex, float t, Color light)
            => Heat <= 0 ? light : Color.Lerp(light, GsGolemFist.MagmaOrange, t * t * 0.18f * Heat);

        /// <summary>热度皮肤：1~2 层暖橙薄罩，满层炽熔=熔光衬+玄武暗压+熔橙罩+岩缝熔纹闪烁</summary>
        protected override void PostDrawHead(Color lightColor, float headRotation, Rectangle frame, Vector2 origin) {
            if (Heat <= 0) {
                return;
            }
            Texture2D tex = TextureAssets.Projectile[VanillaProjID].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;

            if (!Molten) {
                //低热：拳壳透出的暖橙薄罩，层数越高越亮
                Color warm = GsGolemFist.MagmaOrange * (0.13f * Heat);
                warm.A = 0;
                Main.EntitySpriteDraw(tex, pos, frame, warm, headRotation, origin,
                    Projectile.scale * 1.03f, SpriteEffects.None, 0);
                return;
            }

            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            Texture2D dark = CWRAsset.Extra_98?.Value;
            if (glow == null || star == null || dark == null) {
                return;
            }
            //identity 播种的熔光呼吸，不掷 Main.rand
            float breathe = 0.85f + 0.15f * MathF.Sin(Main.GameUpdateCount * 0.09f + Seed);

            //熔光底衬（加色）：整拳被内热烘出的光
            Color halo = GsGolemFist.MagmaOrange * (0.32f * breathe);
            halo.A = 0;
            Main.EntitySpriteDraw(glow, pos, null, halo, 0f, glow.Size() / 2f,
                frame.Height * 1.7f / glow.Height, SpriteEffects.None, 0);

            //玄武暗压层（真 alpha）：把拳壳压回岩石的深色
            Color press = new Color(30, 26, 24) * 0.32f;
            Main.EntitySpriteDraw(dark, pos, null, press, headRotation, dark.Size() / 2f,
                frame.Height * 1.1f / dark.Height, SpriteEffects.None, 0);

            //熔橙加色罩：岩壳整体烧透
            Color over = GsGolemFist.MagmaOrange * (0.42f * breathe);
            over.A = 0;
            Main.EntitySpriteDraw(tex, pos, frame, over, headRotation, origin,
                Projectile.scale * 1.05f, SpriteEffects.None, 0);

            //岩缝熔纹闪烁 ×3：identity 定位、各自错相明灭
            for (int i = 0; i < 3; i++) {
                float ph = Seed + i * 2.09f;
                float flick = 0.5f + 0.5f * MathF.Sin(Main.GameUpdateCount * 0.21f + ph * 3.7f);
                Vector2 off = new Vector2(MathF.Sin(ph * 5.3f), MathF.Cos(ph * 3.1f)) * 9f;
                Color crack = Color.Lerp(GsGolemFist.MagmaOrange, GsGolemFist.CoreRed, 0.35f)
                    * (0.55f * flick);
                crack.A = 0;
                Main.EntitySpriteDraw(star, pos + off, null, crack, ph,
                    star.Size() / 2f, 0.05f * (0.7f + 0.3f * flick), SpriteEffects.None, 0);
            }
        }
    }

    /// <summary>
    /// 岩震环：命中点轰出的扩张冲击波，~14 帧从小到大、透明度衰减，35% 小 AOE。
    /// 首帧闷响+距离衰减屏震（各客户端按自己与落点的距离结算）；
    /// 自绘：熔橙外环+暗红内环加色叠层，环沿撒岩尘补石质
    /// </summary>
    internal class GsGolemFistQuakeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int LifeFrames = 14;
        private const float MaxRadius = 88f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;//超过存续：每目标只结一次
            Projectile.timeLeft = LifeFrames;
        }

        public override bool ShouldUpdatePosition() => false;

        private float LifeT => 1f - Projectile.timeLeft / (float)LifeFrames;
        /// <summary>扩张曲线：先猛后缓，不匀速</summary>
        private float Radius => MathHelper.Lerp(14f, MaxRadius, 1f - (1f - LifeT) * (1f - LifeT));

        public override void AI() {
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item14 with {
                        Volume = 0.5f,
                        Pitch = -0.55f,
                        MaxInstances = 3
                    }, Projectile.Center);
                    //距离衰减屏震：各客户端按自己与落点的距离结算
                    if (CWRClientConfig.Instance.ScreenVibration) {
                        float dist = Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center);
                        float shake = MathHelper.Lerp(2f, 0f, MathHelper.Clamp(dist / 900f, 0f, 1f));
                        if (shake > 0.1f) {
                            Main.LocalPlayer.CWR()?.GetScreenShake(shake);
                        }
                    }
                }
            }
            //扩张环沿撒岩尘：石质身份由碎屑说话
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 at = Projectile.Center + ang.ToRotationVector2() * Radius;
                    Dust d = Dust.NewDustPerfect(at, DustID.Stone,
                        ang.ToRotationVector2() * Main.rand.NextFloat(1.5f, 3f),
                        120, default, Main.rand.NextFloat(0.8f, 1.2f));
                    d.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center,
                GsGolemFist.MagmaOrange.ToVector3() * 0.22f * (1f - LifeT));
        }

        /// <summary>扩张圆盘判定：环扫过即中，免疫窗覆盖全存续</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius + 8f;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (ring == null || glow == null) {
                return false;
            }
            float alpha = 1f - LifeT;
            float scale = Radius * 2f / ring.Width;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            //identity 定相，绘制不掷 Main.rand
            float rot = Projectile.identity * 0.917f;

            //熔橙外环（加色）：冲击波前锋
            Color outer = GsGolemFist.MagmaOrange * (0.55f * alpha);
            outer.A = 0;
            Main.EntitySpriteDraw(ring, pos, null, outer, rot, ring.Size() / 2f,
                scale, SpriteEffects.None, 0);
            //暗红内环（加色）：环内余热
            Color inner = GsGolemFist.CoreRed * (0.4f * alpha);
            inner.A = 0;
            Main.EntitySpriteDraw(ring, pos, null, inner, -rot * 0.7f, ring.Size() / 2f,
                scale * 0.72f, SpriteEffects.None, 0);
            //起爆心：头几帧一点熔光
            if (LifeT < 0.3f) {
                Color core = GsGolemFist.MagmaOrange * (0.5f * (1f - LifeT / 0.3f));
                core.A = 0;
                Main.EntitySpriteDraw(glow, pos, null, core, 0f, glow.Size() / 2f,
                    0.5f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 熔石：炽熔态命中崩出的带重力岩块（50% 伤害）。飞行=熔核加色+石壳真 alpha 双层+缝隙闪；
    /// 落地小熔爆后转熔斑余痕 ~64 帧（判定关闭，熔光渐冷），PRT_LavaFire 再驻留 1.5s
    /// </summary>
    internal class GsGolemFistMagmaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int ScorchFrames = 64;

        /// <summary>ai[0]=1 已落地转熔斑（各端按各自撞砖判定，位置已同步，结果一致）</summary>
        private bool Scorched => Projectile.ai[0] == 1f;
        private float Seed => Projectile.identity * 0.917f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            if (Scorched) {
                Projectile.velocity = Vector2.Zero;
                //熔斑余烬：偶发上飘火屑
                if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                    Dust d = Dust.NewDustPerfect(
                        Projectile.Center + Main.rand.NextVector2Circular(8f, 3f), DustID.Torch,
                        -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.9f), 100, default, 0.9f);
                    d.noGravity = true;
                }
                Lighting.AddLight(Projectile.Center, GsGolemFist.MagmaOrange.ToVector3()
                    * 0.16f * (Projectile.timeLeft / (float)ScorchFrames));
                return;
            }
            //抛物飞行：重力加速+按速自旋
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.32f, 16f);
            Projectile.rotation += Projectile.velocity.X * 0.05f + 0.06f;
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Torch,
                    -Projectile.velocity * 0.15f, 80, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, GsGolemFist.MagmaOrange.ToVector3() * 0.2f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            EnterScorch();
            return false;
        }

        /// <summary>落地：小熔爆一记，转熔斑余痕相</summary>
        private void EnterScorch() {
            if (Scorched) {
                return;
            }
            Projectile.ai[0] = 1f;
            Projectile.velocity = Vector2.Zero;
            Projectile.tileCollide = false;
            Projectile.timeLeft = ScorchFrames;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with {
                Volume = 0.32f,
                Pitch = -0.2f,
                MaxInstances = 3
            }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Lava,
                    Main.rand.NextVector2Circular(2.5f, 1.5f) - Vector2.UnitY * Main.rand.NextFloat(1f, 3f),
                    60, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
            //余痕预算：PRT_LavaFire 默认寿命 90 帧+，熔斑熄了火还在舔
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_LavaFire>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 2f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f),
                    GsGolemFist.MagmaOrange, Main.rand.NextFloat(0.25f, 0.4f));
            }
        }

        /// <summary>熔斑相纯余痕，不当磨床</summary>
        public override bool? CanDamage() => Scorched ? false : null;

        public override void OnKill(int timeLeft) {
            //飞行途中撞敌碎裂：同款小熔爆（落地路径由 EnterScorch 自演不走这里）
            if (Scorched || VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Lava,
                    Main.rand.NextVector2Circular(3f, 3f), 60, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = Main.rand.NextBool();
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Stone,
                    Main.rand.NextVector2Circular(3f, 3f), 110, default, 1f);
                d.noGravity = false;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D shell = CWRAsset.TearSpread01?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (glow == null || shell == null || star == null) {
                return false;
            }
            Vector2 pos = Projectile.Center - Main.screenPosition;

            if (Scorched) {
                //熔斑余痕：横铺熔光渐冷 + 冷凝壳碎片
                float fade = Projectile.timeLeft / (float)ScorchFrames;
                Color smear = Color.Lerp(GsGolemFist.CoreRed, GsGolemFist.MagmaOrange, fade)
                    * (0.5f * fade);
                smear.A = 0;
                Main.spriteBatch.Draw(glow, pos + Vector2.UnitY * 4f, null, smear, 0f,
                    glow.Size() / 2f, new Vector2(1.5f, 0.45f) * (0.6f + 0.4f * fade),
                    SpriteEffects.None, 0f);
                Color hot = GsGolemFist.MagmaOrange * (0.4f * fade);
                hot.A = 0;
                Main.spriteBatch.Draw(glow, pos + Vector2.UnitY * 3f, null, hot, 0f,
                    glow.Size() / 2f, new Vector2(0.8f, 0.25f), SpriteEffects.None, 0f);
                //冷凝石壳碎片（真 alpha 暗）
                Color crust = Color.Lerp(GsGolemFist.BasaltGray, Color.Black, 0.55f) * (0.7f * fade);
                Main.spriteBatch.Draw(shell, pos + Vector2.UnitY * 2f, null, crust,
                    Seed % MathHelper.TwoPi, shell.Size() / 2f, 0.11f, SpriteEffects.None, 0f);
                return false;
            }

            //identity 播种的核心呼吸
            float breathe = 0.8f + 0.2f * MathF.Sin(Main.GameUpdateCount * 0.14f + Seed);
            //熔核（加色）：光从岩缝里透出来
            Color halo = GsGolemFist.MagmaOrange * (0.35f * breathe);
            halo.A = 0;
            Main.spriteBatch.Draw(glow, pos, null, halo, 0f, glow.Size() / 2f,
                0.36f * breathe, SpriteEffects.None, 0f);
            Color core = Color.Lerp(GsGolemFist.MagmaOrange, Color.White, 0.25f) * (0.5f * breathe);
            core.A = 0;
            Main.spriteBatch.Draw(glow, pos, null, core, 0f, glow.Size() / 2f,
                0.18f, SpriteEffects.None, 0f);
            //玄武石壳（真 alpha）双层错转：包住熔核的岩块本体
            Color rock = Color.Lerp(GsGolemFist.BasaltGray, Color.Black, 0.45f);
            Main.spriteBatch.Draw(shell, pos, null, rock, Projectile.rotation,
                shell.Size() / 2f, 0.16f, SpriteEffects.None, 0f);
            Color rockLit = GsGolemFist.BasaltGray * 0.85f;
            Main.spriteBatch.Draw(shell, pos, null, rockLit, Projectile.rotation + MathHelper.PiOver2,
                shell.Size() / 2f, 0.12f, SpriteEffects.None, 0f);
            //缝隙闪（加色）：壳裂处的熔光尖
            Color glint = GsGolemFist.MagmaOrange * (0.5f * breathe);
            glint.A = 0;
            Main.spriteBatch.Draw(star, pos, null, glint, -Projectile.rotation * 0.6f,
                star.Size() / 2f, 0.1f * breathe, SpriteEffects.None, 0f);
            return false;
        }
    }
}
