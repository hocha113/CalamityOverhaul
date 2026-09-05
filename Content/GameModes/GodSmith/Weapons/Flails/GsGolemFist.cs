using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 【连枷·石巨人之拳 ★A】玄武岩熔核重拳：玄武岩拳壳包着熔岩核，越打越烫。<br/>
    /// 签名行为：①热量双相：实打命中攒热 0~3 层（每层 +8% 伤害，8 秒不打冷却），
    /// 满层进炽熔态：命中崩 3 枚熔石 ②岩震：每记砸中敌人或撞砖都轰出
    /// 扩张岩震环（35% 小 AOE）+ 距离衰减屏震 ③直拳弹道：Brace 蓄压后平射不掉弧
    /// </summary>
    internal class GsGolemFist : GsFlailScheme
    {
        public override int TargetItemID => ItemID.GolemFist;

        protected override int FlailProjType => ModContent.ProjectileType<GsGolemFistHead>();

        protected override string GsDescFallback =>
            "Reforged: solid hits stack heat (up to 3, +8% damage each, fades after 8 seconds); at full heat the fist runs molten and impacts hurl 3 magma chunks\nEvery punch that lands on a foe or a wall slams out a stone shockwave";
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

        /// <summary>出手时把当前热量写进 ai[2] 随生成包过线，各端热层一致</summary>
        protected override float LaunchAi2(Player player, int index) => heat;

        //热层至 +24%、岩震 35% 小 AOE、炽熔熔石 3×50%，机制收益大，底伤克制在 8%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.08f;
    }

    /// <summary>
    /// 石巨人之拳锤头。直拳弹道：SelfSpinHead 关、蓄压 Brace 姿态、
    /// PostStateAI 抵掉大半基类微重力；热层按 ai[2] 各端同源；
    /// 命中/撞砖轰岩震环，炽熔态命中追加 3 枚熔石
    /// </summary>
    internal class GsGolemFistHead : GsFlailHeadProj
    {
        public override int SourceItemID => ItemID.GolemFist;
        public override int VanillaProjID => ProjectileID.GolemFist;
        public override Asset<Texture2D> ChainTexture => TextureAssets.Chain22;

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
        }

        protected override void OnLaunch(float charge) {
            if (VaultUtils.isServer) {
                return;
            }
            //拳风音
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with {
                Volume = 0.9f,
                Pitch = -0.3f + charge * 0.25f
            }, Owner.Center);
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
    }

    /// <summary>
    /// 岩震环：命中点轰出的扩张冲击波，~14 帧从小到大、透明度衰减，35% 小 AOE。
    /// 首帧闷响+距离衰减屏震（各客户端按自己与落点的距离结算）；
    /// 用原版泡泡贴图按当前半径画一笔作范围提示
    /// </summary>
    internal class GsGolemFistQuakeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bubble;

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
        }

        /// <summary>扩张圆盘判定：环扫过即中，免疫窗覆盖全存续</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius + 8f;

        /// <summary>范围提示：原版泡泡贴图按当前半径缩放画一笔，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float scale = Radius * 2f / MathF.Max(tex.Width, 1);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor * (1f - LifeT),
                0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 熔石：炽熔态命中崩出的带重力岩块（50% 伤害）。原版陨石贴图默认绘制；
    /// 落地小熔爆后转熔斑余痕 ~64 帧（判定关闭，随寿命淡出）
    /// </summary>
    internal class GsGolemFistMagmaProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Meteor1;

        private const int ScorchFrames = 64;

        /// <summary>ai[0]=1 已落地转熔斑（各端按各自撞砖判定，位置已同步，结果一致）</summary>
        private bool Scorched => Projectile.ai[0] == 1f;

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
                //熔斑余痕：驻定，随寿命淡出
                Projectile.velocity = Vector2.Zero;
                Projectile.Opacity = Projectile.timeLeft / (float)ScorchFrames;
                return;
            }
            //抛物飞行：重力加速+按速自旋
            Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.32f, 16f);
            Projectile.rotation += Projectile.velocity.X * 0.05f + 0.06f;
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
        }

        /// <summary>熔斑相纯余痕，不当磨床</summary>
        public override bool? CanDamage() => Scorched ? false : null;
    }
}
