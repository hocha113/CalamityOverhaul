using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【重压破甲】材质：绯红精金重锻的破城重剑。
    /// 签名：①拍表整体偏重（长举、全拍顿帧、音高全族最低）
    /// ②终结拍整替几何为过顶压顶劈，命中打上破甲（BrokenArmor 4 秒）
    /// ③落点掀起贴地横扫的精金震波，对波及目标同样破甲
    /// </summary>
    internal class GsAdamantiteSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.AdamantiteSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsAdamantiteSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: slow, crushing adamantite cleaves; the finisher drops as an overhead slam that breaks armor and quakes the ground";
        internal static readonly Color AdaBright = new(255, 148, 148); //绯亮刃缘
        internal static readonly Color AdaMain = new(214, 58, 74);     //绯红重钢
        internal static readonly Color AdaHot = new(255, 96, 60);      //灼绯强调

        //预算账：拍均 (1.05+1.05+1.5)/3≈1.20 ×底伤 1.06；落点震波 0.5x 每循环一发
        //（压顶目标同吃 ~0.75 → +0.125/拍）→ 名义 (1.20+0.125)×1.06≈1.40/拍；
        //连段总帧 (25+25+31)=81 对原版 63 (+29%) → 综合单体 DPS ≈ 1.40×0.78 ≈ 原版 109%；
        //破甲（BrokenArmor 4 秒）对高甲目标另有 ~+5% 隐性收益，仍在 120% 包络内；击退 +35% 不进 DPS
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.06f;

        public override void GsModifyWeaponKnockback(Item item, Player player, ref StatModifier knockback)
            => knockback *= 1.35f;
    }

    /// <summary>
    /// 重压破甲手持：三拍重剑，两记沉横劈接「压顶劈」——终结拍整替几何为过顶下砸
    /// （比铅坠劈起手更靠后、行程更紧），玩家本体不位移；
    /// 落刃瞬间在刃尖放精金震波。ai[0]=拍号 ai[1]=交替符号（终结拍恒过顶）
    /// </summary>
    internal class GsAdamantiteSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.AdamantiteSword;
        protected override Color EdgeBright => GsAdamantiteSword.AdaBright;
        protected override Color BodyMain => GsAdamantiteSword.AdaMain;
        protected override Color HotAccent => GsAdamantiteSword.AdaHot;

        //破城重剑判定更厚
        protected override float CollisionWidth => 46f;

        private bool quakeFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 沉横劈
            0 => new GsBroadBeat {
                Raise = 8, Hold = 2, Slash = 5, Recover = 10,
                RaiseBack = 2.15f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.075f,
                DamageMult = 1.05f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.5f,
            },
            //拍1 返沉劈
            1 => new GsBroadBeat {
                Raise = 8, Hold = 2, Slash = 5, Recover = 10,
                RaiseBack = 2.2f, Follow = 1.1f, ReachScale = 1f, LeanAmp = 0.08f,
                DamageMult = 1.05f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.56f,
            },
            //拍2 压顶劈：全族最重的一击，顿帧 4
            _ => new GsBroadBeat {
                Raise = 11, Hold = 3, Slash = 5, Recover = 12,
                RaiseBack = 2.3f, Follow = 1.2f, ReachScale = 1.2f, LeanAmp = 0.11f,
                DamageMult = 1.5f, Hitstop = 4, LungeSpeed = 2.0f, SwingPitch = -0.7f,
            },
        };

        protected override void OnStageInit() {
            if (IsFinisher) {
                //压顶劈恒沿面朝向翻落，压掉交替符号，翻刃方向随之对齐
                swingDir = facingDir;
            }
        }

        /// <summary>压顶劈起止角：自脑后高位翻过天顶砸向脚前（比铅坠劈更紧的行程）</summary>
        private float SlamStart => -MathHelper.PiOver2 - (facingDir * 0.9f);
        private float SlamEnd => SlamStart + (facingDir * 2.7f);

        /// <summary>终结拍整替几何为过顶下砸，普通拍走基类横劈</summary>
        protected override void UpdateBladeTransform(int phase) {
            if (!IsFinisher) {
                base.UpdateBladeTransform(phase);
                return;
            }
            float slamStart = SlamStart;
            switch (phase) {
                case PhaseRaise: {
                    //自身前低位拖上脑后，末段越拖越慢（精金的分量）
                    float p = timer / (float)raiseDur;
                    float eased = 1f - MathF.Pow(1f - p, 2.4f);
                    float liftFrom = slamStart + (facingDir * 1.35f);
                    mainAngle = MathHelper.Lerp(liftFrom, slamStart, eased);
                    mainReach = FullReach * MathHelper.Lerp(0.52f, 0.9f, eased);
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    float p = (timer - raiseDur) / (float)holdDur;
                    mainAngle = slamStart - (facingDir * 0.07f * EaseOutQuad(p));
                    mainReach = FullReach * MathHelper.Lerp(0.9f, 0.96f, EaseOutQuad(p));
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float p = (timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    mainAngle = MathHelper.Lerp(slamStart, SlamEnd, SwingCurve(p));
                    mainReach = FullReach * (0.96f + 0.04f * MathF.Sin(MathHelper.Clamp(p * 1.8f, 0f, 1f) * MathHelper.Pi));
                    break;
                }
                default: {
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    float settle = EaseOutQuad(Math.Min(1f, q * 2.2f));
                    mainAngle = SlamEnd + (facingDir * 0.09f * (1f - settle));
                    mainReach = FullReach * MathHelper.Lerp(0.96f, 0.8f, q * q);
                    slashProgress = 1f;
                    break;
                }
            }
            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            if (!IsFinisher) {
                return;
            }
            //落刃：收势首帧在刃尖放精金震波（震波自寻地面，找不到就空爆）
            if (phase == PhaseRecover && !quakeFired) {
                quakeFired = true;
                int quakeDamage = Math.Max(1, (int)(Projectile.damage * 0.5f));
                SpawnOwnedProj(ModContent.ProjectileType<GsAdamantiteSwordQuakeProj>(),
                    mainTip, Vector2.Zero, quakeDamage, Projectile.knockBack * 0.8f);
            }
        }

        /// <summary>压顶命中：打上破甲（AddBuff 在攻击方端调用，原版自带同步）+ 破甲脆响</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!IsFinisher) {
                return;
            }
            target.AddBuff(BuffID.BrokenArmor, 240);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.5f, Pitch = -0.3f, MaxInstances = 3 }, target.Center);
            }
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                //压顶：厚重双响
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.55f }, Owner.Center);
            }
        }
    }

    /// <summary>
    /// 精金震波：压顶落刃处向下寻地（10 格内），贴地后向两侧横扫冲击前锋
    /// （12 帧过冲扩到半宽 130），波及目标 0.5x 伤害并破甲；无地则原地小半径空爆。
    /// 用原版食人魔震地贴图按判定范围拉伸画一笔作范围提示
    /// </summary>
    internal class GsAdamantiteSwordQuakeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DD2OgreSmash;
        public override LocalizedText DisplayName => Language.GetText("ItemName.AdamantiteSword");

        private const int TotalLife = 26;
        private const float MaxHalfWidth = 130f;

        private ref float Life => ref Projectile.localAI[0];
        /// <summary>1=已贴地横扫 0=空爆</summary>
        private ref float GroundedFlag => ref Projectile.localAI[1];
        private bool Grounded => GroundedFlag > 0.5f;
        private float Life01 => MathHelper.Clamp(Life / TotalLife, 0f, 1f);

        /// <summary>冲击前锋当前半宽：12 帧过冲 6% 再回坐</summary>
        private float HalfWidth {
            get {
                float p = MathHelper.Clamp(Life / 12f, 0f, 1f);
                float burst = p < 0.7f ? 1.06f * (p / 0.7f) : MathHelper.Lerp(1.06f, 1f, (p - 0.7f) / 0.3f);
                return MaxHalfWidth * burst;
            }
        }

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.DD2OgreSmash];
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
            if (Life == 1f) {
                //落点寻地：向下 10 格找实心块，贴地横扫；找不到就空爆（各端图格一致，结果一致）
                Point tile = Projectile.Center.ToTileCoordinates();
                for (int j = 0; j < 10; j++) {
                    if (!WorldGen.SolidTile(tile.X, tile.Y + j)) {
                        continue;
                    }
                    Projectile.Center = new Vector2(Projectile.Center.X, ((tile.Y + j) * 16f) - 6f);
                    GroundedFlag = 1f;
                    break;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 0.9f, Pitch = -0.2f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.45f }, Projectile.Center);
                }
            }
        }

        //伤害只在扩张期结算（一目标一次）
        public override bool? CanDamage() => Life <= 12f ? null : false;

        /// <summary>贴地：横扫矩形（半宽扩张、高 60）；空爆：半径 70 圆判</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!Grounded) {
                return targetHitbox.Distance(Projectile.Center) <= 70f;
            }
            float halfW = HalfWidth;
            Rectangle sweep = new((int)(Projectile.Center.X - halfW), (int)(Projectile.Center.Y - 54f),
                (int)(halfW * 2f), 60);
            return sweep.Intersects(targetHitbox);
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);//击退向外

        /// <summary>震波波及同样破甲</summary>
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.BrokenArmor, 240);

        /// <summary>范围提示：原版震地贴图按判定范围拉伸画一笔（贴地拉成横扫矩形，空爆按半径缩放），随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Math.Max(1, Main.projFrames[Type]), 0, 0);
            Vector2 origin = frame.Size() * 0.5f;
            Color color = lightColor * (1f - Life01);
            if (!Grounded) {
                float scale = 140f / MathF.Max(frame.Width, 1);
                Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, color,
                    0f, origin, scale, SpriteEffects.None, 0);
                return false;
            }
            Vector2 stretch = new(HalfWidth * 2f / MathF.Max(frame.Width, 1), 60f / MathF.Max(frame.Height, 1));
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition + new Vector2(0f, -24f), frame, color,
                0f, origin, stretch, SpriteEffects.None, 0);
            return false;
        }
    }
}
