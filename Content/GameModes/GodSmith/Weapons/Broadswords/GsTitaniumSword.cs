using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
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
    /// 【钛影残身】材质：银灰钛钢冷锻的闪避金属（呼应钛金套的影护）。
    /// 签名：①每一斩在原地留下一道钛影残身，凝滞半拍后重演同一道斩击
    /// ②第二拍是「退身斩」：出刃同时向后撤步，留影在前自己先走
    /// ③残身自绘半透明刀形与冷灰弧光，凝滞期轮廓渐亮、重演时嘶鸣
    /// </summary>
    internal class GsTitaniumSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.TitaniumSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsTitaniumSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: every slash leaves a titanium shade hanging in the air " +
            "that replays the same cut half a beat later";

        //银灰钛钢色板
        internal static readonly Color TiBright = new(222, 230, 244); //钛亮银
        internal static readonly Color TiMain = new(148, 164, 196);   //钛冷灰
        internal static readonly Color TiHot = new(176, 208, 255);    //冷光泛蓝
        internal static readonly Color TiDeep = new(34, 40, 58);      //深钛影

        //预算账：拍均 (0.95+0.9+1.25)/3≈1.03；每斩钛影 0.22x 半拍后同弧重演
        //（单体重取 ~0.8 → +0.18/拍）；连段总帧 (19+18+25)=62 对原版 60 (+3%)
        //→ 综合单体 DPS ≈ (1.03+0.18)×0.97 ≈ 原版 103%~117%，底伤不再加成
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 钛影残身手持：三拍利落连击（0 正斩 / 1 退身斩：出刃同时后撤步 / 2 追影终结：
    /// 大前压追上自己的影）。每拍斩切爆发都在原地留钛影残身。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsTitaniumSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.TitaniumSword;
        protected override Color EdgeBright => GsTitaniumSword.TiBright;
        protected override Color BodyMain => GsTitaniumSword.TiMain;
        protected override Color HotAccent => GsTitaniumSword.TiHot;
        protected override Color DeepShadow => GsTitaniumSword.TiDeep;

        //冷金属吸一点光
        protected override Color BodyTint(Color lightColor)
            => Color.Lerp(lightColor, GsTitaniumSword.TiDeep, 0.12f);

        private bool shadeFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 正斩：干脆利落
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 3, Recover = 9,
                RaiseBack = 1.75f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 0.95f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍1 退身斩：短促收着打，人往后让
            1 => new GsBroadBeat {
                Raise = 4, Hold = 2, Slash = 3, Recover = 9,
                RaiseBack = 1.6f, Follow = 0.9f, ReachScale = 0.96f, LeanAmp = 0.04f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.08f,
            },
            //拍2 追影终结：大前压，追上残身叠刀
            _ => new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 4, Recover = 11,
                RaiseBack = 2.1f, Follow = 1.3f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 3.0f, SwingPitch = -0.22f,
            },
        };

        /// <summary>每拍留影；退身斩同时向后撤步（owner 端权威，位置随原版同步）</summary>
        protected override void OnSlashBegin() {
            if (!shadeFired) {
                shadeFired = true;
                float startAng = ArcStart - (swingDir * 0.08f);
                int shadeDamage = Math.Max(1, (int)(Projectile.damage * 0.22f));
                SpawnOwnedProj(ModContent.ProjectileType<GsTitaniumSwordShadeProj>(), Hand, Vector2.Zero,
                    shadeDamage, Projectile.knockBack * 0.3f, startAng, ArcEnd, FullReach);
            }
            //退身斩：出刃瞬间后撤步，影子替你站着
            if (ComboStage == 1 && Owner.whoAmI == Main.myPlayer && !Owner.mount.Active) {
                Owner.velocity.X -= facingDir * 2.4f;
            }
        }

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.32f, Pitch = -0.35f }, Owner.Center);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //冷冽钛音：低饱和银光，无暖色
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                GsTitaniumSword.TiHot, IsFinisher ? 0.22f : 0.14f)?.Configure(9, 0.7f);
        }
    }

    /// <summary>
    /// 钛影残身：挥砍留在原地的半透明刀影。凝滞 9 帧（轮廓自暗渐亮、微息浮动），
    /// 随后 5 帧沿同弧重演斩击（0.22x），残痕 8 帧散去。
    /// 自绘：原版剑贴图只作幽灵垫底，冷灰双层弧光与刃缘线为主层；
    /// 全部抖动 identity 播种。ai[0]=弧起角 ai[1]=弧止角 ai[2]=触及半径
    /// </summary>
    internal class GsTitaniumSwordShadeProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;
        public override LocalizedText DisplayName => Language.GetText("ItemName.TitaniumSword");

        private const int DormantFrames = 9;
        private const int SweepFrames = 5;
        private const int TotalFrames = 22;

        private float ArcFrom => Projectile.ai[0];
        private float ArcTo => Projectile.ai[1];
        private float Reach => Projectile.ai[2];
        private ref float Life => ref Projectile.localAI[0];

        /// <summary>重演行程 0~1</summary>
        private float SweepP(float life) =>
            MathHelper.Clamp((life - DormantFrames) / SweepFrames, 0f, 1f);

        /// <summary>行程角：与真刃同源的爆发缓动（快出缓收）</summary>
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
            if (Life == DormantFrames + 1 && !VaultUtils.isServer) {
                //残身出刀：金属嘶鸣 + 更薄的挥音
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.35f, Pitch = 0.35f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.4f, Pitch = 0.3f }, Projectile.Center);
            }
            float mid = MathHelper.Lerp(ArcFrom, ArcTo, 0.5f);
            Lighting.AddLight(Projectile.Center + mid.ToRotationVector2() * (Reach * 0.6f),
                GsTitaniumSword.TiHot.ToVector3() * 0.22f);

            if (VaultUtils.isServer) {
                return;
            }
            if (Life <= DormantFrames && Main.rand.NextBool(3)) {
                //凝滞期：冷光微尘绕影浮动
                float ang = ArcFrom + Main.rand.NextFloat(-0.3f, 0.3f);
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + ang.ToRotationVector2() * (Reach * Main.rand.NextFloat(0.3f, 0.9f)),
                    -Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.6f), GsTitaniumSword.TiHot,
                    Main.rand.NextFloat(0.04f, 0.08f))?.Configure(8, 0.5f);
            }
            else if (Life > DormantFrames && Life <= DormantFrames + SweepFrames) {
                //重演期：沿刃尾甩冷灰火星
                float ang = AngleAt(SweepP(Life));
                Vector2 at = Projectile.Center + ang.ToRotationVector2() * (Reach * Main.rand.NextFloat(0.6f, 1f));
                PRTLoader.NewParticle<PRT_Spark>(at,
                    (ang + MathHelper.PiOver2 * MathF.Sign(ArcTo - ArcFrom)).ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f),
                    Main.rand.NextBool() ? GsTitaniumSword.TiBright : GsTitaniumSword.TiHot,
                    Main.rand.NextFloat(0.26f, 0.45f))?.Configure(true, Main.rand.Next(8, 13));
            }
        }

        //只在重演期结算伤害
        public override bool? CanDamage() =>
            Life > DormantFrames && Life <= DormantFrames + SweepFrames + 1 ? null : false;

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
                    center + dir * (Reach * 0.35f), center + dir * (Reach * 1.02f), 28f, ref collisionPoint)) {
                    return true;
                }
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit4 with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 4.5f),
                    Main.rand.NextBool() ? GsTitaniumSword.TiBright : GsTitaniumSword.TiHot,
                    Main.rand.NextFloat(0.26f, 0.42f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>幽灵刀身朝向：与真刃同一套刃口镜像规则（由弧向与面向反推）</summary>
        private void GetGhostOrientation(out SpriteEffects effect, out float rotOffset) {
            float sweepSign = MathF.Sign(ArcTo - ArcFrom);
            int facing = MathF.Cos(MathHelper.Lerp(ArcFrom, ArcTo, 0.5f)) >= 0f ? 1 : -1;
            bool flipVertically = (facing < 0) != (sweepSign < 0);
            effect = flipVertically ? SpriteEffects.FlipVertically : SpriteEffects.None;
            rotOffset = flipVertically ? -MathHelper.PiOver4 : MathHelper.PiOver4;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            if (glow == null || smear == null) {
                return false;
            }
            Main.instance.LoadItem(ItemID.TitaniumSword);
            Texture2D blade = TextureAssets.Item[ItemID.TitaniumSword].Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            Vector2 bladeOrigin = blade.Size() * 0.5f;
            GetGhostOrientation(out SpriteEffects effect, out float rotOffset);
            //刀形贴图缩放与真刃同一换算（BladePark 0.46 / BladeTipFill 1.02）
            float scale = Reach * (1.02f - 0.46f) * 2f / MathF.Max(new Vector2(blade.Width, blade.Height).Length(), 1f);
            float sweepSign = MathF.Sign(ArcTo - ArcFrom);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 7f, 0f, 1f);
            float breath = 1f + 0.025f * MathF.Sin(Life * 0.55f + SegRand(3) * 6.28f);

            if (Life <= DormantFrames) {
                //凝滞：半透明刀影悬在起手角，轮廓渐亮、微息浮动
                float charge = Life / (float)DormantFrames;
                Vector2 at = center + ArcFrom.ToRotationVector2() * (Reach * 0.46f);
                Color ghost = GsTitaniumSword.TiMain * (0.18f + 0.22f * charge);
                ghost.A = 0;
                Main.EntitySpriteDraw(blade, at, null, ghost, ArcFrom + rotOffset, bladeOrigin,
                    scale * breath, effect, 0);
                Color rim = GsTitaniumSword.TiHot * (0.14f + 0.3f * charge);
                rim.A = 0;
                Main.EntitySpriteDraw(blade, at, null, rim, ArcFrom + rotOffset, bladeOrigin,
                    scale * breath * 1.04f, effect, 0);
                //柄座冷光渐聚
                Color hiltGlow = GsTitaniumSword.TiHot * (0.3f * charge);
                hiltGlow.A = 0;
                Main.EntitySpriteDraw(glow, center, null, hiltGlow, 0f, glow.Size() * 0.5f,
                    0.24f * charge, SpriteEffects.None, 0);
                return false;
            }

            float p = SweepP(Life);
            float ang = AngleAt(p);

            //重演/残痕：冷灰双层弧光沿当前角走
            Vector2 arcAt = center + ang.ToRotationVector2() * (Reach * 0.55f);
            float arcRot = ang + sweepSign * 0.35f;
            float arcAlpha = (0.3f + 0.3f * p) * fade;
            Color outer = GsTitaniumSword.TiBright * arcAlpha;
            outer.A = 0;
            Main.EntitySpriteDraw(smear, arcAt, null, outer, arcRot, smear.Size() / 2f,
                new Vector2(0.44f, 0.2f) * (Reach / 118f), SpriteEffects.None, 0);
            Color innerArc = GsTitaniumSword.TiHot * (arcAlpha * 0.7f);
            innerArc.A = 0;
            Main.EntitySpriteDraw(smear, arcAt, null, innerArc, arcRot, smear.Size() / 2f,
                new Vector2(0.4f, 0.09f) * (Reach / 118f), SpriteEffects.None, 0);

            if (Life <= DormantFrames + SweepFrames) {
                //重演期：半透明刀影执行同一道斩击，两道旧角残影跟随
                for (int g = 2; g >= 1; g--) {
                    float ghostAng = AngleAt(MathHelper.Clamp(p - 0.16f * g, 0f, 1f));
                    Vector2 gPos = center + ghostAng.ToRotationVector2() * (Reach * 0.46f);
                    Color trail = GsTitaniumSword.TiMain * (g == 1 ? 0.2f : 0.1f);
                    trail.A = 0;
                    Main.EntitySpriteDraw(blade, gPos, null, trail, ghostAng + rotOffset, bladeOrigin,
                        scale, effect, 0);
                }
                Vector2 bladeAt = center + ang.ToRotationVector2() * (Reach * 0.46f);
                Color body = GsTitaniumSword.TiMain * 0.5f;
                body.A = 0;
                Main.EntitySpriteDraw(blade, bladeAt, null, body, ang + rotOffset, bladeOrigin, scale, effect, 0);
                Color edgeLine = GsTitaniumSword.TiBright * 0.4f;
                edgeLine.A = 0;
                Main.EntitySpriteDraw(blade, bladeAt, null, edgeLine, ang + rotOffset, bladeOrigin,
                    scale * 1.03f, effect, 0);
            }
            else {
                //残痕期：刃尖冷光珠沿弧散逸
                for (int i = 0; i < 4; i++) {
                    float t = (i + 0.5f) / 4f;
                    float dieAt = 0.4f + 0.6f * SegRand(i + 20);
                    float segFade = MathHelper.Clamp((dieAt - (1f - fade)) / 0.35f, 0f, 1f) * fade;
                    if (segFade <= 0.02f) {
                        continue;
                    }
                    float scatterAng = MathHelper.Lerp(ArcFrom, ArcTo, t);
                    Vector2 at = center + scatterAng.ToRotationVector2() * (Reach * (0.9f + 0.1f * SegRand(i + 40)));
                    Color spark = GsTitaniumSword.TiHot * (0.4f * segFade);
                    spark.A = 0;
                    Main.EntitySpriteDraw(glow, at, null, spark, 0f, glow.Size() * 0.5f,
                        0.12f + 0.06f * SegRand(i + 60), SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
