using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Endgame
{
    /// <summary>
    /// 【神赋·学徒套 T1】「引焰术」：学徒烬焰（借哨兵之火续燃的术焰）。
    /// ①火焰爆哨兵的命中为你积攒引焰层；②攒满后下一次魔法命中在目标上方展开焰文，
    /// 悬停半拍再倾泻烬弹砸向目标；③烬弹命中爆出焰屑，火星受重力回落。<br/>
    /// 与原版套装技联动：原版扩展火焰爆的视野与射程，神赋让哨兵命中兼职燃料，
    /// 哨兵本体一概不改；引焰层是攻击方端本地量，焰文与烬弹 owner 侧生成
    /// </summary>
    internal class GsApprenticeArmor : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsC";

        public override int[] HeadIDs => [ItemID.ApprenticeHat];

        public override int BodyID => ItemID.ApprenticeRobe;

        public override int LegsID => ItemID.ApprenticeTrousers;

        protected override string EndowLineFallback =>
            "Kindlecraft: your Flameburst tower's hits build kindling; at six stacks the next magic hit unfolds a fire glyph over the target, raining three ember bolts";

        //学徒烬焰色板
        internal static readonly Color FlameBright = new(255, 232, 150);
        internal static readonly Color FlameMain = new(255, 130, 42);
        internal static readonly Color FlameDeep = new(96, 38, 14);
        //暗艺影焰色板
        internal static readonly Color ShadowBright = new(240, 200, 255);
        internal static readonly Color ShadowMain = new(152, 70, 220);
        internal static readonly Color ShadowDeep = new(40, 16, 60);

        /// <summary>引爆所需引焰层数</summary>
        protected virtual int StacksNeeded => 6;

        /// <summary>影焰模式（暗艺档：紫黑色板 + 暗影焰 + 四枚影烬）</summary>
        protected virtual bool ShadowMode => false;

        /// <summary>本套的三档火焰爆哨兵弹丸</summary>
        private static bool IsFlameburstShot(int type) =>
            type == ProjectileID.DD2FlameBurstTowerT1Shot
            || type == ProjectileID.DD2FlameBurstTowerT2Shot
            || type == ProjectileID.DD2FlameBurstTowerT3Shot;

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            if (VaultUtils.isServer || state.EndowCharge < StacksNeeded) {
                return;
            }
            //满焰读数：术焰绕腕微光（层数只在攻击方端存在，读数天然只有本人可见）
            Color glow = ShadowMode ? ShadowMain : FlameMain;
            Lighting.AddLight(player.Center, glow.ToVector3() * 0.2f);
            if (Main.rand.NextBool(9)) {
                PRTLoader.NewParticle<PRT_Light>(player.Center + Main.rand.NextVector2CircularEdge(16f, 22f),
                    new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)), glow, Main.rand.NextFloat(0.07f, 0.12f))
                    ?.Configure(13, 0.7f);
            }
        }

        public override void OnEndowHitNPC(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone, Projectile sourceProj) {
            //焰文与烬弹自身命中不喂层，防自循环；假人不算数
            if (sourceProj != null && sourceProj.type == ModContent.ProjectileType<GsApprenticeGlyphProj>()) {
                return;
            }
            if (target.type == NPCID.TargetDummy) {
                return;
            }

            //火焰爆哨兵命中：积攒引焰
            if (sourceProj != null && IsFlameburstShot(sourceProj.type)) {
                if (state.EndowCharge < StacksNeeded) {
                    state.EndowCharge++;
                    if (!VaultUtils.isServer && state.EndowCharge == StacksNeeded) {
                        SoundEngine.PlaySound(SoundID.DD2_DarkMageAttack with { Volume = 0.5f, Pitch = 0.4f }, player.Center);
                    }
                }
                return;
            }

            //满焰后的魔法命中：展开焰文
            if (state.EndowCharge < StacksNeeded || !hit.DamageType.CountsAsClass(DamageClass.Magic)) {
                return;
            }
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_FlameburstTowerShot with { Volume = 0.8f, Pitch = -0.2f }, target.Center);
            }
            if (player.whoAmI == Main.myPlayer) {
                //烬弹总伤按触发伤害折算并封顶；需要哨兵喂满六层，收益在神赋包络内
                int emberDamage = Math.Clamp((int)(damageDone * 0.30f), 10, 300);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithApprenticeEndow"),
                    target.Center + new Vector2(0f, -120f), Vector2.Zero,
                    ModContent.ProjectileType<GsApprenticeGlyphProj>(), emberDamage, 2f, player.whoAmI,
                    0f, 0f, ShadowMode ? 1f : 0f);
            }
        }
    }

    /// <summary>
    /// 【神赋·学徒套 T3 暗黑艺术家装】「引焰术·暗艺」：同一门术法坠入影侧。
    /// 五层即可引爆，焰文化作影焰文，倾泻四枚影烬并点燃暗影焰
    /// </summary>
    internal class GsApprenticeDarkArtistArmor : GsApprenticeArmor
    {
        public override int[] HeadIDs => [ItemID.ApprenticeAltHead];

        public override int BodyID => ItemID.ApprenticeAltShirt;

        public override int LegsID => ItemID.ApprenticeAltPants;

        protected override string EndowLineFallback =>
            "Kindlecraft, Dark Artist: five stacks suffice, and the glyph turns to shadowflame, raining four shadow embers";

        protected override int StacksNeeded => 5;

        protected override bool ShadowMode => true;
    }

    /// <summary>
    /// 焰文与烬弹（双态同类）：ai[1]=0 为焰文（悬停展开的旋转符环，纯演出无伤），
    /// 悬停 22 帧后 owner 侧倾泻 ai[1]=1 的烬弹；烬弹俯冲加速、沿速度拉伸；
    /// ai[2]=1 影焰模式换紫黑色板并点燃暗影焰。命中焰屑迸散，火星受重力回落
    /// </summary>
    internal class GsApprenticeGlyphProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>焰文悬停帧数</summary>
        private const int HoverFrames = 22;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>0 = 焰文，1 = 烬弹</summary>
        private ref float Mode => ref Projectile.ai[1];

        /// <summary>1 = 影焰（暗艺档）</summary>
        private ref float ShadowSet => ref Projectile.ai[2];

        private bool IsGlyph => Mode == 0f;

        private float Seed => Projectile.identity * 0.7907f % 3.53f;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        private Color CBright => ShadowSet == 1f ? GsApprenticeArmor.ShadowBright : GsApprenticeArmor.FlameBright;
        private Color CMain => ShadowSet == 1f ? GsApprenticeArmor.ShadowMain : GsApprenticeArmor.FlameMain;
        private Color CDeep => ShadowSet == 1f ? GsApprenticeArmor.ShadowDeep : GsApprenticeArmor.FlameDeep;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (IsGlyph) {
                GlyphAI();
                return;
            }
            //烬弹：俯冲持续加速
            if (Projectile.velocity.Length() < 20f) {
                Projectile.velocity *= 1.06f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.5f,
                    Projectile.velocity * 0.08f + Main.rand.NextVector2Circular(0.5f, 0.5f),
                    Main.rand.NextBool(3) ? CDeep : CMain,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
            Lighting.AddLight(Projectile.Center, CMain.ToVector3() * (0.28f * VisualFade));
        }

        private void GlyphAI() {
            //焰文本体不打人，只作倾泻前的悬停演出
            Projectile.friendly = false;
            Projectile.velocity = new Vector2(0f, MathF.Sin(Life * 0.25f + Seed) * 0.3f);
            Projectile.rotation += 0.06f;
            Lighting.AddLight(Projectile.Center, CMain.ToVector3() * (0.35f * VisualFade));
            if (!Main.dedServ && Life % 4 == 0) {
                //符环边缘火星游走（更新路径掷 rand 无碍）
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + ang.ToRotationVector2() * 22f,
                    ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.5f,
                    CMain, 0.3f)?.Configure(false, 12);
            }
            if (Life < HoverFrames) {
                return;
            }
            //悬停期满：owner 侧倾泻烬弹后焰文谢幕
            if (Projectile.owner == Main.myPlayer) {
                int count = 3 + (int)ShadowSet;//影焰档四枚
                for (int i = 0; i < count; i++) {
                    Vector2 aim = Projectile.Center + new Vector2((i - (count - 1) * 0.5f) * 36f, 150f);
                    Vector2 vel = (aim - Projectile.Center).SafeNormalize(Vector2.UnitY) * 7f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center, vel, Projectile.type,
                        Projectile.damage, Projectile.knockBack, Projectile.owner, 0f, 1f, ShadowSet);
                }
            }
            Projectile.Kill();
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (ShadowSet == 1f) {
                target.AddBuff(BuffID.ShadowFlame, 240);
            }
            else {
                target.AddBuff(BuffID.OnFire, 240);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            if (IsGlyph) {
                //焰文谢幕：符环收拢成一圈火星
                for (int i = 0; i < 8; i++) {
                    float ang = MathHelper.TwoPi * i / 8f;
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center + ang.ToRotationVector2() * 20f,
                        -ang.ToRotationVector2() * 2f, CMain, 0.34f)?.Configure(false, 12);
                }
                return;
            }
            //烬弹余痕：焰屑迸散后受重力回落，比弹体活得久
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero, CBright, 0.13f)?.Configure(8, 0.7f);
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool() ? CMain : CDeep,
                    Main.rand.NextFloat(0.26f, 0.46f))?.Configure(true, Main.rand.Next(16, 28));
            }
        }

        //==================== 绘制：焰文=旋转符环，烬弹=三层焰体 + 速度拉伸 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98?.Value;
            if (tex == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() * 0.5f;
            Color core = CBright with { A = 0 };

            if (IsGlyph) {
                //展开-收拢的符环生命周期
                float open = MathHelper.Clamp(Life / 8f, 0f, 1f) * (1f - MathHelper.Clamp((Life - HoverFrames + 4f) / 4f, 0f, 0.6f));
                //暗焰压边环
                Main.EntitySpriteDraw(tex, pos, null, CDeep * (0.8f * fade), Projectile.rotation, origin,
                    new Vector2(0.5f, 0.5f) * open, SpriteEffects.None, 0);
                //术焰主体环
                Main.EntitySpriteDraw(tex, pos, null, CMain * (0.9f * fade), -Projectile.rotation * 0.7f, origin,
                    new Vector2(0.4f, 0.4f) * open, SpriteEffects.None, 0);
                //亮芯：加色三缝旋转拟符纹
                for (int i = 0; i < 3; i++) {
                    float ang = Projectile.rotation * 1.4f + MathHelper.TwoPi * i / 3f;
                    Main.EntitySpriteDraw(tex, pos, null, core * (0.55f * fade), ang, origin,
                        new Vector2(0.32f, 0.05f) * open, SpriteEffects.None, 0);
                }
                return false;
            }

            //烬弹：俯冲拉伸的三层焰体
            Vector2 dPos = pos + Projectile.velocity * 0.3f;
            float rotation = Projectile.rotation;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.04f, 0.12f, 0.8f);
            float wob = MathF.Sin(Life * 0.7f + Seed * 6f) * 0.08f;

            Main.EntitySpriteDraw(tex, dPos, null, CDeep * (0.85f * fade), rotation, origin,
                new Vector2(0.24f + wob, 0.30f + stretch * 0.8f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, dPos, null, CMain * fade, rotation, origin,
                new Vector2(0.18f + wob, 0.24f + stretch * 0.65f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, dPos, null, core * (0.6f * fade), rotation, origin,
                new Vector2(0.08f, 0.13f + stretch * 0.3f), SpriteEffects.None, 0);
            return false;
        }
    }
}
