using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 悬停半拍再倾泻烬弹砸向目标。<br/>
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

        /// <summary>引爆所需引焰层数</summary>
        protected virtual int StacksNeeded => 6;

        /// <summary>影焰模式（暗艺档：暗影焰 + 四枚影烬）</summary>
        protected virtual bool ShadowMode => false;

        /// <summary>本套的三档火焰爆哨兵弹丸</summary>
        private static bool IsFlameburstShot(int type) =>
            type == ProjectileID.DD2FlameBurstTowerT1Shot
            || type == ProjectileID.DD2FlameBurstTowerT2Shot
            || type == ProjectileID.DD2FlameBurstTowerT3Shot;

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
    /// 五层即可引爆，倾泻四枚影烬并点燃暗影焰
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
    /// 焰文与烬弹（双态同类）：ai[1]=0 为焰文（隐形的悬停编排态，纯流程无伤），
    /// 悬停 22 帧后 owner 侧倾泻 ai[1]=1 的烬弹；烬弹俯冲加速；
    /// ai[2]=1 影焰模式点燃暗影焰
    /// </summary>
    internal class GsApprenticeGlyphProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

        /// <summary>焰文悬停帧数</summary>
        private const int HoverFrames = 22;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>0 = 焰文，1 = 烬弹</summary>
        private ref float Mode => ref Projectile.ai[1];

        /// <summary>1 = 影焰（暗艺档）</summary>
        private ref float ShadowSet => ref Projectile.ai[2];

        private bool IsGlyph => Mode == 0f;

        private float Seed => Projectile.identity * 0.7907f % 3.53f;

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
        }

        private void GlyphAI() {
            //焰文本体不打人，只作倾泻前的悬停编排
            Projectile.friendly = false;
            Projectile.velocity = new Vector2(0f, MathF.Sin(Life * 0.25f + Seed) * 0.3f);
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
            if (Main.dedServ || IsGlyph) {
                return;
            }
            //烬弹消亡的响声
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
        }

        /// <summary>焰文态是隐形编排弹不画本体；烬弹态走默认绘制</summary>
        public override bool PreDraw(ref Color lightColor) => !IsGlyph;
    }
}
