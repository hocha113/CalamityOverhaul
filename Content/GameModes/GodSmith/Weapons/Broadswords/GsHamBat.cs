using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【饱食之力·火腿棒】材质：老饕圣物级的蜜汁大火腿，油脂就是它的刃。
    /// 签名：①按饱食档位增伤 +5%/+10%/+15%（原版特性原样保留），吃得越饱挥砍越冒
    /// 肉香粒子与油星 ②终结拍命中溅起肉屑并炸开「香气冲击」小范围波（食物系音效）
    /// ③满档时挥舞自带滋滋作响的油脂质感，滑稽感就是它的身份
    /// </summary>
    internal class GsHamBat : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.HamBat;

        protected override int HeldProjID => ModContent.ProjectileType<GsHamBatHeld>();

        protected override string GsDescFallback =>
            "Reforged: the power of satiety; swings grow greasier and mightier with your Well Fed tier " +
            "(+5%/+10%/+15% damage), and the finishing beat splatters meat scraps with a burst of savory aroma";

        //蜜汁火腿色板
        internal static readonly Color HamGlaze = new(255, 226, 196);  //蜜汁高光
        internal static readonly Color HamPink = new(232, 128, 110);   //火腿粉体色
        internal static readonly Color RoastAmber = new(255, 176, 96); //炙烤琥珀
        internal static readonly Color HamDeep = new(52, 22, 18);      //焦皮暗棕

        //底伤不加成（原版 57/20f、scale1.2 的大肉腿）：三拍 1.0/1.0/1.25x 按 66 帧循环摊算约原版 110%；
        //饱食增伤与原版逐字等效（WellFed 档位 +5%/+10%/+15%，走命中乘区，双方同吃同涨）；
        //终结拍香气冲击 0.3x 小 AoE 摊进循环约 +3%，肉香油星纯滑稽不进预算
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 饱食之力手持：三拍肉感重击。0 抡腿 / 1 回抡 / 2 满膛全力挥（前压+香气冲击）。
    /// 饱食档位越高油星肉香越多、增伤越足。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsHamBatHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.HamBat;
        protected override Color EdgeBright => GsHamBat.HamGlaze;
        protected override Color BodyMain => GsHamBat.HamPink;
        protected override Color HotAccent => GsHamBat.RoastAmber;
        protected override Color DeepShadow => GsHamBat.HamDeep;

        //大肉腿触距（原版 scale 1.2）
        protected override float BaseReach => 126f;
        protected override float CollisionWidth => 44f;

        protected override bool GlowAlways => IsFinisher;
        protected override Color GlowColor => GsHamBat.RoastAmber;

        /// <summary>一拍只炸一次香气</summary>
        private bool aromaFired;

        /// <summary>饱食档位 0~3（buff 各端同步，读取无需守门）</summary>
        private int SatietyTier
            => Owner.HasBuff(BuffID.WellFed3) ? 3
             : Owner.HasBuff(BuffID.WellFed2) ? 2
             : Owner.HasBuff(BuffID.WellFed) ? 1 : 0;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 抡腿
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.05f,
                DamageMult = 1.0f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.15f,
            },
            //拍1 回抡
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.95f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.055f,
                DamageMult = 1.0f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.08f,
            },
            //拍2 满膛全力挥：前压
            _ => new GsBroadBeat {
                Raise = 8, Hold = 3, Slash = 5, Recover = 11,
                RaiseBack = 2.3f, Follow = 1.25f, ReachScale = 1.15f, LeanAmp = 0.09f,
                DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.35f,
            },
        };

        //==================== 老饕演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.9f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //全力挥的厚风声
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.35f, Pitch = -0.4f }, Owner.Center);
            }
        }

        /// <summary>饱食增伤：与原版逐字等效（+5%/+10%/+15% 按 WellFed 档位）</summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            int tier = SatietyTier;
            if (tier > 0) {
                modifiers.SourceDamage *= 1f + 0.05f * tier;
            }
        }

        /// <summary>终结拍命中：香气冲击 + 咀嚼重音（一拍一次）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!IsFinisher || aromaFired) {
                return;
            }
            aromaFired = true;
            SetFlash(5);
            SpawnOwnedProj(ModContent.ProjectileType<GsHamBatAromaProj>(),
                target.Center, Vector2.Zero, Math.Max(1, (int)(Projectile.damage * 0.3f)),
                Projectile.knockBack * 1.1f);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.85f, Pitch = -0.15f }, target.Center);
            }
        }

        /// <summary>肉香与油星随饱食档位加码：油星贴挥弧甩、肉香粒子袅袅上飘</summary>
        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            int tier = SatietyTier;
            if (phase != PhaseSlash) {
                //持棒待机：满档时偶尔冒一缕肉香
                if (tier >= 2 && Main.rand.NextBool(16)) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 0.95f)),
                        -Vector2.UnitY * Main.rand.NextFloat(0.4f, 0.9f),
                        GsHamBat.HamGlaze, Main.rand.NextFloat(0.05f, 0.08f))?.Configure(12, 0.55f);
                }
                return;
            }
            //油星：琥珀火星贴切线甩出，档位越高越密
            Vector2 tangent = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
            for (int i = 0; i < tier; i++) {
                if (Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Spark>(
                        Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1f)),
                        tangent * Main.rand.NextFloat(2.5f, 5.5f),
                        GsHamBat.RoastAmber, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
                }
            }
            //肉香：奶粉色香雾自挥弧上飘
            if (tier > 0 && Main.rand.NextBool(4 - tier)) {
                PRTLoader.NewParticle<PRT_Light>(
                    Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.5f, 1f)),
                    -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f),
                    Main.rand.NextBool() ? GsHamBat.HamGlaze : GsHamBat.HamPink,
                    Main.rand.NextFloat(0.06f, 0.1f))?.Configure(11, 0.6f);
            }
        }

        /// <summary>肉屑命中反馈：粉肉粒弹起 + 油点四溅，终结拍加量</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            int scraps = IsFinisher ? 7 : 3;
            for (int i = 0; i < scraps; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-4.5f, -1.5f)),
                    Main.rand.NextBool() ? GsHamBat.HamPink : GsHamBat.RoastAmber,
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(12, 20));
            }
            if (IsFinisher) {
                //啃一大口的脆响
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.5f, Pitch = 0.2f }, target.Center);
            }
        }
    }

    /// <summary>
    /// 香气冲击：终结拍命中处炸开的小范围肉香波。半径 8 帧过冲撑满后回坐，
    /// 伤害只在扩张期结算一次、击退向外；香雾环+气旋涡+袅袅蒸汽，滑稽而认真。
    /// 绘制全走确定性相位，禁 Main.rand
    /// </summary>
    internal class GsHamBatAromaProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int TotalLife = 24;
        private const float MaxRadius = 92f;
        private ref float Life => ref Projectile.localAI[0];
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>当前扩张半径：8 帧过冲 6% 再回坐</summary>
        private float Radius {
            get {
                float p = MathHelper.Clamp(Life / 8f, 0f, 1f);
                float burst = p < 0.7f ? 1.06f * (p / 0.7f) : MathHelper.Lerp(1.06f, 1f, (p - 0.7f) / 0.3f);
                return MaxRadius * burst;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item2 with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
                //肉屑与油点腾起
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                        Main.rand.NextBool() ? GsHamBat.HamPink : GsHamBat.RoastAmber,
                        Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(12, 20));
                }
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Light>(
                        Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.6f),
                        GsHamBat.HamGlaze, Main.rand.NextFloat(0.08f, 0.13f))?.Configure(14, 0.7f);
                }
            }
            Lighting.AddLight(Projectile.Center, GsHamBat.RoastAmber.ToVector3() * (0.5f * (1f - Life01)));
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 9f ? null : false;

        /// <summary>圆形判定：香气笼罩范围内即命中</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//香气把人熏开

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D air = CWRAsset.Airflow?.Value;
            if (glow == null || air == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = 1f - Life01;
            float radius = Radius;

            //香气涡：气旋贴图缓旋，像一锅刚揭盖的高汤
            Color swirl = GsHamBat.HamGlaze * (0.3f * fade);
            swirl.A = 0;
            Main.EntitySpriteDraw(air, center, null, swirl, Life * 0.09f + SegRand(3) * 6.28f,
                air.Size() * 0.5f, radius * 2f / air.Width, SpriteEffects.None, 0);

            //香雾环：奶粉色光珠沿当前半径排布
            int beads = 10;
            for (int i = 0; i < beads; i++) {
                float ang = (MathHelper.TwoPi * i / beads) + SegRand(i) * 0.5f;
                Vector2 at = center + (ang.ToRotationVector2() * radius);
                float pulse = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7f + SegRand(i + 30) * 6.28f);
                Color bead = Color.Lerp(GsHamBat.HamGlaze, GsHamBat.HamPink, SegRand(i + 60)) * (0.45f * fade * pulse);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, at, null, bead, 0f, glow.Size() * 0.5f,
                    0.22f + 0.07f * SegRand(i + 90), SpriteEffects.None, 0);
            }

            //袅袅蒸汽：三缕香雾摆着腰上飘
            for (int i = 0; i < 3; i++) {
                float t = (Life * 0.045f + SegRand(i + 120) * 0.9f) % 1f;
                float sway = MathF.Sin((t * 7f) + (i * 2.1f)) * 10f;
                Vector2 at = center + new Vector2(((i - 1) * 20f) + sway, -12f - (t * 52f));
                Color steam = GsHamBat.HamGlaze * (0.34f * fade * (1f - t));
                steam.A = 0;
                Main.EntitySpriteDraw(glow, at, null, steam, 0f, glow.Size() * 0.5f,
                    0.16f + 0.1f * t, SpriteEffects.None, 0);
            }

            //爆心暖光
            Color heart = GsHamBat.RoastAmber * (0.4f * fade);
            heart.A = 0;
            Main.EntitySpriteDraw(glow, center, null, heart, 0f, glow.Size() * 0.5f, 0.4f, SpriteEffects.None, 0);
            return false;
        }
    }
}
