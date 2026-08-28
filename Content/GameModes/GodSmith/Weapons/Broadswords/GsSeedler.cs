using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【荆棘园圃】材质：世纪之花心木配阳绿汁液的园艺重剑。
    /// 签名：①原版种荚保留升级：每一斩掷出旋转木荚，弹跳带重力，
    /// 炸裂成扇形非匀速的荆棘弹雨（轻微追击）②终结拍种荚化作藤蔓爆发，
    /// 落点竖起驻留荆棘丛持续刺击 ③命中反馈木屑与汁液绿尘分流。
    /// 四相生命周期：出手荚旋 / 飞行叶迹 / 命中爆荚 / 余痕荆棘丛参差消散
    /// </summary>
    internal class GsSeedler : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Seedler;

        protected override int HeldProjID => ModContent.ProjectileType<GsSeedlerHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash hurls a spinning seed pod that bounces and bursts " +
            "into a fan of seeking thorns; the finisher's pod erupts into vines, " +
            "raising a lingering bramble patch that keeps stabbing";

        //园圃色板
        internal static readonly Color SeedBright = new(216, 255, 160); //阳绿亮缘
        internal static readonly Color SeedMain = new(112, 192, 72);    //叶身翠绿
        internal static readonly Color SeedHot = new(250, 220, 96);     //汁液金绿
        internal static readonly Color SeedDeep = new(30, 42, 20);      //腐叶暗绿
        internal static readonly Color SeedWood = new(146, 98, 54);     //心木棕

        //原版链按单体实战 斩1.0+荚1.0+落点刺0.5~1.0 ≈ 2.5~3.0x/23帧 估；
        //本方案三拍循环 ~65 帧：斩 1.0+1.0+1.3、荚 0.7x 三发（终结荚吃 1.3 拍倍率）、
        //刺 5×0.3x/荚（单体实战 2~3 中）、荆棘丛终结命中约 4 跳×0.2x →
        //单体约 8.5x/65 帧，对原版估算区间 ≈ 101%~120%；弹雨与棘丛是范围收益，底伤不动
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 荆棘园圃手持：三拍连段。0 横斩 / 1 返斩 / 2 播种重劈（前压终结）。
    /// 每拍斩切爆发掷出种荚，终结拍的荚携藤蔓爆发旗。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsSeedlerHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Seedler;
        protected override Color EdgeBright => GsSeedler.SeedBright;
        protected override Color BodyMain => GsSeedler.SeedMain;
        protected override Color HotAccent => GsSeedler.SeedHot;
        protected override Color DeepShadow => GsSeedler.SeedDeep;

        private bool podThrown;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.8f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.04f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.12f,
            },
            //拍2 播种：长举重劈，把荚砸进土里
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.14f, LeanAmp = 0.085f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.26f,
            },
        };

        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsSeedler.SeedMain, 0.1f);

        /// <summary>每拍斩切爆发掷荚：沿出手向抛出，带一点上抛（出手荚旋相）</summary>
        protected override void OnSlashBegin() {
            if (podThrown) {
                return;
            }
            podThrown = true;
            if (IsFinisher) {
                SetFlash(7);
            }
            Vector2 dir = baseAngle.ToRotationVector2();
            int podDamage = Math.Max(1, (int)(Projectile.damage * 0.7f));
            SpawnOwnedProj(ModContent.ProjectileType<GsSeedlerPodProj>(),
                Hand + dir * (FullReach * 0.7f), dir * 9.5f + new Vector2(0f, -2.2f),
                podDamage, Projectile.knockBack * 0.4f, swingDir, IsFinisher ? 1f : 0f);
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.4f, Pitch = 0.1f }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.4f }, Owner.Center);
            }
        }

        /// <summary>命中反馈分流：钢质只溅木屑，血肉补汁液绿尘</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            Vector2 aimDir = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            //木屑：带重力弹开的心木碎屑
            int chips = IsFinisher ? 5 : 3;
            for (int i = 0; i < chips; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.WoodFurniture,
                    aimDir.RotatedByRandom(0.8) * Main.rand.NextFloat(2f, 5f), 40, default,
                    Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = false;
            }
            if (!steel) {
                //汁液：绿金光珠自伤口渗出上飘
                for (int i = 0; i < (IsFinisher ? 4 : 2); i++) {
                    PRTLoader.NewParticle<PRT_Light>(
                        target.Center + Main.rand.NextVector2Circular(10f, 10f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f),
                        Main.rand.NextBool() ? GsSeedler.SeedHot : GsSeedler.SeedMain,
                        Main.rand.NextFloat(0.07f, 0.12f))?.Configure(12, 0.7f);
                }
            }
        }
    }

    /// <summary>
    /// 种荚：每斩掷出的旋转木荚。重力弹跳（至多两弹），出手 6 帧荚旋成形，
    /// 飞行滴落叶迹，命中或落定炸成荆棘弹雨；ai[0]=旋向 ai[1]=藤蔓爆发旗
    /// （终结荚，爆点竖起荆棘丛）。荚体借原版种荚贴图，叶旋与叶迹自绘
    /// </summary>
    internal class GsSeedlerPodProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float SpinDir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private bool VineBurst => Projectile.ai[1] > 0.5f;
        private ref float Life => ref Projectile.localAI[0];
        private ref float Bounces => ref Projectile.localAI[1];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 110;
        }

        public override void AI() {
            Life++;
            //重力抛体：荚是颗有分量的木果
            Projectile.velocity.Y += 0.3f;
            if (Projectile.velocity.Y > 14f) {
                Projectile.velocity.Y = 14f;
            }
            //荚旋：滚转随水平速度
            Projectile.rotation += (0.16f + 0.02f * MathF.Abs(Projectile.velocity.X)) * SpinDir;

            Lighting.AddLight(Projectile.Center, GsSeedler.SeedMain.ToVector3() * 0.25f);

            //飞行叶迹：叶绿光珠断续滴落
            if (!VaultUtils.isServer && Life % 4f == 0f) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.06f - Vector2.UnitY * 0.3f,
                    Main.rand.NextBool(3) ? GsSeedler.SeedHot : GsSeedler.SeedMain,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(10, 0.6f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Bounces++;
            if (Bounces >= 2f) {
                return true;
            }
            //弹跳：竖向反弹衰减、横向拖阻
            if (Projectile.velocity.X != oldVelocity.X) {
                Projectile.velocity.X = -oldVelocity.X * 0.7f;
            }
            if (Projectile.velocity.Y != oldVelocity.Y) {
                Projectile.velocity.Y = -oldVelocity.Y * 0.62f;
            }
            Projectile.velocity.X *= 0.86f;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.4f, Pitch = 0.35f }, Projectile.Center);
            }
            return false;
        }

        /// <summary>爆荚：荆棘弹雨（owner 端生成），终结荚追加荆棘丛与藤蔓爆发演出</summary>
        public override void OnKill(int timeLeft) {
            if (Projectile.owner == Main.myPlayer) {
                int thornDamage = Math.Max(1, (int)(Projectile.damage * 0.43f)); //0.7x 荚 × 0.43 ≈ 0.3x 物品伤
                for (int i = 0; i < 5; i++) {
                    //扇形非匀速：以上方为轴散开，速度参差
                    float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-1.15f, 1.15f, i / 4f)
                        + Main.rand.NextFloat(-0.18f, 0.18f);
                    float speed = Main.rand.NextFloat(4.5f, 9.5f);
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center,
                        ang.ToRotationVector2() * speed,
                        ModContent.ProjectileType<GsSeedlerThornProj>(),
                        thornDamage, 0.5f, Projectile.owner);
                }
                if (VineBurst) {
                    int brambleDamage = Math.Max(1, (int)(Projectile.damage * 0.29f)); //≈0.2x 物品伤/跳
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Vector2.Zero,
                        ModContent.ProjectileType<GsSeedlerBrambleProj>(),
                        brambleDamage, 0f, Projectile.owner);
                }
            }

            if (VaultUtils.isServer) {
                return;
            }
            //命中爆荚相：木屑与汁液分流迸溅 + 绿光环闪
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.8f, Pitch = -0.1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.5f, Pitch = 0.1f }, Projectile.Center);
            for (int i = 0; i < 7; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.WoodFurniture,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 6f), 40, default,
                    Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = false;
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.8f, 2.2f),
                    Main.rand.NextBool() ? GsSeedler.SeedHot : GsSeedler.SeedMain,
                    Main.rand.NextFloat(0.08f, 0.14f))?.Configure(14, 0.75f);
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsSeedler.SeedBright, VineBurst ? 0.30f : 0.2f)?.Configure(10, 0.85f);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SeedlerNut);
            Texture2D pod = TextureAssets.Projectile[ProjectileID.SeedlerNut].Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            //出手荚旋相：6 帧带 12% 过冲撑开
            float grow = Life <= 6f ? 1.12f * (Life / 6f)
                : MathHelper.Lerp(1.12f, 1f, MathHelper.Clamp((Life - 6f) / 5f, 0f, 1f));
            float s = grow * (VineBurst ? 1.2f : 1f);

            //旧位置残荚：滚转的运动拖影
            for (int i = 1; i <= 2; i++) {
                Color trail = GsSeedler.SeedMain * (0.16f * (1f - i / 3f));
                trail.A = 0;
                Main.EntitySpriteDraw(pod, center - Projectile.velocity * (i * 1.8f), null, trail,
                    Projectile.rotation - SpinDir * 0.3f * i, pod.Size() * 0.5f, s, SpriteEffects.None, 0);
            }

            //荚体：原版种荚贴图受光绘制 + 木底暗影
            Color shadow = GsSeedler.SeedDeep * 0.5f;
            Main.EntitySpriteDraw(pod, center + new Vector2(1f, 2f), null, shadow,
                Projectile.rotation, pod.Size() * 0.5f, s * 1.02f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(pod, center, null, lightColor, Projectile.rotation,
                pod.Size() * 0.5f, s, SpriteEffects.None, 0);

            //叶旋：两片绿芒叶片绕荚快转（出手相最醒目）
            for (int i = 0; i < 2; i++) {
                float ang = Life * 0.42f * SpinDir + i * MathHelper.Pi;
                Vector2 at = center + ang.ToRotationVector2() * (13f * s);
                Color leaf = GsSeedler.SeedBright * 0.55f;
                leaf.A = 0;
                Main.EntitySpriteDraw(star, at, null, leaf, ang + MathHelper.PiOver2,
                    star.Size() * 0.5f, new Vector2(0.3f, 0.1f) * s, SpriteEffects.None, 0);
            }

            //终结荚：藤蔓爆发预兆的金绿核光
            if (VineBurst) {
                float pulse = 0.6f + 0.4f * MathF.Sin(Life * 0.4f);
                Color core = GsSeedler.SeedHot * (0.4f * pulse);
                core.A = 0;
                Main.EntitySpriteDraw(star, center, null, core, Life * 0.1f,
                    star.Size() * 0.5f, 0.3f * s, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 荆棘刺：爆荚散射的追击小刺。速度参差、8 帧后轻微追击附近敌人，
    /// 触砖即碎。刺体借原版荆棘贴图，追击相带绿芒指向
    /// </summary>
    internal class GsSeedlerThornProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
        }

        public override void AI() {
            Life++;
            //轻微追击：8 帧后向 320 内最近可追目标缓转（保留原版荆棘的追击身份）
            if (Life >= 8f) {
                NPC prey = null;
                float best = 320f * 320f;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy(Projectile)) {
                        continue;
                    }
                    float d = npc.DistanceSQ(Projectile.Center);
                    if (d < best) {
                        best = d;
                        prey = npc;
                    }
                }
                if (prey != null) {
                    float speed = Projectile.velocity.Length();
                    Vector2 want = (prey.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * speed;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, 0.07f);
                }
                else {
                    //无目标时坠回抛体
                    Projectile.velocity.Y += 0.12f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, GsSeedler.SeedMain.ToVector3() * 0.12f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GrassBlades,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.5f), 80, default,
                    Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.SeedlerThorn);
            Texture2D thorn = TextureAssets.Projectile[ProjectileID.SeedlerThorn].Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);

            //追击向绿芒：先画在刺身下层
            Color streak = GsSeedler.SeedMain * (0.45f * fade);
            streak.A = 0;
            Main.EntitySpriteDraw(star, center - Projectile.velocity.SafeNormalize(Vector2.Zero) * 6f,
                null, streak, Projectile.rotation - MathHelper.PiOver2, star.Size() * 0.5f,
                new Vector2(0.4f, 0.1f), SpriteEffects.None, 0);

            Main.EntitySpriteDraw(thorn, center, null, lightColor * fade, Projectile.rotation,
                thorn.Size() * 0.5f, 1f, SpriteEffects.None, 0);

            Color tip = GsSeedler.SeedBright * (0.5f * fade);
            tip.A = 0;
            Main.EntitySpriteDraw(star, center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 5f,
                null, tip, Projectile.rotation - MathHelper.PiOver2, star.Size() * 0.5f,
                new Vector2(0.2f, 0.08f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 荆棘丛：藤蔓爆发在落点竖起的驻留刺丛。160 帧寿命，18 帧一跳持续刺击；
    /// 破土 8 帧过冲成形，木棕根座 + 弯曲棘条摇曳，余痕相各棘参差蚀散。
    /// 绘制全走确定性相位
    /// </summary>
    internal class GsSeedlerBrambleProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 160;
        private const float Radius = 84f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 18;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                //藤蔓爆发：破土闷响 + 木屑汁液齐飞
                SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.9f, Pitch = -0.35f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
                for (int i = 0; i < 8; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.5f, 10f),
                        DustID.WoodFurniture, new Vector2(Main.rand.NextFloat(-1.5f, 1.5f), -Main.rand.NextFloat(1.5f, 4f)),
                        40, default, Main.rand.NextFloat(0.9f, 1.4f));
                    d.noGravity = false;
                }
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + Main.rand.NextVector2Circular(Radius * 0.4f, 8f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2f),
                        GsSeedler.SeedMain, Main.rand.NextFloat(0.07f, 0.12f))?.Configure(14, 0.7f);
                }
            }

            Lighting.AddLight(Projectile.Center, GsSeedler.SeedMain.ToVector3() * (0.35f * (1f - Life01)));

            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                //棘间浮尘
                Vector2 at = Projectile.Center + new Vector2(Main.rand.NextFloat(-Radius, Radius) * 0.8f, Main.rand.NextFloat(-14f, 2f));
                PRTLoader.NewParticle<PRT_Light>(at, -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f),
                    Main.rand.NextBool(3) ? GsSeedler.SeedHot : GsSeedler.SeedMain,
                    Main.rand.NextFloat(0.04f, 0.08f))?.Configure(10, 0.55f);
            }
        }

        public override bool? CanDamage() => Life >= 5f && Projectile.timeLeft > 12 ? null : false;

        /// <summary>横扁棘丛判定</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 delta = targetHitbox.Center.ToVector2() - Projectile.Center;
            delta.Y *= 1.5f;
            return delta.Length() <= Radius;
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || star == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float life = Life01;
            //破土 8 帧带 12% 过冲
            float grow = Life <= 8f ? 1.12f * (Life / 8f)
                : MathHelper.Lerp(1.12f, 1f, MathHelper.Clamp((Life - 8f) / 6f, 0f, 1f));

            //木棕根座：真 alpha 暗斑压底
            for (int i = 0; i < 3; i++) {
                float dieAt = 0.7f + 0.3f * SegRand(i);
                float segFade = MathHelper.Clamp((dieAt - life) / 0.25f, 0f, 1f);
                if (segFade <= 0.01f) {
                    continue;
                }
                Vector2 at = center + new Vector2((SegRand(i + 5) - 0.5f) * Radius * 1.3f, 4f);
                Color wood = Color.Lerp(GsSeedler.SeedDeep, GsSeedler.SeedWood, 0.4f) * (0.55f * segFade);
                Main.EntitySpriteDraw(blot, at, null, wood, SegRand(i + 8) * 0.6f - 0.3f,
                    blot.Size() * 0.5f, new Vector2(0.3f, 0.13f) * grow, SpriteEffects.None, 0);
            }

            //棘条：八根弯曲的刺自地面竖起，双段拼出弧曲，余痕相参差蚀散
            for (int i = 0; i < 8; i++) {
                float dieAt = 0.5f + 0.5f * SegRand(i + 20);
                float segFade = MathHelper.Clamp((dieAt - life) / 0.3f, 0f, 1f);
                if (segFade <= 0.01f) {
                    continue;
                }
                float x = (i / 7f - 0.5f) * Radius * 1.55f + (SegRand(i + 30) - 0.5f) * 14f;
                float height = (24f + 20f * SegRand(i + 40)) * grow;
                float lean = (SegRand(i + 50) - 0.5f) * 0.7f;
                float sway = MathF.Sin(Main.GlobalTimeWrappedHourly * (1.4f + SegRand(i + 60)) + SegRand(i + 70) * 6.28f) * 0.16f;
                Vector2 baseAt = center + new Vector2(x, 5f);
                //下段直立、上段外弯：两截绿芒拼一根弯棘
                float angLow = -MathHelper.PiOver2 + lean * 0.4f + sway * 0.5f;
                float angHigh = -MathHelper.PiOver2 + lean + sway;
                Color body = GsSeedler.SeedMain * (0.62f * segFade);
                body.A = 0;
                Vector2 mid = baseAt + angLow.ToRotationVector2() * (height * 0.5f);
                Main.EntitySpriteDraw(star, mid, null, body, angLow, star.Size() * 0.5f,
                    new Vector2(height / star.Width * 2f, 0.11f), SpriteEffects.None, 0);
                Vector2 top = baseAt + angLow.ToRotationVector2() * height * 0.9f;
                Main.EntitySpriteDraw(star, top + angHigh.ToRotationVector2() * (height * 0.28f), null,
                    body, angHigh, star.Size() * 0.5f,
                    new Vector2(height / star.Width * 1.2f, 0.08f), SpriteEffects.None, 0);
                //刺尖：跳伤节奏的金绿明灭
                float pulse = 0.5f + 0.5f * MathF.Sin(Life * 0.35f + SegRand(i + 80) * 6.28f);
                Color tip = GsSeedler.SeedHot * (0.45f * segFade * pulse);
                tip.A = 0;
                Main.EntitySpriteDraw(glow, top + angHigh.ToRotationVector2() * (height * 0.5f), null,
                    tip, 0f, glow.Size() * 0.5f, 0.13f, SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
