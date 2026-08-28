using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
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
    /// 【万剑归宗】材质：万剑归一的时空剑阵，手中握的是剑阵之枢。
    /// 签名：①每一斩掷出历史名剑虚影，沿弧线掠向光标处回旋再折返（原版分形剑阵保留重铸）
    /// ②命中积攒剑意，剑意分三阶放大终结拍的剑阵编队（三剑扇 / 五剑扇+小裂隙 / 八剑归宗阵+时空裂隙爆发）
    /// ③枢剑辉光相位流转虹彩，剑意读数以环绕剑星显示，裂隙爆发是全包最重的一记演出
    /// </summary>
    internal class GsZenith : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Zenith;

        protected override int HeldProjID => ModContent.ProjectileType<GsZenithHeld>();

        //归宗仪式续段窗口放宽
        protected override int ComboResetFrames => 70;

        protected override string GsDescFallback =>
            "Reforged: the pivot of ten thousand blades; every slash hurls phantom swords of history " +
            "that arc to your cursor and return, hits build Sword Will, and the third strike unleashes " +
            "a converging blade formation that tears open a spacetime rift at full Will";

        //剑阵之枢色板（枢体偏夜紫，辉光走虹彩相位）
        internal static readonly Color PivotBright = new(214, 238, 255); //星白刃缘
        internal static readonly Color PivotMain = new(132, 112, 232);   //夜紫枢体
        internal static readonly Color PivotHot = new(255, 104, 202);    //裂隙洋红
        internal static readonly Color PivotDeep = new(20, 12, 40);      //时空暗紫

        /// <summary>剑意层数（0~9）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int SwordWill;

        /// <summary>剑意阶位：0~2 无阵 / 3~5 一阶 / 6~8 二阶 / 9 满阶归宗</summary>
        internal static int WillTier(int will) => will >= 9 ? 3 : will / 3;

        //底伤不加成：对照原版每约 10 帧一发全额分形（useTime10、每用必掷），
        //本作快双拍（约16帧）= 刀身 1.0x + 双虚影各 0.5x（贴身常中一枚），终结拍 1.25x + 阵列虚影
        //各 0.5x（3/5/8 枚按阶，多目标才吃满）+ 满阶裂隙 1.2x 每循环一次，
        //单体综合 DPS 约原版 100%~120%，八剑阵与裂隙是 AoE 收益
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }
    }

    /// <summary>
    /// 万剑归宗手持：三拍连段。0/1 交替快斩各掷两枚虚影剑，2 终结重斩按剑意阶位
    /// 展开剑阵（扇阵或八剑归宗阵+裂隙）。剑意攒于方案实例，owner 守门。
    /// ai[0]=拍号 ai[1]=交替符号
    /// </summary>
    internal class GsZenithHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Zenith;
        protected override Color EdgeBright => GsZenith.PivotBright;
        protected override Color BodyMain => GsZenith.PivotMain;
        protected override Color HotAccent => GsZenith.PivotHot;
        protected override Color DeepShadow => GsZenith.PivotDeep;

        /// <summary>虚影剑名册（万剑归一的历史剑，全部原版贴图垫底+虹彩罩）</summary>
        internal static readonly int[] SwordRoster = [
            ItemID.CopperShortsword, ItemID.Starfury, ItemID.EnchantedSword, ItemID.BeeKeeper,
            ItemID.BladeofGrass, ItemID.Muramasa, ItemID.NightsEdge, ItemID.Excalibur,
            ItemID.TrueExcalibur, ItemID.TrueNightsEdge, ItemID.Seedler, ItemID.TheHorsemansBlade,
            ItemID.InfluxWaver, ItemID.StarWrath, ItemID.Meowmere, ItemID.TerraBlade,
        ];

        /// <summary>光标目标离玩家的最大距离</summary>
        private const float TargetRange = 620f;

        private bool volleyFired;
        private bool riftFired;
        private int rosterCursor;

        private GsZenith Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsZenith : null;

        //枢剑吸光，辉光走虹彩相位
        protected override Color BodyTint(Color lightColor) => Color.Lerp(lightColor, GsZenith.PivotDeep, 0.22f);
        protected override bool GlowAlways => true;
        protected override Color GlowColor =>
            Main.hslToRgb((Main.GlobalTimeWrappedHourly * 0.35f + ComboStage * 0.21f) % 1f, 0.85f, 0.66f);
        protected override Color SmearOuterColor => GlowColor;

        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0/1 交替快斩：短举快出，掷影为主
            0 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.7f, Follow = 1.0f, ReachScale = 1.05f, LeanAmp = 0.04f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.05f,
            },
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 7,
                RaiseBack = 1.75f, Follow = 1.05f, ReachScale = 1.05f, LeanAmp = 0.045f,
                DamageMult = 1f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = -0.05f,
            },
            //拍2 归宗重斩：长举展阵、死寂滞谷、前压斩落
            _ => new GsBroadBeat {
                Raise = 7, Hold = 3, Slash = 4, Recover = 11,
                RaiseBack = 2.2f, Follow = 1.3f, ReachScale = 1.15f, LeanAmp = 0.085f,
                DamageMult = 1.25f, Hitstop = 2, LungeSpeed = 2.8f, SwingPitch = -0.25f,
            },
        };

        protected override void SetSwordDefaults() => Projectile.timeLeft = 150;

        //==================== 剑阵生成 ====================

        /// <summary>owner 端取光标目标点（限程）；只在 myPlayer 路径调用</summary>
        private Vector2 AimTarget() {
            Vector2 to = Main.MouseWorld - Owner.Center;
            if (to.Length() > TargetRange) {
                to = to.SafeNormalize(Vector2.UnitX * facingDir) * TargetRange;
            }
            return Owner.Center + to;
        }

        /// <summary>掷一枚虚影剑（owner 端）；converge=true 时自 target 外环切入</summary>
        private void ThrowPhantom(Vector2 target, float damageMul, bool converge, int ringIndex, int ringCount) {
            int sword = SwordRoster[rosterCursor++ % SwordRoster.Length];
            //合围式用负号编码（剑的物品 ID 恒为正）
            float mode = converge ? -sword : sword;
            Vector2 spawn = Owner.Center;
            if (converge) {
                float ang = MathHelper.TwoPi * ringIndex / ringCount;
                spawn = target + ang.ToRotationVector2() * 250f;
            }
            SpawnOwnedProj(ModContent.ProjectileType<GsZenithPhantomProj>(), spawn, Vector2.Zero,
                Math.Max(1, (int)(Projectile.damage * damageMul)), Projectile.knockBack * 0.5f,
                target.X, target.Y, mode);
        }

        protected override void OnSlashBegin() {
            if (volleyFired || Projectile.owner != Main.myPlayer) {
                return;
            }
            volleyFired = true;
            rosterCursor = (int)(Projectile.identity % SwordRoster.Length);
            Vector2 target = AimTarget();
            GsZenith scheme = Scheme;

            if (!IsFinisher) {
                //普通拍：双虚影出鞘
                ThrowPhantom(target, 0.5f, false, 0, 1);
                ThrowPhantom(target, 0.5f, false, 0, 1);
                return;
            }

            //终结拍：按剑意阶位展阵，剑意一次结清
            SetFlash(7);
            int will = scheme?.SwordWill ?? 0;
            int tier = GsZenith.WillTier(will);
            if (scheme != null) {
                scheme.SwordWill = 0;
            }
            switch (tier) {
                case 0:
                    //无阵：三剑扇
                    for (int i = 0; i < 3; i++) {
                        ThrowPhantom(target + new Vector2(0f, (i - 1) * 46f), 0.5f, false, 0, 1);
                    }
                    break;
                case 1:
                    //一阶：五剑扇
                    for (int i = 0; i < 5; i++) {
                        ThrowPhantom(target + new Vector2((i - 2) * 34f, (i - 2) * 30f), 0.5f, false, 0, 1);
                    }
                    break;
                case 2:
                    //二阶：五剑扇 + 小裂隙（0.6x）
                    for (int i = 0; i < 5; i++) {
                        ThrowPhantom(target, 0.5f, true, i, 5);
                    }
                    SpawnOwnedProj(ModContent.ProjectileType<GsZenithRiftProj>(), target, Vector2.Zero,
                        Math.Max(1, (int)(Projectile.damage * 0.6f)), Projectile.knockBack, 0f);
                    break;
                default:
                    //满阶归宗：八剑自八方合围 + 时空裂隙爆发（1.2x）
                    for (int i = 0; i < 8; i++) {
                        ThrowPhantom(target, 0.5f, true, i, 8);
                    }
                    SpawnOwnedProj(ModContent.ProjectileType<GsZenithRiftProj>(), target, Vector2.Zero,
                        Math.Max(1, (int)(Projectile.damage * 1.2f)), Projectile.knockBack, 1f);
                    break;
            }
        }

        /// <summary>命中攒剑意（owner 守门）；攒满一阶有音阶提示</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer || IsFinisher) {
                return;
            }
            GsZenith scheme = Scheme;
            if (scheme == null || scheme.SwordWill >= 9) {
                return;
            }
            int oldTier = GsZenith.WillTier(scheme.SwordWill);
            scheme.SwordWill++;
            int newTier = GsZenith.WillTier(scheme.SwordWill);
            if (newTier > oldTier) {
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.4f, Pitch = 0.1f + 0.18f * newTier }, Owner.Center);
                SetFlash(6);
            }
        }

        //==================== 演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.85f, Pitch = Beat.SwingPitch }, Owner.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.3f, Pitch = IsFinisher ? -0.4f : 0.2f }, Owner.Center);
            if (IsFinisher) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.45f, Pitch = -0.3f }, Owner.Center);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (!IsFinisher || phase > PhaseHold) {
                return;
            }
            //展阵蓄势：虹彩剑尘自四周向枢剑收拢
            Vector2 hand = Hand;
            Vector2 at = hand + Main.rand.NextVector2Unit() * Main.rand.NextFloat(44f, 80f);
            Color c = Main.hslToRgb(Main.rand.NextFloat(), 0.9f, 0.62f);
            PRTLoader.NewParticle<PRT_Light>(at, (Vector2.Lerp(hand, mainTip, 0.6f) - at) * 0.16f, c,
                Main.rand.NextFloat(0.06f, 0.12f))?.Configure(9, 0.6f);
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //虹彩星屑，剑意越高越密
            GsZenith scheme = Scheme;
            int will = Owner.whoAmI == Main.myPlayer ? (scheme?.SwordWill ?? 0) : 0;
            int sparks = 3 + will / 3 * 2 + (IsFinisher ? 3 : 0);
            for (int i = 0; i < sparks; i++) {
                Color c = Main.hslToRgb(Main.rand.NextFloat(), 0.9f, 0.6f);
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 6.5f), c,
                    Main.rand.NextFloat(0.35f, 0.55f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }

        /// <summary>剑意读数：环绕玩家的剑星，一星一层，阶位换色（只画给 owner）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            GsZenith scheme = Scheme;
            int will = scheme?.SwordWill ?? 0;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (star == null) {
                return;
            }

            if (will > 0) {
                int tier = GsZenith.WillTier(will);
                Vector2 center = Owner.MountedCenter - Main.screenPosition;
                for (int i = 0; i < will; i++) {
                    float ang = Main.GlobalTimeWrappedHourly * 1.8f + MathHelper.TwoPi * i / 9f;
                    Vector2 at = center + ang.ToRotationVector2() * new Vector2(34f, 22f);
                    float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + i * 0.9f);
                    Color c = (tier >= 3 ? GsZenith.PivotHot
                        : Main.hslToRgb((i / 9f + Main.GlobalTimeWrappedHourly * 0.2f) % 1f, 0.8f, 0.65f)) * (0.45f * pulse);
                    c.A = 0;
                    sb.Draw(star, at, null, c, 0f, star.Size() * 0.5f, 0.16f, SpriteEffects.None, 0f);
                }
            }

            //终结拍蓄势：光标处先亮出阵位标记，玩家能读到阵要落在哪
            if (IsFinisher && CurrentPhase <= PhaseHold && CWRAsset.SoftGlow?.Value is Texture2D glow) {
                float p = CurrentPhase == PhaseHold ? 1f : MathHelper.Clamp(timer / (float)raiseDur, 0f, 1f);
                Vector2 to = Main.MouseWorld - Owner.Center;
                if (to.Length() > TargetRange) {
                    to = to.SafeNormalize(Vector2.UnitX) * TargetRange;
                }
                Vector2 mark = Owner.Center + to - Main.screenPosition;
                Color mc = GsZenith.PivotHot * (0.25f + 0.2f * p);
                mc.A = 0;
                sb.Draw(glow, mark, null, mc, 0f, glow.Size() * 0.5f, 0.5f + 0.25f * p, SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 虚影剑：历史名剑的时空残象。两种走法：出返式（自玩家掠向目标点、回旋斩、折返消散）
    /// 与合围式（自目标外环切入阵心、过心即散）。全程变速，禁匀速直飞。
    /// ai[0]/ai[1]=目标点 ai[2]=剑种物品 ID（负号 = 合围式）。
    /// 原版剑贴图垫底 + 虹彩相位罩 + 白核 + 残影列，抖动 identity 播种
    /// </summary>
    internal class GsZenithPhantomProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int OutEnd = 13;    //掠出段
        private const int SpinEnd = 21;   //回旋斩段
        private const int BackEnd = 36;   //折返段
        private const int ConvergeEnd = 16;//合围切入段

        private Vector2 TargetPos => new(Projectile.ai[0], Projectile.ai[1]);
        private bool Converge => Projectile.ai[2] < 0f;
        private int SwordID => (int)MathF.Abs(Projectile.ai[2]);
        private ref float Life => ref Projectile.localAI[0];

        private Vector2 origin;
        private bool originSet;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 60;
        }

        /// <summary>identity 播种伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override void AI() {
            if (!originSet) {
                originSet = true;
                origin = Projectile.Center;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.25f, Pitch = 0.3f + SegRand(1) * 0.3f },
                        Projectile.Center);
                }
            }
            Life++;
            Player owner = Main.player[Projectile.owner];
            Vector2 desired;

            if (Converge) {
                //合围式：切入阵心并小幅过冲，过心即散
                if (Life <= ConvergeEnd) {
                    float p = Life / ConvergeEnd;
                    float eased = 1f - MathF.Pow(1f - p, 2.6f);//出生猛、临心缓
                    Vector2 inward = (TargetPos - origin).SafeNormalize(Vector2.UnitX);
                    desired = Vector2.Lerp(origin, TargetPos + inward * 42f, eased);
                }
                else {
                    desired = Projectile.Center + Projectile.velocity * 0.72f;//过心滑出减速
                    if (Life > ConvergeEnd + 8) {
                        Projectile.Kill();
                        return;
                    }
                }
            }
            else if (Life <= OutEnd) {
                //掠出：贝塞尔弧线，出手快临靶缓，弓向按 identity 交替
                float p = Life / OutEnd;
                float eased = 1f - MathF.Pow(1f - p, 2.2f);
                Vector2 mid = Vector2.Lerp(origin, TargetPos, 0.5f);
                float bowSign = SegRand(7) > 0.5f ? 1f : -1f;
                Vector2 ctrl = mid + (TargetPos - origin).SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                    * (bowSign * (70f + 60f * SegRand(3)));
                Vector2 a = Vector2.Lerp(origin, ctrl, eased);
                Vector2 b = Vector2.Lerp(ctrl, TargetPos, eased);
                desired = Vector2.Lerp(a, b, eased);
            }
            else if (Life <= SpinEnd) {
                //回旋斩：绕目标点小半径疾转
                float spinDir = SegRand(11) > 0.5f ? 1f : -1f;
                float ang = (Life - OutEnd) * 0.62f * spinDir + SegRand(13) * MathHelper.TwoPi;
                desired = TargetPos + ang.ToRotationVector2() * 28f;
            }
            else {
                //折返：加速追回持剑者，贴身即散
                float q = MathHelper.Clamp((Life - SpinEnd) / (BackEnd - SpinEnd), 0f, 1f);
                desired = Vector2.Lerp(Projectile.Center, owner.MountedCenter, 0.10f + 0.30f * q * q);
                if (Life > BackEnd || Projectile.Center.Distance(owner.MountedCenter) < 30f) {
                    Projectile.Kill();
                    return;
                }
            }

            Projectile.velocity = desired - Projectile.Center;
            Projectile.rotation = (Projectile.velocity.Length() > 0.5f
                ? Projectile.velocity.ToRotation() : Projectile.rotation - MathHelper.PiOver4) + MathHelper.PiOver4;

            Color hue = PhantomHue(0f);
            Lighting.AddLight(Projectile.Center, hue.ToVector3() * 0.35f);

            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                //航迹星屑
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    -Projectile.velocity * 0.06f, hue, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(9, 0.6f);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (VaultUtils.isServer) {
                return;
            }
            Color hue = PhantomHue(0f);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f),
                    Main.rand.NextBool() ? hue : GsZenith.PivotBright,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //残象散场：几粒虹彩光尘
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.4f, 1.1f),
                    PhantomHue(Main.rand.NextFloat(0.1f)), Main.rand.NextFloat(0.06f, 0.1f))?.Configure(12, 0.6f);
            }
        }

        /// <summary>本剑的虹彩相位色（identity 定相 + 缓慢流转）</summary>
        private Color PhantomHue(float shift)
            => Main.hslToRgb((SegRand(21) + Main.GlobalTimeWrappedHourly * 0.22f + shift) % 1f, 0.88f, 0.63f);

        public override bool PreDraw(ref Color lightColor) {
            int sword = SwordID;
            if (sword <= 0 || sword >= ItemID.Count) {
                sword = ItemID.EnchantedSword;
            }
            Main.instance.LoadItem(sword);
            Texture2D tex = TextureAssets.Item[sword].Value;
            Vector2 orig = tex.Size() * 0.5f;
            float scale = 82f / MathF.Max(tex.Size().Length(), 1f);
            float fade = Converge
                ? MathHelper.Clamp((ConvergeEnd + 8 - Life) / 8f, 0f, 1f)
                : MathHelper.Clamp((BackEnd + 2 - Life) / 10f, 0f, 1f);
            float grow = MathHelper.Clamp(Life / 2f, 0f, 1f);//出生 2 帧撑满
            float k = fade * grow;
            Color hue = PhantomHue(0f);

            //残影列：旧位置的淡彩剑影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Color ghost = PhantomHue(i * 0.045f) * (0.16f * t * k);
                ghost.A = 0;
                Main.EntitySpriteDraw(tex, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                    null, ghost, Projectile.oldRot[i], orig, scale * (0.9f + 0.1f * t), SpriteEffects.None, 0);
            }

            Vector2 center = Projectile.Center - Main.screenPosition;

            //虹彩罩（外圈相位色）
            Color shell = hue * (0.55f * k);
            shell.A = 0;
            Main.EntitySpriteDraw(tex, center, null, shell, Projectile.rotation, orig, scale * 1.08f, SpriteEffects.None, 0);
            //白核（剑形本体，半透明星白）
            Color core = GsZenith.PivotBright * (0.75f * k);
            core.A = 0;
            Main.EntitySpriteDraw(tex, center, null, core, Projectile.rotation, orig, scale * 0.96f, SpriteEffects.None, 0);

            //剑尖光点
            if (CWRAsset.StarGlow01?.Value is Texture2D star) {
                Vector2 tip = center + (Projectile.rotation - MathHelper.PiOver4).ToRotationVector2() * (40f * scale / 0.5f * 0.5f);
                Color tipC = GsZenith.PivotBright * (0.5f * k);
                tipC.A = 0;
                Main.EntitySpriteDraw(star, tip, null, tipC, 0f, star.Size() * 0.5f, 0.2f, SpriteEffects.None, 0);
            }
            return false;
        }
    }

    /// <summary>
    /// 时空裂隙：归宗阵心的引爆。22 帧引信期裂缝渐开（真 alpha 暗缝+虹彩缝缘），
    /// 引爆帧撕裂成星爆并结算一次 AoE，余波光尘上浮。ai[0]=满阶旗（1=八剑归宗，半径与演出加码）
    /// </summary>
    internal class GsZenithRiftProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int Fuse = 22;
        private const int TotalLife = 46;
        private bool FullRite => Projectile.ai[0] > 0.5f;
        private float Radius => FullRite ? 170f : 120f;
        private ref float Life => ref Projectile.localAI[0];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 8;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = TotalLife;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>identity 播种伪随机</summary>
        private float SegRand(int salt) {
            uint h = (uint)(Projectile.identity * 374761393 + salt * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0x1000000;
        }

        public override void AI() {
            Life++;
            if (Life == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
            }
            if (Life == Fuse && !VaultUtils.isServer) {
                //引爆：撕裂重音 + 星屑环喷
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.7f, Pitch = -0.35f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.5f, Pitch = -0.55f }, Projectile.Center);
                int sparks = FullRite ? 22 : 14;
                for (int i = 0; i < sparks; i++) {
                    Color c = Main.hslToRgb(Main.rand.NextFloat(), 0.9f, 0.62f);
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(4f, 9f + (FullRite ? 3f : 0f)), c,
                        Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(16, 26));
                }
                for (int i = 0; i < (FullRite ? 10 : 6); i++) {
                    PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(30f, 30f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.8f, 2.2f),
                        Main.rand.NextBool() ? GsZenith.PivotHot : GsZenith.PivotBright,
                        Main.rand.NextFloat(0.1f, 0.18f))?.Configure(16, 0.8f);
                }
            }
            float glowK = Life < Fuse ? Life / Fuse * 0.5f : 1f - (Life - Fuse) / (float)(TotalLife - Fuse);
            Lighting.AddLight(Projectile.Center, GsZenith.PivotHot.ToVector3() * MathHelper.Clamp(glowK, 0f, 1f));
        }

        //只在引爆窗结算一次
        public override bool? CanDamage() => Life >= Fuse && Life <= Fuse + 5 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => targetHitbox.Distance(Projectile.Center) <= Radius;

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
            => modifiers.HitDirectionOverride = Math.Sign(target.Center.X - Projectile.Center.X);

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blot = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            Texture2D star = CWRAsset.StarTexture?.Value;
            if (blot == null || glow == null || star == null) {
                return false;
            }
            Vector2 center = Projectile.Center - Main.screenPosition;
            float slitAng = SegRand(5) * MathHelper.Pi;

            if (Life < Fuse) {
                //引信期：暗缝渐开，缝缘虹彩渗光
                float p = Life / Fuse;
                float open = p * p;
                Color dark = GsZenith.PivotDeep * (0.75f * open);
                Main.EntitySpriteDraw(blot, center, null, dark, slitAng,
                    blot.Size() * 0.5f, new Vector2(0.42f * open + 0.06f, 0.05f + 0.06f * open), SpriteEffects.None, 0);
                for (int i = -1; i <= 1; i += 2) {
                    Vector2 lip = center + (slitAng + MathHelper.PiOver2).ToRotationVector2() * (i * 6f * open);
                    Color rim = Main.hslToRgb((SegRand(8) + Life * 0.02f) % 1f, 0.85f, 0.62f) * (0.5f * open);
                    rim.A = 0;
                    Main.EntitySpriteDraw(glow, lip, null, rim, 0f, glow.Size() * 0.5f,
                        new Vector2(1.6f * open + 0.2f, 0.35f), SpriteEffects.None, 0);
                }
                return false;
            }

            //爆发期：星爆闪 + 扩张光珠环，随后温和收场
            float q = (Life - Fuse) / (float)(TotalLife - Fuse);
            float fade = 1f - q;
            float ring = Radius * MathF.Min(1.06f * (q / 0.35f), 1f);
            Color flash = GsZenith.PivotBright * (0.85f * fade * fade);
            flash.A = 0;
            Main.EntitySpriteDraw(star, center, null, flash, slitAng, star.Size() * 0.5f,
                (FullRite ? 0.62f : 0.45f) * (0.7f + 0.3f * fade), SpriteEffects.None, 0);
            int beads = FullRite ? 16 : 12;
            for (int i = 0; i < beads; i++) {
                float ang = MathHelper.TwoPi * i / beads + SegRand(i) * 0.35f;
                Color bead = Main.hslToRgb((i / (float)beads + SegRand(40) * 0.3f) % 1f, 0.85f, 0.62f) * (0.5f * fade);
                bead.A = 0;
                Main.EntitySpriteDraw(glow, center + ang.ToRotationVector2() * ring, null, bead, 0f,
                    glow.Size() * 0.5f, 0.26f + 0.1f * SegRand(i + 70), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
