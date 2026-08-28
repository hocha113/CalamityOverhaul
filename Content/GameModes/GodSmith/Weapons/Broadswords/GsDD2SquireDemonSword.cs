using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 【狱火印】材质：地狱窑口淬出的军团骑士剑，剑格烙着会苏醒的狱火印记。
    /// 签名：①原版举盾完整保留：手持按住右键举盾（原版按物品类型判定，接管后照常生效），
    /// 成功格挡获得格挡增益，下一斩命中结算原版同款 5 倍力度（+4f ScalingBonusDamage，
    /// 接管后物品不再直击，结算重接进手持 ModifyHitExtra）
    /// ②成功格挡瞬间火印爆闪，刃身燃狱火：其后 3 次挥砍放出火焰刃波并点燃敌人
    /// ③平时挥砍甩余烬火星，狱火斩刃焰舔身
    /// </summary>
    internal class GsDD2SquireDemonSword : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.DD2SquireDemonSword;

        protected override int HeldProjID => ModContent.ProjectileType<GsDD2SquireDemonSwordHeld>();

        protected override string GsDescFallback =>
            "Reforged: raise the shield with right click as ever; a perfect block still " +
            "repays fivefold force on the next strike, and brands the blade with hellfire: " +
            "the next three slashes hurl burning waves that ignite foes";

        //狱火色板
        internal static readonly Color InfernoBright = new(255, 216, 148); //焰亮鎏金
        internal static readonly Color InfernoMain = new(250, 122, 44);    //狱火橙
        internal static readonly Color InfernoHot = new(255, 66, 22);      //熔核赤
        internal static readonly Color InfernoDeep = new(36, 18, 14);      //焦铁垫影

        /// <summary>剩余狱火挥砍数（0~3）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int FlameSwings;

        /// <summary>格挡增益上沿检测（myPlayer 专用）</summary>
        private bool parrySeen;

        //底伤不加成：拍均 1.0/1.05/1.3，三拍循环约 64 帧 = 3.35x/64f，对上原版 3.0x/60f 约 105%；
        //火焰刃波 0.45x 只在格挡后 3 斩出现（技巧条件收益）；格挡 5 倍结算与原版等额，不计入常态包络
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }

        /// <summary>
        /// 镜像基类生成流程，追加 ai[2]=狱火旗（随生成包过线，远端刃焰演出一致）；
        /// 狱火计数在 myPlayer 守门内消耗
        /// </summary>
        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.ownedProjectileCounts[HeldProjID] > 0) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                int beat = comboCounter % ComboBeats;
                float swingSign = comboCounter % 2 == 0 ? 1f : -1f;
                ModifyLocalSwing(item, player, ref beat, ref swingSign);
                comboCounter++;
                comboResetTimer = ComboResetFrames;
                float flame = 0f;
                if (FlameSwings > 0) {
                    FlameSwings--;
                    flame = 1f;
                }
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    HeldProjID, player.GetWeaponDamage(item), item.knockBack, player.whoAmI, beat, swingSign, flame);
            }
            return false;
        }

        /// <summary>格挡成功的瞬间（原版 buff 198 上沿）点燃狱火印：3 次狱火挥砍 + 火印爆闪</summary>
        public override void GsHoldItem(Item item, Player player) {
            base.GsHoldItem(item, player); //连段衰减记账
            if (player.whoAmI != Main.myPlayer) {
                return; //myPlayer 守门（服务器上恒不等，天然排除）
            }
            bool parry = player.HasBuff(BuffID.ParryDamageBuff);
            if (parry && !parrySeen) {
                FlameSwings = 3;
                //火印爆闪：狱火自身前炸开
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.5f, Pitch = 0.25f }, player.Center);
                Vector2 at = player.Center + new Vector2(player.direction * 14f, -4f);
                PRTLoader.NewParticle<PRT_Light>(at, Vector2.Zero, InfernoHot, 0.22f)?.Configure(12, 0.85f);
                for (int i = 0; i < 12; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(at,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 7f),
                        Main.rand.NextBool(3) ? InfernoBright : InfernoHot,
                        Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(14, 24));
                }
            }
            parrySeen = parry;
        }
    }

    /// <summary>
    /// 狱火印手持：三拍骑士连段（横斩/返斩/军团重劈）。ai[0]=拍号 ai[1]=交替符号
    /// ai[2]=狱火旗（本斩带火焰刃波与刃焰演出，随生成包过线）。
    /// 格挡结算重接：原版 Player.cs 40483 在物品直击里做
    /// parryDamageBuff → ScalingBonusDamage += 4f 后清标志清 buff 198，等效搬进 ModifyHitExtra
    /// </summary>
    internal class GsDD2SquireDemonSwordHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.DD2SquireDemonSword;
        protected override Color EdgeBright => GsDD2SquireDemonSword.InfernoBright;
        protected override Color BodyMain => GsDD2SquireDemonSword.InfernoMain;
        protected override Color HotAccent => GsDD2SquireDemonSword.InfernoHot;
        protected override Color DeepShadow => GsDD2SquireDemonSword.InfernoDeep;

        /// <summary>本斩是否燃着狱火（ai[2] 随生成包过线，各端一致）</summary>
        private bool FlameSwing => Projectile.ai[2] > 0.5f;

        private bool waveFired;
        private bool parrySettled;

        //狱火斩常亮，平时只在终结拍亮
        protected override bool GlowAlways => FlameSwing || IsFinisher;
        protected override Color SmearInnerColor => FlameSwing ? GsDD2SquireDemonSword.InfernoHot : base.SmearInnerColor;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 6, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.048f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.08f,
            },
            //拍1 返斩
            1 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 7,
                RaiseBack = 1.9f, Follow = 1.02f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 1.05f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.02f,
            },
            //拍2 军团重劈：长举前压
            _ => new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 5, Recover = 10,
                RaiseBack = 2.15f, Follow = 1.2f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.3f, Hitstop = 2, LungeSpeed = 2.6f, SwingPitch = -0.2f,
            },
        };

        /// <summary>狱火斩沿出手向放出火焰刃波（伤 0.45x，点燃敌人）</summary>
        protected override void OnSlashBegin() {
            if (waveFired || !FlameSwing) {
                return;
            }
            waveFired = true;
            Vector2 dir = baseAngle.ToRotationVector2();
            int waveDamage = Math.Max(1, (int)(Projectile.damage * 0.45f));
            SpawnOwnedProj(ModContent.ProjectileType<GsDD2SquireDemonSwordFireWaveProj>(),
                Hand + dir * (FullReach * 0.7f), dir * 13f, waveDamage, Projectile.knockBack * 0.4f, swingDir);
        }

        /// <summary>
        /// 原版格挡结算重接：parryDamageBuff 为 public 字段，直接等效执行
        /// （+4f ScalingBonusDamage，清标志，清 buff 198）
        /// </summary>
        protected override void ModifyHitExtra(NPC target, ref NPC.HitModifiers modifiers) {
            if (!Owner.parryDamageBuff) {
                return;
            }
            modifiers.ScalingBonusDamage += 4f;
            Owner.parryDamageBuff = false;
            Owner.ClearBuff(BuffID.ParryDamageBuff);
            parrySettled = true;
        }

        /// <summary>格挡结算命中的一记重响与爆火（结算在 ModifyHitExtra，这里只管演出）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!parrySettled) {
                return;
            }
            parrySettled = false;
            SetFlash(8);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = 0.55f, Pitch = -0.1f }, target.Center);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f),
                        Main.rand.NextBool() ? GsDD2SquireDemonSword.InfernoHot : GsDD2SquireDemonSword.InfernoBright,
                        Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(14, 24));
                }
            }
        }

        /// <summary>平时挥砍甩余烬（重力火星），狱火斩追加刃焰舔舐</summary>
        protected override void HandleParticles(int phase) {
            if (phase == PhaseSlash) {
                //余烬：沿切线甩出带重力的火星，狱火斩加量
                Vector2 sweepVel = (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2();
                int count = FlameSwing ? 3 : (IsFinisher ? 2 : 1);
                for (int i = 0; i < count; i++) {
                    Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.55f, 1.0f));
                    Color c = Main.rand.NextBool(3) ? GsDD2SquireDemonSword.InfernoHot : GsDD2SquireDemonSword.InfernoMain;
                    PRTLoader.NewParticle<PRT_Spark>(at, sweepVel * Main.rand.NextFloat(3f, 6.5f) + Vector2.UnitY * 0.5f,
                        c, Main.rand.NextFloat(0.32f, 0.55f))?.Configure(true, Main.rand.Next(14, 22));
                }
            }
            if (FlameSwing && phase <= PhaseSlash && Main.rand.NextBool(2)) {
                //刃焰：狱火沿刃身上舔
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.35f, 0.95f));
                PRTLoader.NewParticle<PRT_Light>(at, -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.3f),
                    GsDD2SquireDemonSword.InfernoHot, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(9, 0.7f);
            }
        }

        /// <summary>狱火斩刃焰光斑（读 ai[2]，各端都画）+ 狱火余量刻记（只画给 owner）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            //狱火斩：刃身三段焰斑呼吸（确定性抖动，禁 Main.rand）
            if (FlameSwing) {
                Vector2 hand = Hand;
                for (int i = 0; i < 3; i++) {
                    float k = 0.35f + 0.25f * i;
                    float flick = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * (9f + i * 2.3f) + DrawRand01(i) * 6.28f);
                    Vector2 at = hand + mainAngle.ToRotationVector2() * (mainReach * k) - Main.screenPosition;
                    Color c = GsDD2SquireDemonSword.InfernoHot * (0.30f * flick * MathF.Max(fanFade, 0.4f));
                    c.A = 0;
                    sb.Draw(glow, at, null, c, 0f, glow.Size() * 0.5f, 0.30f + 0.08f * flick, SpriteEffects.None, 0f);
                }
            }
            //狱火余量：owner 侧近格处的小火记
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsDD2SquireDemonSword scheme =
                GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsDD2SquireDemonSword : null;
            int left = scheme?.FlameSwings ?? 0;
            if (left <= 0 || fanFade <= 0.05f) {
                return;
            }
            Vector2 hand2 = Hand;
            for (int i = 0; i < left; i++) {
                Vector2 at = hand2 + mainAngle.ToRotationVector2() * (mainReach * (0.24f + 0.10f * i)) - Main.screenPosition;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 7.5f + i * 1.6f);
                Color c = GsDD2SquireDemonSword.InfernoBright * (0.5f * fanFade * pulse);
                c.A = 0;
                sb.Draw(glow, at, null, c, 0f, glow.Size() * 0.5f, 0.11f, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 狱火刃波：小型火焰月牙，出膛快后缓（13 → 约 5），沿途洒余烬，命中点燃（OnFire 4 秒）。
    /// ai[0]=弯向符号
    /// </summary>
    internal class GsDD2SquireDemonSwordFireWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float BendSign => Projectile.ai[0] >= 0f ? 1f : -1f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 36;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item20 with { Volume = 0.45f, Pitch = 0.15f }, Projectile.Center);
            }
            //出膛快后缓
            if (Projectile.velocity.Length() > 5f) {
                Projectile.velocity *= 0.94f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsDD2SquireDemonSword.InfernoMain.ToVector3() * 0.35f);
            if (!VaultUtils.isServer && Main.rand.NextBool(2)) {
                //余烬拖尾坠落
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Projectile.velocity * 0.1f + new Vector2(0f, Main.rand.NextFloat(0.2f, 0.8f)),
                    Main.rand.NextBool(3) ? GsDD2SquireDemonSword.InfernoHot : GsDD2SquireDemonSword.InfernoMain,
                    Main.rand.NextFloat(0.25f, 0.45f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        public override bool? CanDamage() => Life >= 1f ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.OnFire, 240);
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    GsDD2SquireDemonSword.InfernoHot, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //焰散：火尘缓浮
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.8f),
                    GsDD2SquireDemonSword.InfernoMain, Main.rand.NextFloat(0.04f, 0.08f))?.Configure(10, 0.6f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (smear == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation + BendSign * 0.3f;
            float grow = Life <= 2f ? Life / 2f : 1f;
            float fade = MathHelper.Clamp(Projectile.timeLeft / 9f, 0f, 1f) * grow;
            float speed01 = MathHelper.Clamp(Projectile.velocity.Length() / 13f, 0f, 1f);
            //速度拉伸的小月牙
            Vector2 size = new(0.30f + 0.14f * speed01, 0.13f);

            //残弧
            for (int i = 1; i <= 2; i++) {
                Vector2 back = center - Projectile.velocity * (i * 1.8f);
                Color trail = GsDD2SquireDemonSword.InfernoMain * (0.14f * (1f - i / 3f) * fade);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, back, null, trail, rot, smear.Size() * 0.5f, size * (1f - i * 0.12f), SpriteEffects.None, 0);
            }
            //焰体 + 亮缘 + 热芯
            Color body = GsDD2SquireDemonSword.InfernoMain * (0.55f * fade);
            body.A = 0;
            Main.EntitySpriteDraw(smear, center, null, body, rot, smear.Size() * 0.5f, size, SpriteEffects.None, 0);
            Color edge = GsDD2SquireDemonSword.InfernoBright * (0.6f * fade);
            edge.A = 0;
            Main.EntitySpriteDraw(smear, center + Projectile.velocity.SafeNormalize(Vector2.Zero) * 4f, null, edge, rot,
                smear.Size() * 0.5f, new Vector2(size.X * 0.8f, size.Y * 0.5f), SpriteEffects.None, 0);
            Color core = GsDD2SquireDemonSword.InfernoHot * (0.4f * fade);
            core.A = 0;
            Main.EntitySpriteDraw(glow, center, null, core, 0f, glow.Size() * 0.5f, 0.2f, SpriteEffects.None, 0);
            return false;
        }
    }
}
