using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Broadswords
{
    /// <summary>
    /// 【湍流波刃·相位标记】材质：火星科技相位钢。
    /// 签名：①每斩放出相位光波，命中后在目标另一侧闪现回斩共 2 次
    /// ②闪现回斩会给目标烙下相位标记（隐形驻场弹幕）
    /// ③终结过载斩对全部烙印目标各引一道追加相位斩（上限 3 道）
    /// </summary>
    internal class GsInfluxWaver : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.InfluxWaver;

        protected override int HeldProjID => ModContent.ProjectileType<GsInfluxWaverHeld>();

        protected override int ComboBeats => 3;

        protected override int ComboResetFrames => 58;

        protected override string GsDescFallback =>
            "Reforged: Martian phase-steel; every slash casts a phase wave that blinks to the far side of its victim and strikes back twice, blink strikes brand the target with a phase mark, and the third overdrive slash calls an extra phase blade on every branded foe, up to three";
        internal static readonly Color PhaseBright = new(150, 240, 255); //荧青刃缘
        internal static readonly Color PhaseMain = new(74, 120, 176);    //相位钢蓝体色
        internal static readonly Color PhaseHot = new(64, 255, 228);     //电荧青强调

        //底伤 +10%（重铸追斩链单段 0.85x 低于原版 1.0x×3 段，底伤补回）：近战拍均约 1.12x +
        //每斩相位波 0.85x 且命中后闪现回斩 2 次（同价，单波链上限 3 段）+ 终结拍对烙印目标各引 0.7x 相位斩（上限 3），
        //按三拍循环约 61 帧对照原版（近战 1.0x + 光波 1.0x×3 段追斩每 20 帧）摊算，综合单体 DPS 约原版 103%~110%
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.1f;
    }

    /// <summary>
    /// 相位钢手持：三拍连段。0 横斩 / 1 返斩 / 2 过载终结（长举锁定烙印目标、前压重劈、引相位斩）。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsInfluxWaverHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.InfluxWaver;
        protected override float BaseReach => 122f;
        protected override Color EdgeBright => GsInfluxWaver.PhaseBright;
        protected override Color BodyMain => GsInfluxWaver.PhaseMain;
        protected override Color HotAccent => GsInfluxWaver.PhaseHot;

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

        /// <summary>命中荧青高频电音</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.16f, Pitch = 0.55f, MaxInstances = 3 }, target.Center);
            }
        }
    }

    /// <summary>
    /// 相位光波：每一斩放出的波刃，用原版湍流波刃弹幕贴图。出膛 8→约 17 加速后缓；命中后
    /// 闪现到目标另一侧短驻回瞄（慢速段无伤害），再以 21 速回斩，共追斩 2 次；
    /// 闪现回斩命中烙下相位标记。闪现音效由各端从位置突变自行推断，不走额外网络。
    /// ai[0]=剩余追斩数 ai[1]=挥动符号
    /// </summary>
    internal class GsInfluxWaverWaveProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InfluxWaver;

        private ref float StrikesLeft => ref Projectile.ai[0];
        private ref float Life => ref Projectile.localAI[0];

        //owner 端追斩驻留读秒；远端只按速度快慢渲染
        private int windTimer;
        //各端本地推断闪现用的上一帧中心
        private Vector2 lastSeenCenter;

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
            //闪现推断：中心一帧内突跳即认定相位闪现，放材质化音效
            if (lastSeenCenter != Vector2.Zero && Vector2.Distance(Projectile.Center, lastSeenCenter) > 60f
                && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
            }
            lastSeenCenter = Projectile.Center;

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
            //原版波刃贴图为斜向刀形，补 45 度
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
        }

        //材质化慢速段无伤害：以速度判据，各端一致
        public override bool? CanDamage() => Projectile.velocity.Length() > 7f ? null : false;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Projectile.velocity.X >= 0f ? 1 : -1;//击退随斩向

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
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
    }

    /// <summary>
    /// 相位烙印：闪现回斩过的目标携带的隐形驻场标记（无伤害、不绘制），
    /// 只供过载斩点名；目标消亡或超时即散。
    /// ai[0]=目标 NPC 索引 ai[1]=环绕半径
    /// </summary>
    internal class GsInfluxWaverBrandProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int TargetIndex => (int)Projectile.ai[0];

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
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    /// <summary>
    /// 相位引斩：过载斩点名烙印目标的追加斩击，用原版湍流波刃弹幕贴图。0~5 帧目标侧位材质化（无伤害），
    /// 6~13 帧 24 速贯穿目标（伤害窗），此后减速成余像；目标提前消亡则直接散去。
    /// ai[0]=目标 NPC 索引 ai[1]=切入侧符号
    /// </summary>
    internal class GsInfluxWaverPhaseSlashProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InfluxWaver;

        private const int MaterializeEnd = 5;
        private const int DashEnd = 13;
        private int TargetIndex => (int)Projectile.ai[0];
        private float SideSign => Projectile.ai[1] >= 0f ? 1f : -1f;
        private ref float Life => ref Projectile.localAI[0];

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
            //原版波刃贴图为斜向刀形，补 45 度
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
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
        }
    }
}
