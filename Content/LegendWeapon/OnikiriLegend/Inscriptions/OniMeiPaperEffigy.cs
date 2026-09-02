using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions
{
    /// <summary>
    /// 紙樋「表影」：把里世界的面影搬到表世界。<br/>
    /// 疾走穿身时在落点挂一张该敌手的纸型；你朝纸型挥刀，伤害传导到真身，
    /// 并让它接下来一段时间挨刀更疼。纸型不会自己打人，它是个靶子，
    /// 打不打、什么时候打，都是玩家的决定。<br/>
    /// ai[0]=真身 whoAmI ai[1]=真身 type ai[2]=基础武器伤害
    /// </summary>
    internal class OniMeiPaperEffigy : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>贴出来的那几帧不能斩，免得疾走自己顺手切了</summary>
        private const int ArmDelay = 14;
        /// <summary>裂开到烧尽的帧数</summary>
        private const int SplitFrames = 22;
        private const float PaperHalfWidth = 30f;
        private const float PaperHalfHeight = 42f;

        private static readonly Color PaperBody = new(226, 214, 196);
        private static readonly Color InkDeep = new(28, 12, 16);

        private int timer;
        private bool cut;
        private int cutTimer;
        private float cutAngle;
        private float swayPhase;
        /// <summary>拓下来的那一帧：源矩形、收进卡面的缩放与朝向，纯本地表现</summary>
        private Rectangle sourceFrame;
        private float sourceScale = 1f;
        private SpriteEffects sourceEffects = SpriteEffects.None;
        private bool appearanceResolved;

        private int SourceId => (int)Projectile.ai[0];
        private int SourceType => (int)Projectile.ai[1];
        private int BaseWeaponDamage => Math.Max(1, (int)Projectile.ai[2]);
        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = (int)(PaperHalfWidth * 2f);
            Projectile.height = (int)(PaperHalfHeight * 2f);
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = OniMeiCombat.PaperEffigyLifeTicks;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>owner 端挂纸；同时在场超额时顶掉最旧的一张</summary>
        internal static bool TryImprint(Player player, NPC source, int baseWeaponDamage,
            IEntitySource entitySource = null) {
            if (player == null || Main.myPlayer != player.whoAmI || source?.active != true) {
                return false;
            }
            NPC root = OniMeiCombat.ResolveEffectRoot(source) ?? source;
            int type = ModContent.ProjectileType<OniMeiPaperEffigy>();
            OniMeiPaperEffigy oldest = null;
            int count = 0;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || proj.type != type
                    || proj.ModProjectile is not OniMeiPaperEffigy effigy) {
                    continue;
                }
                //同一主体只挂一张，不然一次疾走能给同一个敌人贴满
                if (effigy.SourceId == root.whoAmI && !effigy.cut) {
                    return false;
                }
                count++;
                if (oldest == null || proj.timeLeft < oldest.Projectile.timeLeft) {
                    oldest = effigy;
                }
            }
            if (count >= OniMeiCombat.PaperEffigyMaxCount) {
                oldest?.Projectile.Kill();
            }

            Projectile spawned = Projectile.NewProjectileDirect(
                entitySource ?? player.GetSource_Misc("CWR_OniMeiPaperEffigy"),
                root.Center, Vector2.Zero, type, 0, 0f, player.whoAmI,
                ai0: root.whoAmI, ai1: root.type, ai2: Math.Max(1, baseWeaponDamage));
            return spawned.active;
        }

        /// <summary>有纸在场：疾走加价的判据</summary>
        internal static bool AnyOwned(Player player) => player != null
            && player.ownedProjectileCounts[ModContent.ProjectileType<OniMeiPaperEffigy>()] > 0;

        public override void AI() {
            timer++;
            swayPhase += 0.05f;
            if (cut) {
                if (++cutTimer >= SplitFrames) {
                    Projectile.Kill();
                }
                return;
            }
            if (timer >= ArmDelay && Projectile.IsOwnedByLocalPlayer()) {
                DetectBladeSweep();
            }
            Lighting.AddLight(Projectile.Center, new Vector3(0.28f, 0.22f, 0.18f));
        }

        /// <summary>
        /// 纸型不是 NPC，接不到刀的命中回调；改为主动看"本机玩家的刀这一帧扫到我没有"。<br/>
        /// 只认鬼切自己的伤害动作，且必须处在其伤害窗内，挥空的收势不算斩到
        /// </summary>
        private void DetectBladeSweep() {
            Rectangle box = Projectile.Hitbox;
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != Projectile.owner || proj.damage <= 0 || !proj.friendly) {
                    continue;
                }
                if (!IsOnikiriBlade(proj) || proj.ModProjectile.CanDamage() == false) {
                    continue;
                }
                if (!proj.Hitbox.Intersects(box)) {
                    continue;
                }
                Vector2 delta = proj.Center - Projectile.Center;
                Cut(delta.LengthSquared() > 1f
                    ? delta.ToRotation() + MathHelper.PiOver2
                    : MathHelper.PiOver4);
                return;
            }
        }

        private static bool IsOnikiriBlade(Projectile proj) => proj.ModProjectile is CrimsonRendSlash
            or CrimsonSweepSlash
            or CrimsonRendCleave
            or OniZanshinSlashs.OniZanshinSlash
            or OniAnnihilates.OniAnnihilate
            or OniFinaleSlashs.OniFinaleCut;

        /// <summary>斩纸：伤害传导真身并给它挂一段受创；纸自己不结算伤害</summary>
        private void Cut(float angle) {
            cut = true;
            cutTimer = 0;
            cutAngle = angle;
            Projectile.netUpdate = true;

            NPC source = ResolveSource();
            if (source != null && Projectile.IsOwnedByLocalPlayer()) {
                int damage = Math.Max(1, (int)(BaseWeaponDamage * OniMeiCombat.PaperEffigyDamageMul));
                Owner.ApplyDamageToNPC(source, damage, 0f,
                    source.Center.X >= Owner.Center.X ? 1 : -1, false,
                    CWRRef.GetTrueMeleeNoSpeedDamageClass());
                source.AddBuff(ModContent.BuffType<OniPaperBrandDebuff>(),
                    OniMeiCombat.PaperEffigyBrandTicks);
            }
            PlayCutCue(source);
        }

        private NPC ResolveSource() {
            int id = SourceId;
            if (id < 0 || id >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[id];
            return npc.active && npc.type == SourceType && npc.life > 0 ? npc : null;
        }

        private void PlayCutCue(NPC source) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.55f, Volume = 0.75f }, Projectile.Center);
            Vector2 dir = cutAngle.ToRotationVector2();
            //纸屑沿刀口对开：两半各自飘走，读作"被裁成两片"
            for (int i = 0; i < 10; i++) {
                float side = i % 2 == 0 ? 1f : -1f;
                PRTLoader.NewParticle<PRT_CrimsonSpark>(
                    Projectile.Center + dir * Main.rand.NextFloat(-24f, 24f),
                    dir.RotatedBy(MathHelper.PiOver2) * side * Main.rand.NextFloat(1.5f, 4f)
                        + Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.6f),
                    PaperBody, Main.rand.NextFloat(0.18f, 0.32f))
                    ?.Configure(Main.rand.Next(20, 34), affectedByGravity: true);
            }
            if (source == null) {
                return;
            }
            //赤线：从纸面飞向真身，把"传导"这件事画出来
            Vector2 toSource = (source.Center - Projectile.Center) / 12f;
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center,
                    toSource * Main.rand.NextFloat(0.8f, 1.25f),
                    new Color(255, 60, 46), Main.rand.NextFloat(0.20f, 0.34f))
                    ?.Configure(14, affectedByGravity: false);
            }
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(cut);
            writer.Write((short)cutTimer);
            writer.Write(cutAngle);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            cut = reader.ReadBoolean();
            cutTimer = reader.ReadInt16();
            cutAngle = reader.ReadSingle();
        }

        /// <summary>
        /// 纸型拓的是这个敌手本身：取它的贴图与挂纸那一刻的帧，按纸片尺寸收进卡面。<br/>
        /// 纯客户端表现，不同机器上帧号差一两格无所谓，所以不进网络
        /// </summary>
        private void ResolveAppearance() {
            appearanceResolved = true;
            int type = SourceType;
            if (type <= 0 || type >= TextureAssets.Npc.Length) {
                return;
            }
            Main.instance.LoadNPC(type);
            Texture2D texture = TextureAssets.Npc[type]?.Value;
            if (texture == null || texture.Width <= 0 || texture.Height <= 0) {
                return;
            }

            NPC live = ResolveSource();
            Rectangle frame = live?.frame ?? default;
            //真身已经不在、或帧越界，就退回第一帧的定姿，纸型本来就是静的
            if (frame.Width <= 0 || frame.Height <= 0 || frame.X < 0 || frame.Y < 0
                || frame.Right > texture.Width || frame.Bottom > texture.Height) {
                int frames = Math.Max(1, Main.npcFrameCount[type]);
                frame = new Rectangle(0, 0, texture.Width, texture.Height / frames);
            }
            if (frame.Width <= 0 || frame.Height <= 0) {
                return;
            }

            sourceFrame = frame;
            //收进卡面：大小敌手都裁成同一号纸，判定框才对得上看到的东西
            float fit = MathF.Min(PaperHalfWidth * 1.7f / frame.Width,
                PaperHalfHeight * 1.7f / frame.Height);
            sourceScale = MathHelper.Clamp(fit, 0.25f, 1.15f);
            sourceEffects = live?.spriteDirection == 1
                ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Main.dedServ) {
                return false;
            }
            if (!appearanceResolved) {
                ResolveAppearance();
            }
            if (sourceFrame.Width <= 0 || sourceFrame.Height <= 0) {
                return false;
            }
            Texture2D texture = TextureAssets.Npc[SourceType]?.Value;
            if (texture == null) {
                return false;
            }

            float fade = MathHelper.Clamp(timer / 8f, 0f, 1f)
                * MathHelper.Clamp(Projectile.timeLeft / 40f, 0f, 1f);
            if (cut) {
                fade *= 1f - cutTimer / (float)SplitFrames;
            }
            if (fade <= 0.01f) {
                return false;
            }

            //挂着的纸会晃；贴出来那几帧还带一道"从侧面翻正"的展开
            float sway = MathF.Sin(swayPhase) * 0.05f;
            float unfold = MathHelper.Clamp(timer / 10f, 0f, 1f);
            unfold = 1f - (1f - unfold) * (1f - unfold);
            Vector2 scale = new(sourceScale * MathHelper.Lerp(0.12f, 1f, unfold), sourceScale);
            Vector2 center = Projectile.Center - Main.screenPosition;

            Color paper = PaperBody * (fade * 0.95f);
            Color edge = OnikiriUITheme.Ink * (fade * 0.85f);
            //落刀那两帧纸面吃一记闪白，斩纸才有"响"
            if (cut && cutTimer <= 3) {
                paper = Color.Lerp(paper, new Color(255, 232, 198) * fade, 1f - cutTimer / 4f);
            }

            if (!cut) {
                DrawSheet(texture, sourceFrame, center, sway, scale, edge, paper);
                return false;
            }

            //斩纸：沿刀口把源矩形切成两片，各自朝法线滑开并微微翻转
            float split = cutTimer / (float)SplitFrames;
            Vector2 dir = cutAngle.ToRotationVector2();
            Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);
            bool acrossRows = MathF.Abs(dir.X) >= MathF.Abs(dir.Y);
            float flipX = (sourceEffects & SpriteEffects.FlipHorizontally) != 0 ? -1f : 1f;

            for (int piece = 0; piece < 2; piece++) {
                float side = piece == 0 ? 1f : -1f;
                Rectangle sub = sourceFrame;
                Vector2 local;
                if (acrossRows) {
                    int h = sourceFrame.Height / 2;
                    sub.Height = h;
                    sub.Y += piece == 0 ? 0 : sourceFrame.Height - h;
                    local = new Vector2(0f, (piece == 0 ? -1f : 1f) * h * 0.5f * scale.Y);
                }
                else {
                    int w = sourceFrame.Width / 2;
                    sub.Width = w;
                    sub.X += piece == 0 ? 0 : sourceFrame.Width - w;
                    local = new Vector2((piece == 0 ? -1f : 1f) * flipX * w * 0.5f * scale.X, 0f);
                }
                if (sub.Width <= 0 || sub.Height <= 0) {
                    continue;
                }
                float rot = sway + split * side * 0.30f;
                Vector2 pos = center + local.RotatedBy(rot) + normal * (split * 20f * side)
                    + Vector2.UnitY * (split * split * 10f);
                DrawSheet(texture, sub, pos, rot, scale, edge, paper);
            }
            return false;
        }

        /// <summary>一片纸：先四向偏移压出墨边，再铺和纸色的本体</summary>
        private void DrawSheet(Texture2D texture, Rectangle frame, Vector2 pos, float rotation,
            Vector2 scale, Color edge, Color paper) {
            Vector2 origin = frame.Size() * 0.5f;
            const float edgeOffset = 1.35f;
            Main.EntitySpriteDraw(texture, pos - Vector2.UnitX * edgeOffset, frame, edge,
                rotation, origin, scale, sourceEffects);
            Main.EntitySpriteDraw(texture, pos + Vector2.UnitX * edgeOffset, frame, edge,
                rotation, origin, scale, sourceEffects);
            Main.EntitySpriteDraw(texture, pos - Vector2.UnitY * edgeOffset, frame, edge,
                rotation, origin, scale, sourceEffects);
            Main.EntitySpriteDraw(texture, pos + Vector2.UnitY * edgeOffset, frame, edge,
                rotation, origin, scale, sourceEffects);
            Main.EntitySpriteDraw(texture, pos, frame, paper, rotation, origin, scale, sourceEffects);
        }
    }

    /// <summary>
    /// 表影「受创」：纸被斩过之后，本体挨刀更疼的那一段。<br/>
    /// 加深在 <see cref="OniMeiCombat.BuildPaperBrandMul"/> 由主刀读取；本类只挂旗标与介质
    /// </summary>
    internal class OniPaperBrandDebuff : ModBuff
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.pvpBuff[Type] = false;
            Main.buffNoSave[Type] = true;
            BuffID.Sets.LongerExpertDebuff[Type] = false;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //介质：身上黏着的纸屑还在往下掉，读得出"刚被拓过一张"
            if (Main.dedServ || !Main.rand.NextBool(6)) {
                return;
            }
            Vector2 pos = npc.Center + Main.rand.NextVector2Circular(npc.width * 0.5f, npc.height * 0.5f);
            PRTLoader.NewParticle<PRT_CrimsonSpark>(pos,
                Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.4f)
                    + Main.rand.NextVector2Circular(0.6f, 0.2f),
                new Color(226, 214, 196), Main.rand.NextFloat(0.10f, 0.18f))
                ?.Configure(Main.rand.Next(16, 26), affectedByGravity: true);
        }
    }
}
