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
    /// 【劫掠连舞·鎏金弯刀】材质：海盗船长的鎏金弯刀，海风与赃金养出的快剑。
    /// 签名：①四拍流水快斩，拍拍音调攀升，连段命中攒「风头」（金色刻光挂在刀身）
    /// ②攒足风头后第四拍变旋风双弧斩：绕身整两周、判定两轮、身周环起海风气旋
    /// ③命中溅金点与海沫，攒层伴金币脆响
    /// </summary>
    internal class GsCutlass : GsBroadswordScheme
    {
        public override int TargetItemID => ItemID.Cutlass;

        protected override int HeldProjID => ModContent.ProjectileType<GsCutlassHeld>();

        protected override int ComboBeats => 4;

        protected override string GsDescFallback =>
            "Reforged: a four-beat plunder dance with rising tempo; land hits to build Panache, " +
            "and with enough swagger the fourth beat becomes a whirling double-arc slash that strikes twice all around";

        //海盗鎏金色板
        internal static readonly Color GoldBright = new(255, 244, 208); //鎏金刃缘
        internal static readonly Color GoldMain = new(232, 186, 96);    //赃金体色
        internal static readonly Color SeaSpray = new(72, 214, 225);    //海沫青
        internal static readonly Color BrigDeep = new(30, 22, 12);      //船舱暗棕

        /// <summary>风头层数（0~3）；跨玩家共享单例，只在 myPlayer 守门路径读写</summary>
        internal int Panache;

        //底伤不加成：四拍 0.9/0.9/0.9/1.1x 按 61 帧循环摊算约原版 100%；
        //攒足风头（前三拍命中 ≥2 记）后第四拍换旋风双弧斩（0.95x×两轮判定、29 帧、绕身 360°），
        //循环 70 帧摊算约 105%，多出的部分拿连段命中换，且旋风是全向 AoE 收益
        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) { }

        /// <summary>攒足风头的第四拍换旋风：交替符号幅值置 2 当旗标（基类只读符号位）</summary>
        protected override void ModifyLocalSwing(Item item, Player player, ref int beat, ref float swingSign) {
            if (beat == 3 && Panache >= 2) {
                Panache = 0;
                swingSign *= 2f;
            }
        }

        public override void GsHoldItem(Item item, Player player) {
            base.GsHoldItem(item, player);
            //断手回拍：风头散场
            if (player.whoAmI == Main.myPlayer && comboResetTimer == 0 && Panache > 0) {
                Panache = 0;
            }
        }
    }

    /// <summary>
    /// 劫掠连舞手持：四拍流水快斩，0/1/2 短促轻斩音调渐升，3 收官斩；
    /// 风头攒够时拍 3 变旋风双弧斩（UpdateBladeTransform 整替为绕身两周，
    /// 半程重置命中冷却做第二轮判定）。ai[0]=拍号 ai[1]=交替符号（|值|>1.5 = 旋风旗标）
    /// </summary>
    internal class GsCutlassHeld : GsBroadswordHeldBase
    {
        protected override int SwordItemID => ItemID.Cutlass;
        protected override int BeatCount => 4;
        protected override Color EdgeBright => GsCutlass.GoldBright;
        protected override Color BodyMain => GsCutlass.GoldMain;
        protected override Color HotAccent => GsCutlass.SeaSpray;
        protected override Color DeepShadow => GsCutlass.BrigDeep;

        //快剑触距略短
        protected override float BaseReach => 112f;

        /// <summary>本拍是否旋风双弧斩（旗标随 ai[1] 幅值过线，各端一致）</summary>
        private bool Whirl => ComboStage == BeatCount - 1 && MathF.Abs(Projectile.ai[1]) > 1.5f;

        /// <summary>第二轮判定已开启</summary>
        private bool secondPass;
        /// <summary>一拍只攒一次风头</summary>
        private bool panacheGained;

        private GsCutlass Scheme =>
            GodSmithScheme.TryGetScheme(SwordItemID, out GodSmithScheme s) ? s as GsCutlass : null;

        //旋风期残影铺满
        protected override int GhostCount => Whirl ? 5 : (IsFinisher ? 3 : 2);
        protected override float GhostSpacing => Whirl ? 0.34f : (IsFinisher ? 0.22f : 0.17f);

        /// <summary>四拍流水：三记短促快斩音调渐升，第四拍收官（或旋风）</summary>
        protected override GsBroadBeat GetBeat(int stage) => stage switch {
            //拍0 顺手快斩
            0 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.6f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.035f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.0f,
            },
            //拍1 返手快斩
            1 => new GsBroadBeat {
                Raise = 4, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.65f, Follow = 0.95f, ReachScale = 1f, LeanAmp = 0.04f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 0f, SwingPitch = 0.1f,
            },
            //拍2 踏步疾斩：最短促，小前压
            2 => new GsBroadBeat {
                Raise = 3, Hold = 1, Slash = 3, Recover = 6,
                RaiseBack = 1.55f, Follow = 0.9f, ReachScale = 0.98f, LeanAmp = 0.04f,
                DamageMult = 0.9f, Hitstop = 1, LungeSpeed = 1.2f, SwingPitch = 0.2f,
            },
            //拍3 收官斩 / 旋风双弧斩
            _ => Whirl
                ? new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 12, Recover = 9,
                    RaiseBack = 2.0f, Follow = 1.2f, ReachScale = 1.1f, LeanAmp = 0.07f,
                    DamageMult = 0.95f, Hitstop = 2, LungeSpeed = 2.2f, SwingPitch = 0.3f,
                }
                : new GsBroadBeat {
                    Raise = 6, Hold = 2, Slash = 4, Recover = 8,
                    RaiseBack = 2.0f, Follow = 1.15f, ReachScale = 1.08f, LeanAmp = 0.06f,
                    DamageMult = 1.1f, Hitstop = 2, LungeSpeed = 0f, SwingPitch = 0.28f,
                },
        };

        /// <summary>旋风双弧：斩切期整替为绕身两周旋转，慢起快中缓收</summary>
        protected override void UpdateBladeTransform(int phase) {
            if (!Whirl || phase != PhaseSlash) {
                base.UpdateBladeTransform(phase);
                return;
            }
            float p = (timer - raiseDur - holdDur) / (float)slashDur;
            slashProgress = p;
            float spin = SmoothStep01(p) * MathHelper.TwoPi * 2f;
            mainAngle = ArcStart + (swingDir * spin);
            mainReach = FullReach * (0.9f + 0.1f * MathF.Sin(MathHelper.Clamp(p, 0f, 1f) * MathHelper.Pi));
            mainTip = Hand + (mainAngle.ToRotationVector2() * mainReach);
        }

        /// <summary>旋风半程（第一周转完）：重置命中冷却，开第二轮判定</summary>
        protected override void HandlePhaseEvents(int phase) {
            base.HandlePhaseEvents(phase);
            if (!Whirl || phase != PhaseSlash || secondPass || slashProgress < 0.5f) {
                return;
            }
            secondPass = true;
            for (int i = 0; i < Main.maxNPCs; i++) {
                Projectile.localNPCImmunity[i] = 0;
            }
            SetFlash(4);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.4f, Pitch = 0.25f }, Owner.Center);
            }
        }

        //==================== 劫掠演出 ====================

        protected override void PlaySwingSound() {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.8f, Pitch = Beat.SwingPitch }, Owner.Center);
            if (Whirl) {
                //旋风起手的海风啸声
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.45f, Pitch = 0.1f }, Owner.Center);
            }
        }

        /// <summary>前三拍命中攒风头（一拍一层，金币脆响逐层升调）</summary>
        protected override void OnHitTarget(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Owner.whoAmI != Main.myPlayer || panacheGained || ComboStage >= 3) {
                return;
            }
            panacheGained = true;
            GsCutlass scheme = Scheme;
            if (scheme == null) {
                return;
            }
            int old = scheme.Panache;
            scheme.Panache = Math.Min(3, scheme.Panache + 1);
            if (!VaultUtils.isServer && scheme.Panache > old) {
                SoundEngine.PlaySound(SoundID.CoinPickup with { Volume = 0.5f, Pitch = 0.05f + 0.18f * scheme.Panache }, Owner.Center);
            }
            if (old < 2 && scheme.Panache >= 2) {
                //够本钱耍旋风了：刃身闪一记
                SetFlash(5);
            }
        }

        protected override void HandleParticles(int phase) {
            base.HandleParticles(phase);
            if (phase != PhaseSlash) {
                return;
            }
            //海沫沿挥弧洒出，旋风期加密
            if (Main.rand.NextBool(Whirl ? 1 : 3)) {
                Vector2 at = Vector2.Lerp(Hand, mainTip, Main.rand.NextFloat(0.6f, 1f));
                PRTLoader.NewParticle<PRT_Light>(at,
                    (mainAngle + (swingDir * MathHelper.PiOver2)).ToRotationVector2() * Main.rand.NextFloat(1.5f, 3.5f),
                    GsCutlass.SeaSpray, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(8, 0.55f);
            }
        }

        protected override void OnHitFX(NPC target, NPC.HitInfo hit, int damageDone) {
            base.OnHitFX(target, hit, damageDone);
            //劫掠快意：金点上蹦、海沫横溅
            int motes = Whirl ? 5 : 2;
            for (int i = 0; i < motes; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-4f, -1.5f)),
                    Main.rand.NextBool() ? GsCutlass.GoldBright : GsCutlass.SeaSpray,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        /// <summary>旋风气旋（全端，旋态随 ai 过线）+ 风头刻光（owner 侧，层数不共享）</summary>
        protected override void DrawExtra(SpriteBatch sb, Color lightColor) {
            Texture2D air = CWRAsset.Airflow?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (air == null || star == null) {
                return;
            }

            //旋风期：身周环起海风气旋，起收两头收口
            if (Whirl && CurrentPhase == PhaseSlash) {
                Vector2 anchor = Hand - Main.screenPosition;
                float ring = MathF.Sin(MathHelper.Clamp(slashProgress, 0f, 1f) * MathHelper.Pi);
                for (int i = 0; i < 2; i++) {
                    Color c = (i == 0 ? GsCutlass.SeaSpray : GsCutlass.GoldBright) * (0.28f * ring);
                    c.A = 0;
                    sb.Draw(air, anchor, null, c, mainAngle + (i * MathHelper.Pi), air.Size() * 0.5f,
                        new Vector2(mainReach * 2f / air.Width, 0.42f + 0.14f * i), SpriteEffects.None, 0f);
                }
            }

            //风头刻光：沿刀身排出金色亮点
            if (Owner.whoAmI != Main.myPlayer) {
                return;
            }
            int stacks = Scheme?.Panache ?? 0;
            if (stacks <= 0 || fanFade <= 0.05f) {
                return;
            }
            for (int i = 0; i < stacks; i++) {
                Vector2 at = Hand + (mainAngle.ToRotationVector2() * (mainReach * (0.22f + 0.1f * i))) - Main.screenPosition;
                float pulse = 0.7f + 0.3f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + i * 1.5f);
                Color c = GsCutlass.GoldBright * (0.55f * fanFade * pulse);
                c.A = 0;
                sb.Draw(star, at, null, c, 0f, star.Size() * 0.5f, 0.13f, SpriteEffects.None, 0f);
            }
        }
    }
}
