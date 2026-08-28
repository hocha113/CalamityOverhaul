using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【蓝钢疾影】材质：轻锻钴蓝合金。
    /// 签名：①「动量」连段不断档则出刀渐快（每层拍表帧数 -3.5%，至多五层），断手清零
    /// ②第四拍拖出一道蓝弧残像，紧随其后沿同弧补上第二段判定
    /// ③动量越高残影越密、挥砍音越锐，速度线沿刃尾拉出
    /// </summary>
    internal class GsCobaltSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.CobaltSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsCobaltSwordHeld>();

        protected override int ComboBeats => 4;

        //动量剑的续段窗口稍宽，层数更容易保住
        protected override int ComboResetFrames => 60;

        protected override string GsDescFallback =>
            "Reforged: keep the combo unbroken and the cobalt edge swings ever faster; " +
            "the fourth strike trails a blue echo arc that cuts again";

        //钴蓝钢色板
        internal static readonly Color CobaltBright = new(168, 214, 255); //钴亮蓝
        internal static readonly Color CobaltMain = new(58, 112, 224);    //钴蓝钢
        internal static readonly Color CobaltHot = new(144, 240, 255);    //疾影电青
        internal static readonly Color CobaltDeep = new(16, 24, 52);      //深钢蓝影

        internal const int MaxMomentum = 5;

        /// <summary>动量层数 0~5；跨玩家共享单例，只在 myPlayer 守门路径读写。
        /// 层数经 ai[1] 模长随生成包过线（基类只消费符号），各端拍表帧数一致</summary>
        internal int Momentum;

        /// <summary>出手前把当前动量编码进交替符号模长，然后自增一层</summary>
        protected override void ModifyLocalSwing(Item item, Player player, ref int beat, ref float swingSign) {
            swingSign *= 1f + Momentum;
            Momentum = Math.Min(MaxMomentum, Momentum + 1);
        }

        /// <summary>断手回拍的同时清动量（base 保住连段衰减记账）</summary>
        public override void GsHoldItem(Item item, Player player) {
            base.GsHoldItem(item, player);
            if (player.whoAmI == Main.myPlayer && comboResetTimer == 0 && Momentum != 0) {
                Momentum = 0;
            }
        }

        //预算账：拍均 (0.95×3+1.18)/4≈1.01；终结疾影残弧 0.45x 同弧补刀（单体重取 ~0.8 → +0.09/拍）；
        //0 动量连段 (21+20+19+25)=85 帧对原版 4×19=76 (+12%，起手约 98%)，
        //满 5 层动量帧数 ×0.825 → 约 70 帧 → 上限约 119%；
        //综合 DPS ≈ 原版 98%~119%，动量爬坡即卖点，底伤不再加成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 蓝钢疾影手持：四拍连击（三快斩+疾影终结）。OnStageInit 按 ai[1] 模长解码动量层数，
    /// 四相帧数整体缩短；层数越高残影越密、音越锐。终结拍放蓝弧残像补刀。
    /// ai[0]=拍号 ai[1]=交替符号×(1+动量)
    /// </summary>
    internal class GsCobaltSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.CobaltSword;
        protected override int BeatCount => 4;
        protected override Color EdgeBright => GsCobaltSword.CobaltBright;
        protected override Color BodyMain => GsCobaltSword.CobaltMain;
        protected override Color HotAccent => GsCobaltSword.CobaltHot;
        protected override Color DeepShadow => GsCobaltSword.CobaltDeep;

        private int momentumStacks;
        private bool echoFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 起手横斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 1, Slash = 4, Recover = 10,
                RaiseBack = 1.7f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.02f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 10,
                RaiseBack = 1.75f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.06f,
            },
            //拍2 顺斩：音再上一阶
            2 => new GsBroadBeat {
                Raise = 5, Hold = 1, Slash = 4, Recover = 9,
                RaiseBack = 1.8f, Follow = 1.05f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.14f,
            },
            //拍3 疾影斩：前压重收，残弧随后补刀
            _ => new GsBroadBeat {
                Raise = 7, Hold = 2, Slash = 4, Recover = 12,
                RaiseBack = 2.05f, Follow = 1.25f, ReachScale = 1.08f, LeanAmp = 0.065f,
                DamageMult = 1.18f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.1f,
            },
        };

        /// <summary>解码动量层数并整体缩短四相帧数（各端由 ai[1] 同源解码，帧数一致）</summary>
        protected override void OnStageInit() {
            momentumStacks = Math.Clamp((int)MathF.Round(MathF.Abs(Projectile.ai[1])) - 1, 0, GsCobaltSword.MaxMomentum);
            if (momentumStacks <= 0) {
                return;
            }
            float k = 1f - 0.035f * momentumStacks;
            raiseDur = Math.Max(1, (int)MathF.Round(raiseDur * k));
            holdDur = Math.Max(1, (int)MathF.Round(holdDur * k));
            slashDur = Math.Max(2, (int)MathF.Round(slashDur * k));
            recoverDur = Math.Max(2, (int)MathF.Round(recoverDur * k));
            totalDur = raiseDur + holdDur + slashDur + recoverDur;
        }

        /// <summary>动量越高挥砍音越锐</summary>
        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with {
                Volume = 0.8f, Pitch = Beat.SwingPitch + 0.05f * momentumStacks
            }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.32f, Pitch = -0.2f }, Owner.Center);
            }
        }

        //动量越高残影越密
        protected override int GhostCount => Math.Min(4, 2 + momentumStacks / 2);

        /// <summary>终结拍：沿本次挥弧放疾影残弧，半拍不到便补第二刀</summary>
        protected override void OnSlashBegin() {
            if (!IsFinisher || echoFired) {
                return;
            }
            echoFired = true;
            SetFlash(6);
            float startAng = ArcStart - (swingDir * 0.08f);
            int echoDamage = Math.Max(1, (int)(Projectile.damage * 0.45f));
            SpawnOwnedProj(ModContent.ProjectileType<GsCobaltSwordEchoProj>(), Hand, Vector2.Zero,
                echoDamage, Projectile.knockBack * 0.4f, startAng, ArcEnd, FullReach);
        }

        /// <summary>动量速度线：斩切期沿刃尾拉出电青拉丝，层数越高越亮（确定性，无随机）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (momentumStacks <= 0 || CurrentPhase != PhaseSlash || slashProgress < 0.15f) {
                return;
            }
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak == null) {
                return;
            }
            float strength = momentumStacks / (float)GsCobaltSword.MaxMomentum;
            Vector2 hand = Hand;
            for (int i = 0; i < 3; i++) {
                float along = 0.55f + 0.18f * i + 0.05f * DrawRand01(i);
                float trailAng = mainAngle - (swingDir * (0.22f + 0.1f * i));
                Vector2 at = hand + (trailAng.ToRotationVector2() * mainReach * along) - Main.screenPosition;
                float len = (34f + 16f * strength) / streak.Size().X;
                Color c = GsCobaltSword.CobaltHot * (0.34f * strength * fanFade * (1f - i * 0.25f));
                c.A = 0;
                sb.Draw(streak, at, null, c, trailAng + (swingDir * MathHelper.PiOver2), streak.Size() / 2f,
                    new Vector2(len, 0.06f), SpriteEffects.None, 0f);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //疾影命中：电青短闪
            if (momentumStacks >= 3 || IsFinisher) {
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GsCobaltSword.CobaltHot, 0.2f)
                    ?.Configure(9, 0.8f);
            }
        }
    }

    /// <summary>
    /// 疾影残弧：终结斩后紧随的蓝弧残像，驻在出手点，滞 4 帧后用 5 帧沿同弧重演一次判定。
    /// 自绘月牙三层（深蓝垫底+钴蓝主体+电青刃缘）+ 尾随残弧渐淡；
    /// 滞留期在起手角画蓄势微光。ai[0]=弧起角 ai[1]=弧止角 ai[2]=触及半径。绘制无随机
    /// </summary>
    internal class GsCobaltSwordEchoProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.CobaltSword");

        private const int DelayFrames = 4;
        private const int SweepFrames = 5;
        private const int TotalFrames = 16;

        private float ArcFrom => Projectile.ai[0];
        private float ArcTo => Projectile.ai[1];
        private float Reach => Projectile.ai[2];
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>重演行程 0~1</summary>
        private float SweepP(float life) =>
            MathHelper.Clamp((life - DelayFrames) / SweepFrames, 0f, 1f);

        /// <summary>行程角：三次缓出，弧尾带一点收</summary>
        private float AngleAt(float p) {
            float eased = 1f - MathF.Pow(1f - p, 3f);
            return MathHelper.Lerp(ArcFrom, ArcTo, eased);
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalFrames;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (Life == DelayFrames + 1 && !VaultUtils.isServer) {
                //残像启动：一记更高更薄的挥音
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.45f, Pitch = 0.32f }, Projectile.Center);
            }
            float mid = MathHelper.Lerp(ArcFrom, ArcTo, 0.5f);
            Lighting.AddLight(Projectile.Center + mid.ToRotationVector2() * (Reach * 0.7f),
                GsCobaltSword.CobaltMain.ToVector3() * 0.3f);

            //重演期沿刃尾甩电青火星
            if (!VaultUtils.isServer && Life > DelayFrames && Life <= DelayFrames + SweepFrames) {
                float ang = AngleAt(SweepP(Life));
                Vector2 at = Projectile.Center + ang.ToRotationVector2() * (Reach * Main.rand.NextFloat(0.6f, 1f));
                PRTLoader.NewParticle<PRT_Spark>(at,
                    (ang + MathHelper.PiOver2 * MathF.Sign(ArcTo - ArcFrom)).ToRotationVector2() * Main.rand.NextFloat(2.5f, 5f),
                    GsCobaltSword.CobaltHot, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        //只在重演期结算伤害
        public override bool? CanDamage() =>
            Life > DelayFrames && Life <= DelayFrames + SweepFrames + 1 ? null : false;

        /// <summary>本帧扫过的角度区间逐段采样（半径 35%~102% 的线段）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float pPrev = SweepP(Life - 1f);
            float pCur = SweepP(Life);
            Vector2 center = Projectile.Center;
            float collisionPoint = 0f;
            const int steps = 4;
            for (int i = 0; i <= steps; i++) {
                float ang = AngleAt(MathHelper.Lerp(pPrev, pCur, i / (float)steps));
                Vector2 dir = ang.ToRotationVector2();
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(),
                    center + dir * (Reach * 0.35f), center + dir * (Reach * 1.02f), 30f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? GsCobaltSword.CobaltHot : GsCobaltSword.CobaltBright,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(9, 15));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (crescent == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 6f, 0f, 1f);
            float sweepSign = MathF.Sign(ArcTo - ArcFrom);
            Vector2 origin = crescent.Size() * 0.5f;
            float sizeMul = Reach / 118f;

            if (Life <= DelayFrames) {
                //滞留蓄势：起手角一点电青微光渐亮
                float charge = Life / (float)DelayFrames;
                Vector2 at = center + ArcFrom.ToRotationVector2() * (Reach * 0.7f);
                Color pre = GsCobaltSword.CobaltHot * (0.35f * charge);
                pre.A = 0;
                Main.EntitySpriteDraw(glow, at, null, pre, 0f, glow.Size() * 0.5f, 0.3f * charge, SpriteEffects.None, 0);
                return false;
            }

            float p = SweepP(Life);
            float ang = AngleAt(p);

            //尾随残弧：行程上三段旧角度渐淡
            for (int i = 1; i <= 3; i++) {
                float ghostAng = AngleAt(MathHelper.Clamp(p - 0.14f * i, 0f, 1f));
                Vector2 gPos = center + ghostAng.ToRotationVector2() * (Reach * 0.72f);
                Color trail = GsCobaltSword.CobaltMain * (0.2f * (1f - i / 4f) * fade);
                trail.A = 0;
                Main.EntitySpriteDraw(crescent, gPos, null, trail, ghostAng + sweepSign * 0.45f, origin,
                    new Vector2(0.42f, 0.26f) * sizeMul, SpriteEffects.None, 0);
            }

            Vector2 bladePos = center + ang.ToRotationVector2() * (Reach * 0.72f);
            float bladeRot = ang + sweepSign * 0.45f;
            //深蓝垫底
            Color deep = GsCobaltSword.CobaltDeep * (0.7f * fade);
            deep.A = 0;
            Main.EntitySpriteDraw(crescent, bladePos, null, deep, bladeRot, origin,
                new Vector2(0.5f, 0.32f) * sizeMul, SpriteEffects.None, 0);
            //钴蓝主体
            Color body = GsCobaltSword.CobaltMain * (0.75f * fade);
            body.A = 0;
            Main.EntitySpriteDraw(crescent, bladePos, null, body, bladeRot, origin,
                new Vector2(0.46f, 0.26f) * sizeMul, SpriteEffects.None, 0);
            //电青刃缘
            Color edge = GsCobaltSword.CobaltHot * (0.8f * fade);
            edge.A = 0;
            Main.EntitySpriteDraw(crescent, bladePos, null, edge, bladeRot, origin,
                new Vector2(0.4f, 0.16f) * sizeMul, SpriteEffects.None, 0);
            //刃尖电光
            Vector2 tip = center + ang.ToRotationVector2() * (Reach * 1.0f);
            Color tipC = GsCobaltSword.CobaltBright * (0.55f * fade);
            tipC.A = 0;
            Main.EntitySpriteDraw(glow, tip, null, tipC, 0f, glow.Size() * 0.5f, 0.22f, SpriteEffects.None, 0);
            return false;
        }
    }
}
