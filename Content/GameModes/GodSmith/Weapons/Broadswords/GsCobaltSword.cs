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
    /// 【蓝钢疾影】材质：轻锻钴蓝合金。
    /// 签名：①「动量」连段不断档则出刀渐快（每层拍表帧数 -3.5%，至多五层），断手清零
    /// ②第四拍拖出一道疾影残弧，紧随其后沿同弧补上第二段判定
    /// ③动量越高挥砍音越锐
    /// </summary>
    internal class GsCobaltSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.CobaltSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsCobaltSwordHeld>();

        protected override int ComboBeats => 4;

        //动量剑的续段窗口稍宽，层数更容易保住
        protected override int ComboResetFrames => 60;

        protected override string GsDescFallback =>
            "Reforged: keep the combo unbroken and the cobalt edge swings ever faster; the fourth strike trails a blue echo arc that cuts again";
        internal static readonly Color CobaltBright = new(168, 214, 255); //钴亮蓝
        internal static readonly Color CobaltMain = new(58, 112, 224);    //钴蓝钢
        internal static readonly Color CobaltHot = new(144, 240, 255);    //疾影电青

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
    /// 四相帧数整体缩短；层数越高音越锐。终结拍放疾影残弧补刀。
    /// ai[0]=拍号 ai[1]=交替符号×(1+动量)
    /// </summary>
    internal class GsCobaltSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.CobaltSword;
        protected override int BeatCount => 4;
        protected override Color EdgeBright => GsCobaltSword.CobaltBright;
        protected override Color BodyMain => GsCobaltSword.CobaltMain;
        protected override Color HotAccent => GsCobaltSword.CobaltHot;

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
                Volume = 0.8f,
                Pitch = Beat.SwingPitch + 0.05f * momentumStacks
            }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.32f, Pitch = -0.2f }, Owner.Center);
            }
        }

        /// <summary>终结拍：沿本次挥弧放疾影残弧，半拍不到便补第二刀</summary>
        protected override void OnSlashBegin() {
            if (!IsFinisher || echoFired) {
                return;
            }
            echoFired = true;
            float startAng = ArcStart - (swingDir * 0.08f);
            int echoDamage = Math.Max(1, (int)(Projectile.damage * 0.45f));
            SpawnOwnedProj(ModContent.ProjectileType<GsCobaltSwordEchoProj>(), Hand, Vector2.Zero,
                echoDamage, Projectile.knockBack * 0.4f, startAng, ArcEnd, FullReach);
        }
    }

    /// <summary>
    /// 疾影残弧：终结斩后紧随的第二段判定，驻在出手点，滞 4 帧后用 5 帧沿同弧重演一次判定。
    /// 用原版钴蓝剑物品贴图沿当前弧角画一笔本体。ai[0]=弧起角 ai[1]=弧止角 ai[2]=触及半径
    /// </summary>
    internal class GsCobaltSwordEchoProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.CobaltSword;
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
                //残弧启动：一记更高更薄的挥音
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.45f, Pitch = 0.32f }, Projectile.Center);
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

        /// <summary>本体一笔：滞留期不画，重演期起把原版剑贴图按当前弧角摆在出手点外，随寿命淡出</summary>
        public override bool PreDraw(ref Color lightColor) {
            if (Life <= DelayFrames) {
                return false;
            }
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            float ang = AngleAt(SweepP(Life));
            float fade = MathHelper.Clamp(Projectile.timeLeft / 6f, 0f, 1f);
            //镜像基类翻刃规则：扫向为负或面朝左时纵向翻转，让刃口朝挥动前缘
            bool edgeFlip = ArcTo < ArcFrom;
            bool facingLeft = MathF.Cos(ang) < 0f;
            bool flip = facingLeft != edgeFlip;
            SpriteEffects effect = flip ? SpriteEffects.FlipVertically : SpriteEffects.None;
            float rotOffset = flip ? -MathHelper.PiOver4 : MathHelper.PiOver4;
            float scale = Reach * 0.56f * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
            Vector2 drawPos = Projectile.Center + ang.ToRotationVector2() * (Reach * 0.46f) - Main.screenPosition;
            Main.EntitySpriteDraw(tex, drawPos, null, lightColor * fade, ang + rotOffset, tex.Size() * 0.5f,
                scale, effect, 0);
            return false;
        }
    }
}
