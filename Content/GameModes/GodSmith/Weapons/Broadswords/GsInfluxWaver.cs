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
    /// 【湍流波刃·相位标记】材质：火星科技相位钢，刃身永远拖着一层超前半拍的荧青相位残像。
    /// 签名：①每斩放出相位光波，命中后波体消隐、在目标另一侧闪现回斩共 2 次（瞬移闪线+残像刀形）
    /// ②闪现回斩会给目标烙下环绕相位纹标记（驻场自绘）
    /// ③终结过载斩对全部烙印目标各引一道追加相位斩（上限 3 道），全程电弧荧青反馈
    /// </summary>
    internal class GsInfluxWaver : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.InfluxWaver;

        protected override int HeldProjID => ModContent.ProjectileType<GsInfluxWaverHeld>();

        protected override int ComboBeats => 3;

        protected override int ComboResetFrames => 58;

        protected override string GsDescFallback =>
            "Reforged: Martian phase-steel; every slash casts a phase wave that blinks to the far side " +
            "of its victim and strikes back twice, blink strikes brand the target with a phase mark, " +
            "and the third overdrive slash calls an extra phase blade on every branded foe, up to three";

        //相位钢色板
        internal static readonly Color PhaseBright = new(150, 240, 255); //荧青刃缘
        internal static readonly Color PhaseMain = new(74, 120, 176);    //相位钢蓝体色
        internal static readonly Color PhaseHot = new(64, 255, 228);     //电荧青强调
        internal static readonly Color PhaseDeep = new(12, 22, 40);      //深空钢影

        //底伤 +10%（重铸追斩链单段 0.85x 低于原版 1.0x×3 段，底伤补回）：近战拍均约 1.12x +
        //每斩相位波 0.85x 且命中后闪现回斩 2 次（同价，单波链上限 3 段）+ 终结拍对烙印目标各引 0.7x 相位斩（上限 3），
        //按三拍循环约 61 帧对照原版（近战 1.0x + 光波 1.0x×3 段追斩每 20 帧）摊算，综合单体 DPS 约原版 103%~110%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.1f;
    }

    /// <summary>
    /// 相位钢手持：三拍连段。0 横斩 / 1 返斩 / 2 过载终结（长举锁定烙印目标、前压重劈、引相位斩）。
    /// 斩切期刃身带超前半拍的荧青相位残像；过载蓄力时画锁定线扫向烙印目标。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsInfluxWaverHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.InfluxWaver;
        protected override float BaseReach => 122f;
        protected override Color EdgeBright => GsInfluxWaver.PhaseBright;
        protected override Color BodyMain => GsInfluxWaver.PhaseMain;
        protected override Color HotAccent => GsInfluxWaver.PhaseHot;
        protected override Color DeepShadow => GsInfluxWaver.PhaseDeep;

        //相位钢冷光：刀身压向钢蓝，辉光常亮荧青
        protected override Color BodyTint(Color lightColor) => Color.Lerp(lightColor, GsInfluxWaver.PhaseMain, 0.22f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor => IsFinisher ? GsInfluxWaver.PhaseHot : GsInfluxWaver.PhaseBright;

        private bool waveFired;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 横斩
            0 => new GsBroadBeat {
                Raise = 5, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.85f, Follow = 1.0f, ReachScale = 1f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍1 返斩：短举快接
            1 => new GsBroadBeat {
                Raise = 4, Hold = 2, Slash = 4, Recover = 8,
                RaiseBack = 1.9f, Follow = 1.05f, ReachScale = 1.02f, LeanAmp = 0.05f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.13f,
            },
            //拍2 过载：长举锁定、滞谷读秒、前压重劈
            _ => new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 4, Recover = 10,
                RaiseBack = 2.2f, Follow = 1.25f, ReachScale = 1.12f, LeanAmp = 0.08f,
                DamageMult = 1.35f, Hitstop = 2, LungeSpeed = 2.8f, SwingPitch = -0.28f,
            },
        };

        //==================== 相位演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            //相位钢出鞘的电子副音
            SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.2f, Pitch = 0.35f + Beat.SwingPitch }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = -0.4f }, Owner.Center);
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.32f, Pitch = -0.3f }, Owner.Center);
            }
        }

        /// <summary>每拍放出相位波；过载拍另对烙印目标各引一道相位斩（上限 3，随引随销）</summary>
        protected override void OnSlashBegin() {
            if (waveFired) {
                return;
            }
            waveFired = true;
            if (IsFinisher) {
                SetFlash(7);
            }
            int baseDamage = Math.Max(1, (int)(Projectile.damage / Beat.DamageMult));
            Vector2 dir = baseAngle.ToRotationVector2();
            SpawnOwnedProj(ModContent.ProjectileType<GsInfluxWaverWaveProj>(),
                Hand + dir * (FullReach * 0.85f), dir * 8f,
                Math.Max(1, (int)(baseDamage * 0.85f)), Projectile.knockBack * 0.5f, 2f, swingDir);

            if (!IsFinisher || Projectile.owner != Main.myPlayer) {
                return;
            }
            //过载引斩：逐个点名烙印目标
            int brandType = ModContent.ProjectileType<GsInfluxWaverBrandProj>();
            int called = 0;
            for (int i = 0; i < Main.maxProjectiles && called < 3; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != brandType || p.owner != Projectile.owner) {
                    continue;
                }
                int npcIdx = (int)p.ai[0];
                if (npcIdx < 0 || npcIdx >= Main.maxNPCs || !Main.npc[npcIdx].active) {
                    p.Kill();
                    continue;
                }
                SpawnOwnedProj(ModContent.ProjectileType<GsInfluxWaverPhaseSlashProj>(),
                    Main.npc[npcIdx].Center, Vector2.Zero,
                    Math.Max(1, (int)(baseDamage * 0.7f)), Projectile.knockBack * 0.4f,
                    npcIdx, called % 2 == 0 ? 1f : -1f);
                p.Kill();
                called++;
            }
            if (called > 0) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = 0.15f }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            //过载蓄能：电弧沿刃身乱跳，滞谷期荧青光尘汇入
            if (timer % 2 == 0) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.35f, 1f));
                Dust d = Dust.NewDustPerfect(at, DustID.Electric,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.5f, 1.8f), 100, default,
                    Main.rand.NextFloat(0.5f, 0.9f));
                d.noGravity = true;
            }
            if (phase == PhaseHold) {
                Vector2 from = Hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(40f, 70f);
                PRTLoader.NewParticle<PRT_Light>(from, (Vector2.Lerp(Hand, mainTip, 0.6f) - from) * 0.18f,
                    GsInfluxWaver.PhaseHot, Main.rand.NextFloat(0.06f, 0.1f))?.Configure(8, 0.6f);
            }
        }

        /// <summary>命中反馈：电弧尘 + 荧青高频电音，过载拍加量</summary>
        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            int arcs = IsFinisher ? 6 : 3;
            for (int i = 0; i < arcs; i++) {
                Dust d = Dust.NewDustPerfect(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    DustID.Electric, Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f), 80, default,
                    Main.rand.NextFloat(0.6f, 1.1f));
                d.noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.16f, Pitch = 0.55f, MaxInstances = 3 }, target.Center);
        }

        /// <summary>超前相位残像（斩切期，刃的「未来半拍」）+ 过载蓄力锁定线（标记是同步弹幕，全端可见）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            //斩切期：荧青残像走在真刃前面
            if (CurrentPhase == PhaseSlash && slashProgress > 0.05f && slashProgress < 0.95f) {
                Main.instance.LoadItem(SwordItemID);
                Texture2D tex = TextureAssets.Item[SwordItemID].Value;
                GetBladeDrawOrientation(out SpriteEffects effect, out float rotOffset);
                float scale = mainReach * (BladeTipFill - BladePark) * 2f
                    / MathF.Max(new Vector2(tex.Width, tex.Height).Length(), 1f);
                float aheadAngle = mainAngle + swingDir * 0.14f;
                Vector2 at = Hand + aheadAngle.ToRotationVector2() * (mainReach * BladePark) - Main.screenPosition;
                Color ghost = GsInfluxWaver.PhaseHot * 0.30f;
                ghost.A = 0;
                sb.Draw(tex, at, null, ghost, aheadAngle + rotOffset, tex.Size() / 2f, scale, effect, 0f);
            }

            //过载蓄力：锁定线扫向烙印目标
            if (!IsFinisher || CurrentPhase > PhaseHold) {
                return;
            }
            Texture2D line = CWRAsset.Airflow?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (line == null || glow == null) {
                return;
            }
            float reveal = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
            int brandType = ModContent.ProjectileType<GsInfluxWaverBrandProj>();
            int drawn = 0;
            for (int i = 0; i < Main.maxProjectiles && drawn < 3; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.type != brandType || p.owner != Projectile.owner) {
                    continue;
                }
                int npcIdx = (int)p.ai[0];
                if (npcIdx < 0 || npcIdx >= Main.maxNPCs || !Main.npc[npcIdx].active) {
                    continue;
                }
                Vector2 to = Main.npc[npcIdx].Center;
                Vector2 delta = to - mainTip;
                float len = delta.Length();
                if (len < 8f) {
                    continue;
                }
                float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f + DrawRand01(i) * 6.28f);
                Color c = GsInfluxWaver.PhaseHot * (0.32f * reveal * pulse);
                c.A = 0;
                sb.Draw(line, mainTip - Main.screenPosition, null, c, delta.ToRotation(),
                    new Vector2(0f, line.Height * 0.5f), new Vector2(len / line.Width, 0.05f), SpriteEffects.None, 0f);
                //目标端锁定点
                Color dot = GsInfluxWaver.PhaseBright * (0.5f * reveal * pulse);
                dot.A = 0;
                sb.Draw(glow, to - Main.screenPosition, null, dot, 0f, glow.Size() / 2f, 0.34f, SpriteEffects.None, 0f);
                drawn++;
            }
        }
    }

    /// <summary>
    /// 相位光波：每一斩放出的荧青波刃。出膛 8→约 17 加速后缓；命中后波体消隐、
    /// 闪现到目标另一侧短驻回瞄（慢速段无伤害），再以 21 速回斩，共追斩 2 次；
    /// 闪现回斩命中烙下相位标记。闪现闪线由各端从位置突变自行推断绘制，不走额外网络。
    /// ai[0]=剩余追斩数 ai[1]=挥动符号
    /// </summary>
    internal class GsInfluxWaverWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private ref float StrikesLeft => ref Projectile.ai[0];
        private float SwingSign => Projectile.ai[1] >= 0f ? 1f : -1f;
        private ref float Life => ref Projectile.localAI[0];

        //owner 端追斩驻留读秒；远端只按速度快慢渲染
        private int windTimer;
        //各端本地推断的闪现闪线（不过线）
        private Vector2 blinkFrom, blinkTo;
        private int blinkTimer;
        private Vector2 lastSeenCenter;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 50;
            Projectile.friendly = true;
            Projectile.tileCollide = false;//相位钢穿墙，原版光波亦然
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 6;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
            Projectile.timeLeft = 90;
        }

        public override void AI() {
            Life++;
            //闪现推断：中心一帧内突跳即认定相位闪现，记闪线并放材质化反馈
            if (lastSeenCenter != Vector2.Zero && Vector2.Distance(Projectile.Center, lastSeenCenter) > 60f) {
                blinkFrom = lastSeenCenter;
                blinkTo = Projectile.Center;
                blinkTimer = 10;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
                    for (int i = 0; i < 5; i++) {
                        Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric,
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f), 80, default,
                            Main.rand.NextFloat(0.5f, 0.9f));
                        d.noGravity = true;
                    }
                }
            }
            lastSeenCenter = Projectile.Center;
            if (blinkTimer > 0) {
                blinkTimer--;
            }

            //owner 端：材质化读秒结束即回斩加速
            if (Projectile.owner == Main.myPlayer && windTimer > 0 && --windTimer == 0) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 21f;
                Projectile.netUpdate = true;
            }

            float speed = Projectile.velocity.Length();
            if (windTimer <= 0) {
                //航速塑形：出膛冲刺加速，随后缓速滑行（全程不匀速）
                if (speed > 4f && speed < 16f && Life <= 10f) {
                    Projectile.velocity *= 1.10f;
                }
                else if (speed > 10f) {
                    Projectile.velocity *= 0.986f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, GsInfluxWaver.PhaseHot.ToVector3() * 0.4f);
            if (!VaultUtils.isServer && speed > 6f && Main.rand.NextBool(3)) {
                //航迹：荧青光尘后曳
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    -Projectile.velocity * 0.05f, GsInfluxWaver.PhaseMain,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(9, 0.55f);
            }
        }

        //材质化慢速段无伤害：以速度判据，各端一致
        public override bool? CanDamage() => Projectile.velocity.Length() > 7f ? null : false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Projectile.velocity.X >= 0f ? 1 : -1;//击退随斩向

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                //命中荧青迸溅
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                        Main.rand.NextBool() ? GsInfluxWaver.PhaseBright : GsInfluxWaver.PhaseHot,
                        Main.rand.NextFloat(0.3f, 0.55f))?.Configure(true, Main.rand.Next(10, 18));
                }
            }
            if (Projectile.owner != Main.myPlayer) {
                return;
            }

            //本次若是闪现回斩（非首击）则烙相位标记
            if (StrikesLeft < 2f) {
                TryBrand(target);
            }

            if (StrikesLeft > 0f) {
                StrikesLeft--;
                //闪现到目标另一侧，短驻回瞄再回斩
                Vector2 dashDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 lateral = dashDir.RotatedBy(MathHelper.PiOver2)
                    * ((StrikesLeft % 2f == 0f ? 1f : -1f) * 46f);
                Vector2 exit = target.Center + dashDir * (MathF.Max(target.width, target.height) * 0.5f + 92f) + lateral;
                Projectile.Center = exit;
                Projectile.velocity = (target.Center - exit).SafeNormalize(Vector2.UnitX) * 2.2f;
                windTimer = 6;
                Projectile.timeLeft = Math.Max(Projectile.timeLeft, 55);
                Projectile.netUpdate = true;
            }
            else {
                //追斩链尽：波体 14 帧内相位消散
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 14);
                Projectile.netUpdate = true;
            }
        }

        /// <summary>owner 端烙印：同目标只保留一枚（标记是驻场弹幕，随生成包全端可见）</summary>
        private void TryBrand(NPC target) {
            int brandType = ModContent.ProjectileType<GsInfluxWaverBrandProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == brandType && p.owner == Projectile.owner && (int)p.ai[0] == target.whoAmI) {
                    return;
                }
            }
            float radius = MathF.Max(target.width, target.height) * 0.5f + 16f;
            Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero,
                brandType, 0, 0f, Projectile.owner, target.whoAmI, radius);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //相位消散：荧青光珠缓散
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(16f, 16f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.2f),
                    Main.rand.NextBool() ? GsInfluxWaver.PhaseMain : GsInfluxWaver.PhaseHot,
                    Main.rand.NextFloat(0.06f, 0.11f))?.Configure(12, 0.65f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D line = CWRAsset.Airflow?.Value;
            if (smear == null || glow == null || line == null) {
                return false;
            }
            Main.instance.LoadItem(ItemID.InfluxWaver);
            Texture2D blade = TextureAssets.Item[ItemID.InfluxWaver].Value;
            Vector2 screen = Main.screenPosition;
            Vector2 center = Projectile.Center - screen;
            float speed = Projectile.velocity.Length();
            float grow = MathHelper.Clamp(Life / 3f, 0f, 1f);
            float fade = MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            float k = grow * fade;
            float rot = Projectile.rotation;
            Vector2 fwd = rot.ToRotationVector2();

            //闪现闪线：旧位到新位的荧青光轨，两端光点
            if (blinkTimer > 0) {
                float bl = blinkTimer / 10f;
                Vector2 delta = blinkTo - blinkFrom;
                float len = delta.Length();
                if (len > 4f) {
                    Color lc = GsInfluxWaver.PhaseHot * (0.55f * bl);
                    lc.A = 0;
                    Main.EntitySpriteDraw(line, blinkFrom - screen, null, lc, delta.ToRotation(),
                        new Vector2(0f, line.Height * 0.5f), new Vector2(len / line.Width, 0.02f + 0.08f * bl), SpriteEffects.None, 0);
                    for (int e = 0; e < 2; e++) {
                        Vector2 at = (e == 0 ? blinkFrom : blinkTo) - screen;
                        Color ec = GsInfluxWaver.PhaseBright * (0.5f * bl);
                        ec.A = 0;
                        Main.EntitySpriteDraw(glow, at, null, ec, 0f, glow.Size() * 0.5f,
                            0.08f + 0.3f * bl, SpriteEffects.None, 0);
                    }
                }
            }

            //材质化短驻：残像刀形自虚而实，波体不画
            if (speed <= 7f) {
                float solid = 1f - blinkTimer / 10f;
                Color bg = GsInfluxWaver.PhaseHot * (0.25f + 0.35f * solid);
                bg.A = 0;
                Main.EntitySpriteDraw(blade, center, null, bg, rot + MathHelper.PiOver4,
                    blade.Size() * 0.5f, 1.05f, SpriteEffects.None, 0);
                Color ring = GsInfluxWaver.PhaseBright * (0.1f + 0.4f * (1f - solid));
                ring.A = 0;
                Main.EntitySpriteDraw(glow, center, null, ring, 0f, glow.Size() * 0.5f,
                    0.62f - 0.3f * solid, SpriteEffects.None, 0);
                return false;
            }

            //拖尾：旧位残弧
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - screen;
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Color trail = GsInfluxWaver.PhaseMain * (0.15f * t * k);
                trail.A = 0;
                Main.EntitySpriteDraw(smear, at, null, trail, rot + SwingSign * 0.3f,
                    smear.Size() * 0.5f, new Vector2(0.30f, 0.13f) * t, SpriteEffects.None, 0);
            }

            //波身：钢蓝体 + 荧青前缘 + 白青核线
            Color body = GsInfluxWaver.PhaseMain * (0.55f * k);
            body.A = 0;
            Main.EntitySpriteDraw(smear, center, null, body, rot + SwingSign * 0.3f,
                smear.Size() * 0.5f, new Vector2(0.42f, 0.17f), SpriteEffects.None, 0);
            Color edge = GsInfluxWaver.PhaseBright * (0.7f * k);
            edge.A = 0;
            Main.EntitySpriteDraw(smear, center + fwd * 6f, null, edge, rot + SwingSign * 0.25f,
                smear.Size() * 0.5f, new Vector2(0.38f, 0.08f), SpriteEffects.None, 0);
            Color core = new Color(225, 255, 252) * (0.8f * k);
            core.A = 0;
            Main.EntitySpriteDraw(smear, center + fwd * 3f, null, core, rot + SwingSign * 0.25f,
                smear.Size() * 0.5f, new Vector2(0.32f, 0.032f), SpriteEffects.None, 0);

            //回斩冲刺：残像刀形压在波身上，速度越高越实
            if (speed > 15f) {
                float dashK = MathHelper.Clamp((speed - 15f) / 6f, 0f, 1f) * k;
                Color bg = GsInfluxWaver.PhaseHot * (0.4f * dashK);
                bg.A = 0;
                Main.EntitySpriteDraw(blade, center - fwd * 8f, null, bg, rot + MathHelper.PiOver4,
                    blade.Size() * 0.5f, 1.0f, SpriteEffects.None, 0);
            }

            //月牙双角光点
            Vector2 side = (rot + MathHelper.PiOver2).ToRotationVector2() * SwingSign;
            for (int i = -1; i <= 1; i += 2) {
                Color hc = GsInfluxWaver.PhaseBright * (0.42f * k);
                hc.A = 0;
                Main.EntitySpriteDraw(glow, center + side * (i * 19f) - fwd * 4f, null, hc, 0f,
                    glow.Size() * 0.5f, 0.24f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 相位烙印：闪现回斩过的目标携带的环绕标记（驻场自绘，无伤害）。
    /// 三枚荧青弧括号绕目标旋转 + 顶部锁定点；目标消亡或超时即散。
    /// ai[0]=目标 NPC 索引 ai[1]=环绕半径
    /// </summary>
    internal class GsInfluxWaverBrandProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int TargetIndex => (int)Projectile.ai[0];
        private float OrbitRadius => MathF.Max(Projectile.ai[1], 20f);
        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
        }

        public override bool? CanDamage() => false;

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            Life++;
            if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs) {
                Projectile.Kill();
                return;
            }
            NPC target = Main.npc[TargetIndex];
            if (!target.active) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = target.Center;
            Lighting.AddLight(Projectile.Center, GsInfluxWaver.PhaseHot.ToVector3() * 0.12f);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //标记碎散：几粒荧青短火花
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(OrbitRadius * 0.6f, OrbitRadius * 0.6f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                    GsInfluxWaver.PhaseHot, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(8, 14));
            }
        }

        /// <summary>绘制路径确定性伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (smear == null || glow == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            //浮现 8 帧、将逝 20 帧收干
            float reveal = MathHelper.Clamp(Life / 8f, 0f, 1f);
            float dying = MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            float k = reveal * dying;
            float baseAng = Main.GlobalTimeWrappedHourly * 2.4f + SegRand(3) * 6.28f;

            //三枚弧括号贴轨道切线旋转
            for (int i = 0; i < 3; i++) {
                float ang = baseAng + i * (MathHelper.TwoPi / 3f);
                Vector2 at = center + ang.ToRotationVector2() * OrbitRadius;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + i * 2.1f);
                Color arc = GsInfluxWaver.PhaseHot * (0.5f * k * pulse);
                arc.A = 0;
                Main.EntitySpriteDraw(smear, at, null, arc, ang + MathHelper.PiOver2,
                    smear.Size() * 0.5f, new Vector2(0.10f, 0.035f), SpriteEffects.None, 0);
            }
            //顶部锁定点
            Color dot = GsInfluxWaver.PhaseBright * (0.55f * k);
            dot.A = 0;
            Main.EntitySpriteDraw(glow, center - Vector2.UnitY * (OrbitRadius + 8f), null, dot, 0f,
                glow.Size() * 0.5f, 0.16f + 0.04f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 相位引斩：过载斩点名烙印目标的追加斩击。0~5 帧目标侧位材质化（无伤害、残像自虚而实），
    /// 6~13 帧 24 速贯穿目标（伤害窗），此后减速渐隐成余像；目标提前消亡则直接散去。
    /// ai[0]=目标 NPC 索引 ai[1]=切入侧符号
    /// </summary>
    internal class GsInfluxWaverPhaseSlashProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int MaterializeEnd = 5;
        private const int DashEnd = 13;
        private int TargetIndex => (int)Projectile.ai[0];
        private float SideSign => Projectile.ai[1] >= 0f ? 1f : -1f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 46;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 28;
        }

        public override void AI() {
            Life++;
            bool targetAlive = TargetIndex >= 0 && TargetIndex < Main.maxNPCs && Main.npc[TargetIndex].active;

            if (Life <= MaterializeEnd) {
                if (!targetAlive) {
                    //目标没了：直接进入余像散去
                    Projectile.timeLeft = Math.Min(Projectile.timeLeft, 8);
                    Life = DashEnd + 1;
                    return;
                }
                NPC target = Main.npc[TargetIndex];
                //侧位驻停：贴目标另一侧待命，逐帧回瞄（各端从同步 NPC 位置各自推得）
                Vector2 offset = new(SideSign * (target.width * 0.5f + 116f), -18f);
                Projectile.Center = target.Center + offset;
                Projectile.velocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 0.6f;
                if (Life == 1f && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.35f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            else if (Life == MaterializeEnd + 1) {
                //贯穿冲刺：锁向目标当前位置
                Vector2 aim = targetAlive
                    ? (Main.npc[TargetIndex].Center - Projectile.Center).SafeNormalize(Vector2.UnitX)
                    : Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Projectile.velocity = aim * 24f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.3f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            else if (Life > DashEnd) {
                //余像：减速渐隐
                Projectile.velocity *= 0.86f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsInfluxWaver.PhaseHot.ToVector3() * 0.3f);

            if (!VaultUtils.isServer && Life > MaterializeEnd && Life <= DashEnd && Main.rand.NextBool(2)) {
                //冲刺电弧后曳
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                    DustID.Electric, -Projectile.velocity * 0.08f, 90, default, Main.rand.NextFloat(0.5f, 0.8f));
                d.noGravity = true;
            }
        }

        //只有贯穿窗结算伤害
        public override bool? CanDamage() => Life > MaterializeEnd && Life <= DashEnd ? null : false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Projectile.velocity.X >= 0f ? 1 : -1;//击退随冲向

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.2f, Pitch = 0.5f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6f),
                    Main.rand.NextBool() ? GsInfluxWaver.PhaseBright : GsInfluxWaver.PhaseHot,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 16));
            }
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustPerfect(target.Center, DustID.Electric,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 4.5f), 80, default,
                    Main.rand.NextFloat(0.5f, 0.9f));
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D smear = CWRAsset.SemiCircularSmear?.Value;
            if (glow == null || smear == null) {
                return false;
            }
            Main.instance.LoadItem(ItemID.InfluxWaver);
            Texture2D blade = TextureAssets.Item[ItemID.InfluxWaver].Value;
            Vector2 screen = Main.screenPosition;
            Vector2 center = Projectile.Center - screen;
            float rot = Projectile.rotation;

            if (Life <= MaterializeEnd) {
                //材质化：残像刀形自虚而实 + 收拢光环
                float solid = Life / (float)MaterializeEnd;
                Color bg = GsInfluxWaver.PhaseHot * (0.15f + 0.4f * solid);
                bg.A = 0;
                Main.EntitySpriteDraw(blade, center, null, bg, rot + MathHelper.PiOver4,
                    blade.Size() * 0.5f, 1.1f, SpriteEffects.None, 0);
                Color ring = GsInfluxWaver.PhaseBright * (0.45f * (1f - solid));
                ring.A = 0;
                Main.EntitySpriteDraw(glow, center, null, ring, 0f, glow.Size() * 0.5f,
                    0.62f - 0.3f * solid, SpriteEffects.None, 0);
                return false;
            }

            float fade = Life <= DashEnd ? 1f : MathHelper.Clamp(Projectile.timeLeft / 12f, 0f, 1f);
            //冲刺残像：旧位刀形层层渐隐
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 at = Projectile.oldPos[i] + Projectile.Size * 0.5f - screen;
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Color g = GsInfluxWaver.PhaseMain * (0.22f * t * fade);
                g.A = 0;
                Main.EntitySpriteDraw(blade, at, null, g, rot + MathHelper.PiOver4,
                    blade.Size() * 0.5f, 1.0f, SpriteEffects.None, 0);
            }
            //刀锋光带
            Color streak = GsInfluxWaver.PhaseHot * (0.5f * fade);
            streak.A = 0;
            Main.EntitySpriteDraw(smear, center - rot.ToRotationVector2() * 10f, null, streak, rot,
                smear.Size() * 0.5f, new Vector2(0.36f, 0.07f), SpriteEffects.None, 0);
            //本体残像刀形
            Color bc = GsInfluxWaver.PhaseBright * (0.75f * fade);
            bc.A = 0;
            Main.EntitySpriteDraw(blade, center, null, bc, rot + MathHelper.PiOver4,
                blade.Size() * 0.5f, 1.12f, SpriteEffects.None, 0);
            return false;
        }
    }
}
