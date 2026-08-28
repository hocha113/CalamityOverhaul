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
    /// 【蜂蜡蜂刃】材质：蜂巢蜡髓浇铸的黄黑条纹蜂刃。签名「蜂群协奏」：
    /// ①每记命中自蜂蜡刃口放出 1~2 只护巢蜂 ②第四拍「蜂拥」，举刀时三只蜂影绕刃盘旋，
    /// 爆发时化作真蜂齐射 ③命中糊上滴淌的蜂蜡黏浆（Slimed 可见滴淌）并挂蜜色黏丝
    /// </summary>
    internal class GsBeeKeeper : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.BeeKeeper;

        protected override int HeldProjID => ModContent.ProjectileType<GsBeeKeeperHeld>();

        protected override int ComboBeats => 4;

        protected override string GsDescFallback =>
            "Reforged: every strike releases hive bees from the waxen edge; " +
            "the fourth swing gathers three bee shades around the blade and looses them all at once, and wounds drip sticky wax";

        //蜂蜡色板
        internal static readonly Color WaxBright = new(255, 232, 160);  //蜡髓淡金
        internal static readonly Color WaxMain = new(224, 170, 62);     //蜂蜜琥珀
        internal static readonly Color WaxHot = new(255, 202, 70);      //蜜金强调
        internal static readonly Color WaxDeep = new(30, 24, 10);       //蜂纹黑

        //底乘 1.0：蜂收益就是主预算——每命中 1~2 蜂（期望 1.5）×前三拍 + 蜂拥 3 蜂
        //≈ 7.5 蜂/循环（原版命中期望 2 蜂×3 挥 = 6），单蜂约 1/3 底伤；
        //近战终结仅 1.15x，蜂蜡黏浆为纯演出层，综合 DPS 约为原版 105%~115%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1f;
    }

    /// <summary>
    /// 蜂蜡蜂刃手持：四拍蜂群连击。0/1/2 轻快短扫（蜂翅般的碎步节奏），
    /// 3 蜂拥终结（举刀蜂影绕刃、爆发三蜂齐射+小前压）。
    /// 残影黄黑交替（基类亮蜡残影 + DrawExtra 补暗纹层）。ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsBeeKeeperHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.BeeKeeper;
        protected override Color EdgeBright => GsBeeKeeper.WaxBright;
        protected override Color BodyMain => GsBeeKeeper.WaxMain;
        protected override Color HotAccent => GsBeeKeeper.WaxHot;
        protected override Color DeepShadow => GsBeeKeeper.WaxDeep;

        protected override int BeatCount => 4;
        //黄黑交替条纹：亮蜡残影多铺一层，暗纹由 DrawExtra 补
        protected override int GhostCount => IsFinisher ? 4 : 3;

        protected override GsBroadBeat GetBeat(int stage) {
            if (stage == 3) {
                //蜂拥终结：举刀蜂影绕刃读拍，爆发齐射带小前压
                return new GsBroadBeat {
                    Raise = 8, Hold = 4, Slash = 4, Recover = 10,
                    RaiseBack = 2.05f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.07f,
                    DamageMult = 1.15f, Hitstop = 2, LungeSpeed = 2.0f, SwingPitch = -0.2f,
                };
            }
            //蜂翅碎步：三记轻快短扫，音高错落如振翅
            return new GsBroadBeat {
                Raise = stage == 2 ? 5 : 4, Hold = 1, Slash = 3, Recover = stage == 2 ? 7 : 6,
                RaiseBack = 1.7f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f,
                SwingPitch = stage switch { 0 => 0.05f, 1 => 0.13f, _ => -0.04f },
            };
        }

        /// <summary>owner 端放一只原版护巢蜂（beeType/beeDamage/beeKB 走原版换算，保留强化蜂特性）</summary>
        private void ReleaseBee(Vector2 pos, Vector2 vel, int baseDamage) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            //beeType 会掷 Main.rand 并写 makeStrongBee，须与 beeDamage/beeKB 同端连读
            int type = Owner.beeType();
            int idx = SpawnOwnedProj(type, pos, vel, Owner.beeDamage(baseDamage / 3), Owner.beeKB(0f));
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].DamageType = DamageClass.Melee;//镜像原版蜂刀的近战蜂
                Main.projectile[idx].GetGlobalProjectile<GsBeeKeeperGlobalProj>().WaxTrail = true;
                Main.projectile[idx].netUpdate = true;
            }
        }

        protected override void OnSlashBegin() {
            if (!IsFinisher) {
                return;
            }
            //蜂拥：举刀盘旋的三只蜂影化作真蜂，沿出手向扇形齐射
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            for (int i = 0; i < 3; i++) {
                Vector2 vel = (baseAngle + (i - 1) * 0.32f).ToRotationVector2() * 6.5f;
                ReleaseBee(Vector2.Lerp(Hand, mainTip, 0.6f), vel, baseDamage);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.6f, Pitch = 0.15f }, Owner.Center);
            }
        }

        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            //蜂蜡黏浆：Slow 对 NPC 无效，改挂 Slimed（真实滴淌演出，原版 buff 自带同步）
            target.AddBuff(BuffID.Slimed, 40);
            //命中放蜂 1~2 只（owner 端掷数，生成包同步）
            if (Owner.whoAmI == Main.myPlayer) {
                int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
                int count = Main.rand.Next(1, 3);
                for (int i = 0; i < count; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 2.4f);
                    ReleaseBee(target.Center + Main.rand.NextVector2Circular(8f, 8f), vel, baseDamage);
                }
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //蜜色黏丝：垂坠的蜂蜜尘 + 蜡金光珠
            for (int i = 0; i < (IsFinisher ? 6 : 4); i++) {
                Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(10f, 8f),
                    DustID.Honey, new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.8f, 2f)),
                    60, default, Main.rand.NextFloat(0.9f, 1.4f));
                d.noGravity = false;
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero,
                GsBeeKeeper.WaxHot, 0.13f + (IsFinisher ? 0.08f : 0f))?.Configure(9, 0.7f);
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            //斩切期洒蜡屑：带重力的蜜金碎屑自刃口剥落
            if (phase == PhaseSlash && Main.rand.NextBool(2)) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1f));
                PRTLoader.NewParticle<PRT_Spark>(at,
                    (mainAngle + swingDir * MathHelper.PiOver2).ToRotationVector2() * Main.rand.NextFloat(2f, 4f)
                    + new Vector2(0f, 0.6f),
                    GsBeeKeeper.WaxMain, Main.rand.NextFloat(0.24f, 0.38f))?.Configure(true, Main.rand.Next(12, 18));
            }
            //蜂拥蓄力：环刃蜂鸣的嗡嗡声底（Item97 为原版蜂械射音，压低作振翅底噪）
            if (IsFinisher && phase == PhaseRaise && timer == 2 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.3f, Pitch = -0.35f }, Owner.Center);
            }
        }

        /// <summary>
        /// 自绘层：①斩切期黄黑交替暗纹残影（真 alpha 暗蜡层与基类亮蜡残影相间）
        /// ②蜂拥举刀时三只蜂影绕刃盘旋（加色蜂点 + 翅闪，轨道相位 identity 播种，纯演出非弹幕）
        /// </summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            int phase = CurrentPhase;

            //黄黑交替：暗纹残影插在亮蜡残影的间隙角位
            if (phase == PhaseSlash && slashProgress > 0.10f) {
                Main.instance.LoadItem(SwordItemID);
                Texture2D tex = TextureAssets.Item[SwordItemID].Value;
                GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
                float scale = mainReach * (BladeTipFill - BladePark) * 2f / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
                float spacing = GhostSpacing;
                for (int g = 1; g <= 2; g++) {
                    float ghostAngle = mainAngle - (swingDir * spacing * (g - 0.5f));
                    //真 alpha 暗蜡：与加色亮影相间成条纹
                    Color dark = new Color(GsBeeKeeper.WaxDeep.R, GsBeeKeeper.WaxDeep.G, GsBeeKeeper.WaxDeep.B,
                        (byte)150) * (g == 1 ? 0.4f : 0.22f);
                    Vector2 gPos = Hand + (ghostAngle.ToRotationVector2() * mainReach * BladePark) - Main.screenPosition;
                    sb.Draw(tex, gPos, null, dark, ghostAngle + rotOffset, tex.Size() / 2f, scale, effect, 0);
                }
            }

            //蜂拥蜂影：举刀与滞帧期绕刃盘旋的三只演出蜂
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blot == null || glow == null) {
                return;
            }
            float reveal = MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
            Vector2 bladeMid = Vector2.Lerp(Hand, mainTip, 0.62f);
            for (int i = 0; i < 3; i++) {
                float orbit = timer * 0.22f + DrawRand01(i * 13 + 2) * MathHelper.TwoPi;
                Vector2 at = bladeMid + new Vector2(MathF.Cos(orbit) * 30f, MathF.Sin(orbit * 1.4f) * 18f)
                    - Main.screenPosition;
                //蜂体：真 alpha 黑蜡小椭圆
                Color body = GsBeeKeeper.WaxDeep * (reveal * 0.8f);
                sb.Draw(blot, at, null, body, orbit, blot.Size() / 2f,
                    new Vector2(0.045f, 0.028f), SpriteEffects.None, 0f);
                //蜂晕：加色蜜金点
                Color halo = GsBeeKeeper.WaxHot * (reveal * 0.55f);
                halo.A = 0;
                sb.Draw(glow, at, null, halo, 0f, glow.Size() / 2f, 0.16f, SpriteEffects.None, 0f);
                //翅闪：高频明灭的小白点（确定性正弦，不掷 Main.rand）
                float flick = MathF.Sin(timer * 1.6f + i * 2.1f) > 0f ? 0.5f : 0.12f;
                Color wing = Color.White * (reveal * flick);
                wing.A = 0;
                sb.Draw(glow, at - new Vector2(0f, 4f), null, wing, 0f, glow.Size() / 2f, 0.07f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 蜂刃放出的护巢蜂的蜡尘拖尾（纯演出）。标记只在生成端写入，
    /// 远端蜂不带拖尾（蜂体本身经生成包全端可见，无状态分歧）
    /// </summary>
    internal class GsBeeKeeperGlobalProj : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
            => entity.type == ProjectileID.Bee || entity.type == ProjectileID.GiantBee;

        /// <summary>蜂刃亲生蜂标记（生成端本地量）</summary>
        internal bool WaxTrail;

        public override void PostAI(Projectile projectile) {
            if (!WaxTrail || VaultUtils.isServer) {
                return;
            }
            if (Main.rand.NextBool(5)) {
                Dust d = Dust.NewDustPerfect(projectile.Center, DustID.Honey,
                    -projectile.velocity * 0.15f, 120, default, Main.rand.NextFloat(0.6f, 0.9f));
                d.noGravity = true;
            }
        }
    }
}
