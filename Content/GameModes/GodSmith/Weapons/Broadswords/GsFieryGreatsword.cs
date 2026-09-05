using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【狱岩熔金】材质：狱岩包芯熔金浇口的巨剑，重量就是身份。签名：
    /// ①大剑语言（与太刀相反）：全程看得见的抡，全拍慢重、顿帧最重、体倾最大
    /// ②终结拍过顶熔劈：高举过顶砸落，落点炸出驻留 60 帧的熔坑火域，二跳点燃
    /// </summary>
    internal class GsFieryGreatsword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.FieryGreatsword;

        protected override int HeldProjID => ModContent.ProjectileType<GsFieryGreatswordHeld>();

        protected override string GsDescFallback =>
            "Reforged: a hellstone slab that never hides its heave; molten metal drips off the edge all swing long, and the third strike is an overhead slam that leaves a burning crater";
        internal static readonly Color MagmaBright = new(255, 178, 92); //熔金亮橙
        internal static readonly Color MagmaMain = new(236, 98, 34);    //熔浆橙红
        internal static readonly Color MagmaHot = new(255, 232, 150);   //白热熔芯

        //预算：原版 40 伤/40 帧 = 1.0 伤帧。周期 = 36+35+44 = 115f（useTime 40 地板则 ~124f），
        //直击 = 40×1.04×(0.8+0.85+1.3) ≈ 122.7，熔坑 3 跳×19% 期望 1.5 跳 ≈ +12 →
        //约 1.09~1.17 伤帧 ≈ 109%~117%，熔坑满跳理论上限 ~120%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.04f;

        /// <summary>过顶熔劈永远刃口朝下，交替符号只在前两拍生效</summary>
        protected override void ModifyLocalSwing(Item item, Player player, ref int beat, ref float swingSign) {
            if (beat == ComboBeats - 1) {
                swingSign = 1f;
            }
        }
    }

    /// <summary>
    /// 炎阳巨剑手持：三拍全慢重（举 11~13/滞 3~4/斩 6~7/收 15~20），BaseReach 140、
    /// 判定加宽、滞相重颤。终结拍几何整改为过顶下劈（顶点重颤蓄势、
    /// 加速蓄重曲线砸落、剑插落点冻结），落点生熔坑。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsFieryGreatswordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.FieryGreatsword;
        protected override Color EdgeBright => GsFieryGreatsword.MagmaBright;
        protected override Color BodyMain => GsFieryGreatsword.MagmaMain;
        protected override Color HotAccent => GsFieryGreatsword.MagmaHot;

        protected override float BaseReach => 140f;
        protected override float CollisionWidth => 56f;
        protected override float PointBlankRadius => 52f;
        //过顶劈的落地帧就是最后一帧，伤害窗开满
        protected override float DamageWindowEnd => 1f;

        /// <summary>过顶顶点角与砸落终角（OnStageInit 缓存）</summary>
        private float slamApex, slamEnd;
        private bool slamImpactDone;

        protected override void SetSwordDefaults() {
            //巨剑本体判定加大
            Projectile.width = Projectile.height = 66;
        }

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            0 => new GsBroadBeat {
                Raise = 12, Hold = 3, Slash = 6, Recover = 15,
                RaiseBack = 2.1f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.11f,
                DamageMult = 0.8f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.5f,
            },
            1 => new GsBroadBeat {
                Raise = 11, Hold = 3, Slash = 6, Recover = 15,
                RaiseBack = 2.3f, Follow = 1.15f, ReachScale = 1.06f, LeanAmp = 0.12f,
                DamageMult = 0.85f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = -0.56f,
            },
            //过顶熔劈：几何走 SlamTransform，RaiseBack/Follow 只作扫角参考
            _ => new GsBroadBeat {
                Raise = 13, Hold = 4, Slash = 7, Recover = 20,
                RaiseBack = 2.5f, Follow = 1.15f, ReachScale = 1.18f, LeanAmp = 0.15f,
                DamageMult = 1.3f, Hitstop = 3, LungeSpeed = 4.5f, SwingPitch = -0.65f,
            },
        };

        protected override void OnStageInit() {
            if (!IsFinisher) {
                return;
            }
            //顶点在头顶偏身后，终角在身前偏下；WrapAngle 保证从头顶越过身前砸下
            slamApex = -MathHelper.PiOver2 - (facingDir * 0.62f);
            float raw = new Vector2(facingDir, 1.3f).ToRotation();
            slamEnd = slamApex + MathHelper.WrapAngle(raw - slamApex);
            //过顶劈的扫向恒等于面向，覆掉交替符号，翻刃方向随之对齐
            swingDir = facingDir;
        }

        /// <summary>加速蓄重曲线：入手慢、越砸越快，小过冲后硬停</summary>
        protected override float SwingCurve(float p) {
            const float burstEnd = 0.72f;
            const float overshoot = 1.05f;
            if (p < burstEnd) {
                float t = p / burstEnd;
                return overshoot * t * t * (0.4f + (0.6f * t));
            }
            return MathHelper.Lerp(overshoot, 1f, SmoothStep01((p - burstEnd) / (1f - burstEnd)));
        }

        protected override void UpdateBladeTransform(int phase) {
            if (IsFinisher) {
                SlamTransform(phase);
                mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
                return;
            }
            base.UpdateBladeTransform(phase);
            //滞相重颤：巨剑压手
            if (phase == PhaseHold) {
                mainAngle += MathF.Sin(timer * 1.3f) * 0.014f;
                mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
            }
        }

        /// <summary>过顶熔劈几何：举顶-顶点重颤-砸落-剑插落点</summary>
        private void SlamTransform(int phase) {
            switch (phase) {
                case PhaseRaise: {
                    float p = timer / (float)raiseDur;
                    float eased = SmoothStep01(p);
                    float liftFrom = new Vector2(facingDir, 0.35f).ToRotation();
                    mainAngle = liftFrom + (MathHelper.WrapAngle(slamApex - liftFrom) * eased);
                    mainReach = FullReach * MathHelper.Lerp(0.7f, 1f, eased);
                    slashProgress = 0f;
                    break;
                }
                case PhaseHold: {
                    //顶点重颤：巨剑在头顶打抖，蓄势可见
                    mainAngle = slamApex + (MathF.Sin(timer * 1.5f) * 0.02f);
                    mainReach = FullReach;
                    slashProgress = 0f;
                    break;
                }
                case PhaseSlash: {
                    float p = (timer - raiseDur - holdDur) / (float)slashDur;
                    slashProgress = p;
                    mainAngle = MathHelper.Lerp(slamApex, slamEnd, SwingCurve(p));
                    mainReach = FullReach * (1f + (0.06f * MathF.Sin(MathHelper.Clamp(p * 1.6f, 0f, 1f) * MathHelper.Pi)));
                    break;
                }
                default: {
                    float q = (timer - raiseDur - holdDur - slashDur) / (float)recoverDur;
                    slashProgress = 1f;
                    if (q < 0.35f) {
                        //剑插在落点：重量的余韵
                        mainAngle = slamEnd;
                        mainReach = FullReach;
                    }
                    else {
                        //缓缓把剑拔回持位
                        float s = (q - 0.35f) / 0.65f;
                        float guard = new Vector2(facingDir, 0.55f).ToRotation();
                        mainAngle = slamEnd + (MathHelper.WrapAngle(guard - slamEnd) * (EaseOutQuad(s) * 0.6f));
                        mainReach = FullReach * MathHelper.Lerp(1f, 0.8f, s * s);
                    }
                    break;
                }
            }
        }

        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            //砸地瞬间：熔坑 + 震屏 + 爆音
            if (IsFinisher && !slamImpactDone && phase == PhaseRecover) {
                slamImpactDone = true;
                DoSlamImpact();
            }
        }

        private void DoSlamImpact() {
            //从刃尖向下探地，找不到实心块就贴刃尖生成
            Vector2 at = mainTip;
            int tx = (int)(at.X / 16f);
            int ty = (int)(at.Y / 16f);
            for (int i = 0; i < 14; i++) {
                if (!WorldGen.InWorld(tx, ty + i, 10)) {
                    break;
                }
                if (WorldGen.SolidTile(tx, ty + i)) {
                    at.Y = ((ty + i) * 16f) - 10f;
                    break;
                }
            }
            //熔坑一跳约 19% 当前伤（终结拍已含 1.3 倍，折合底伤约 25%）
            SpawnOwnedProj(ModContent.ProjectileType<GsFieryGreatswordPitProj>(), at, Vector2.Zero,
                Math.Max(1, (int)(Projectile.damage * 0.19f)), 2f);

            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.7f, Pitch = -0.25f }, at);
            if (CWRClientConfig.Instance.ScreenVibration) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(at,
                    new Vector2(0f, 1f), 5f, 7f, 12, 1000f, "GsFierySlam"));
            }
        }

        protected override void PlaySwingSound() {
            //音高全族最低，每拍都带厚重风声
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 1f, Pitch = Beat.SwingPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.7f }, Owner.Center);
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, IsFinisher ? 300 : 180);
        }
    }

    /// <summary>
    /// 熔坑火域：过顶熔劈落点的驻留判定。60 帧寿命，20 帧一跳（约 19% 当前伤）并点燃。
    /// 用原版燃烧瓶地火贴图按判定框拉伸画一笔作范围提示
    /// </summary>
    internal class GsFieryGreatswordPitProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MolotovFire;

        private const int Life = 60;

        private float Life01 => 1f - (Projectile.timeLeft / (float)Life);

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.MolotovFire];
        }

        public override void SetDefaults() {
            Projectile.width = 110;
            Projectile.height = 36;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = Life;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => Projectile.timeLeft > 6 ? null : false;

        public override void AI() {
            //多帧地火贴图的最简帧计数
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Math.Max(1, Main.projFrames[Type]);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
            => target.AddBuff(BuffID.OnFire, 240);

        /// <summary>范围提示：原版地火贴图按判定框拉伸画一笔，首 4 帧撑开、随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Rectangle frame = tex.Frame(1, Math.Max(1, Main.projFrames[Type]), 0, Projectile.frame);
            float fadeIn = MathHelper.Clamp((Life - Projectile.timeLeft) / 4f, 0f, 1f);
            Vector2 stretch = new(Projectile.width / MathF.Max(frame.Width, 1), Projectile.height / MathF.Max(frame.Height, 1));
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, frame, lightColor * (fadeIn * (1f - Life01)),
                0f, frame.Size() * 0.5f, stretch, SpriteEffects.None, 0);
            return false;
        }
    }
}
