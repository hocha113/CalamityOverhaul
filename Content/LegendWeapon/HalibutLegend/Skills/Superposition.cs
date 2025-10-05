using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills
{
    internal static class Superposition
    {
        public static int ID = 6;
        private const int ToggleCD = 30;
        private const int SuperpositionCooldown = 1800; // 30秒终极技能冷却

        public static void AltUse(Item item, Player player) {
            var hp = player.GetOverride<HalibutPlayer>();
            if (hp.SuperpositionToggleCD > 0 || hp.SuperpositionCooldown > 0) return;
            Activate(player);
            hp.SuperpositionToggleCD = ToggleCD;
            hp.SuperpositionCooldown = 60;//调试用
        }

        public static void Activate(Player player) {
            if (Main.myPlayer == player.whoAmI) {
                SpawnSuperpositionEffect(player);
            }
        }

        internal static void SpawnSuperpositionEffect(Player player) {
            var source = player.GetSource_Misc("SuperpositionSkill");
            Projectile.NewProjectile(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<SuperpositionProj>(), 0, 0, player.whoAmI);
        }
    }

    #region 时空克隆体
    internal class TimeClone
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Vector2 SpawnPos;
        public float Alpha;
        public float Life;
        public float MaxLife;
        public float Scale;
        public PlayerSnapshot Snapshot;
        public readonly List<Vector2> TrailPositions = new();
        private const int MaxTrailLength = 28;
        private float spiralAngle;
        private readonly float timeWarpFactor;
        private float orbitRadius;
        private bool converging;

        public TimeClone(Vector2 spawnPos, PlayerSnapshot snapshot, float startOrbitRadius) {
            Position = spawnPos;
            SpawnPos = spawnPos;
            Snapshot = snapshot;
            Velocity = Vector2.Zero;
            Life = 0f;
            MaxLife = 140f; // 更长展示期
            Alpha = 0f;
            Scale = 0.75f;
            spiralAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            timeWarpFactor = Main.rand.NextFloat(0.8f, 1.25f);
            orbitRadius = startOrbitRadius; // 初始环绕半径
        }

        public void SetConverging() => converging = true;

        public void Update(Vector2 center, float gatherProgress, float convergeProgress) {
            Life++;
            float targetRadius = converging 
                ? MathHelper.Lerp(orbitRadius, 0f, MathHelper.SmoothStep(0f,1f, convergeProgress))
                : MathHelper.Lerp(orbitRadius * 1.15f, orbitRadius * 0.85f, (float)Math.Sin(gatherProgress * MathHelper.Pi));
            orbitRadius = MathHelper.Lerp(orbitRadius, targetRadius, converging ? 0.18f : 0.05f);
            spiralAngle += 0.07f * timeWarpFactor + (converging ? 0.12f : 0f);
            Vector2 targetPos = center + spiralAngle.ToRotationVector2() * orbitRadius;
            Vector2 toTarget = (targetPos - Position);
            Velocity = Vector2.Lerp(Velocity, toTarget * (converging ? 0.25f : 0.18f), 0.4f);
            Position += Velocity;
            if (!converging) Alpha = MathHelper.Clamp(gatherProgress * 1.6f, 0f, 1f); else Alpha = (float)Math.Pow(1f - convergeProgress, 0.6f);
            TrailPositions.Insert(0, Position);
            if (TrailPositions.Count > MaxTrailLength) TrailPositions.RemoveAt(TrailPositions.Count - 1);
        }

        public bool ShouldRemove() => Life >= MaxLife || (converging && orbitRadius < 4f && Alpha < 0.05f);

        public void DrawTrail(float globalAlpha) {
            if (TrailPositions.Count < 3) return;
            Texture2D tex = TextureAssets.MagicPixel.Value;
            for (int i = 0; i < TrailPositions.Count - 1; i++) {
                float p = i / (float)TrailPositions.Count;
                float trailAlpha = (1f - p) * Alpha * globalAlpha * 0.55f;
                Vector2 a = TrailPositions[i];
                Vector2 b = TrailPositions[i + 1];
                Vector2 d = b - a;
                float len = d.Length();
                if (len < 0.01f) continue;
                float rot = d.ToRotation();
                Color c = new Color(170, 120, 255, 0) * trailAlpha;
                Main.spriteBatch.Draw(tex, a - Main.screenPosition, new Rectangle(0,0,1,1), c, rot, Vector2.Zero, new Vector2(len, 6f - p * 4f), SpriteEffects.None, 0f);
            }
        }
    }
    #endregion

    #region 法阵符环
    internal class RuneCircle
    {
        public float Life;
        public float MaxLife;
        public float StartRadius;
        public float EndRadius;
        public float Rotation;
        public float RotSpeed;
        public float EllipseFactor;
        public Color ColorA;
        public Color ColorB;
        public bool Shrink;
        public RuneCircle(float startR,float endR,int life,bool shrink, Color a, Color b){
            StartRadius=startR;EndRadius=endR;MaxLife=life;Life=0;Shrink=shrink;Rotation=Main.rand.NextFloat(MathHelper.TwoPi);RotSpeed=Main.rand.NextFloat(-0.05f,0.05f);EllipseFactor=Main.rand.NextFloat(0.6f,1.15f);ColorA=a;ColorB=b;}
        public void Update(){Life++;Rotation+=RotSpeed;}
        public bool Dead=>Life>=MaxLife;
        public void Draw(Vector2 center,float alpha){
            float p=Life/MaxLife;
            float radius = Shrink ? MathHelper.Lerp(StartRadius, EndRadius, p) : MathHelper.Lerp(StartRadius, EndRadius, (float)Math.Sin(p*MathHelper.Pi));
            float fade = (float)Math.Sin(p*MathHelper.Pi) * alpha;
            if (fade <= 0.01f) return;
            Texture2D pix = TextureAssets.MagicPixel.Value;
            int seg = 120; float step = MathHelper.TwoPi/seg;
            for(int i=0;i<seg;i++){
                float ang1 = Rotation + i*step; float ang2 = Rotation + (i+1)*step;
                Vector2 p1 = center + new Vector2((float)Math.Cos(ang1)*radius,(float)Math.Sin(ang1)*radius*EllipseFactor);
                Vector2 p2 = center + new Vector2((float)Math.Cos(ang2)*radius,(float)Math.Sin((ang2))*radius*EllipseFactor);
                Vector2 d = p2-p1;float len=d.Length(); if(len<0.0001f) continue; float rot=d.ToRotation();
                float wave = (float)Math.Sin(ang1*6f + Main.GlobalTimeWrappedHourly*8f)*0.5f +0.5f;
                Color c = Color.Lerp(ColorA, ColorB, wave)*fade*0.6f;
                Main.spriteBatch.Draw(pix,p1 - Main.screenPosition,new Rectangle(0,0,1,1),c,rot,Vector2.Zero,new Vector2(len,2f),SpriteEffects.None,0f);
            }
        }
    }
    #endregion

    internal class SuperpositionProj : BaseHeldProj
    {
        public override string Texture => CWRConstant.Placeholder;

        private List<TimeClone> timeClones;
        private List<RuneCircle> runeCircles = new();
        private enum SuperpositionState { Gathering, Converging, Charging, Launching, Exploding }
        private SuperpositionState currentState = SuperpositionState.Gathering;
        private int stateTimer = 0;
        private const int GatherDuration = 60;
        private const int ConvergeDuration = 45;
        private const int ChargeDuration = 36;
        private const int LaunchDuration = 180;
        private const int ExplodeDuration = 40;
        private float effectAlpha = 0f;
        private float chargeIntensity = 0f; // 保留进度控制(不再绘制十字星)
        private Vector2 attackDirection = Vector2.UnitX;
        private readonly List<TimeRift> timeRifts = new();
        private readonly List<EnergyOrb> energyOrbs = new();

        public override void SetDefaults() {
            Projectile.width = 900;
            Projectile.height = 900;
            Projectile.timeLeft = GatherDuration + ConvergeDuration + ChargeDuration + LaunchDuration + ExplodeDuration + 30;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
        }

        public override void AI() {
            if (!Owner.active) { Projectile.Kill(); return; }
            Projectile.Center = Owner.Center;
            stateTimer++;
            switch (currentState) {
                case SuperpositionState.Gathering: UpdateGathering(); break;
                case SuperpositionState.Converging: UpdateConverging(); break;
                case SuperpositionState.Charging: UpdateCharging(); break;
                case SuperpositionState.Launching: UpdateLaunching(); break;
                case SuperpositionState.Exploding: UpdateExploding(); break;
            }
            UpdateLists();
        }

        private void UpdateLists(){
            if (timeClones!=null){
                float gatherProgress = currentState==SuperpositionState.Gathering? stateTimer/(float)GatherDuration : 1f;
                float convergeProgress = currentState==SuperpositionState.Converging? stateTimer/(float)ConvergeDuration : (currentState>SuperpositionState.Converging?1f:0f);
                foreach (var c in timeClones) { if (currentState==SuperpositionState.Converging) c.SetConverging(); c.Update(Owner.Center, gatherProgress, convergeProgress);} timeClones.RemoveAll(c=>c.ShouldRemove()); }
            foreach(var r in runeCircles) r.Update(); runeCircles.RemoveAll(r=>r.Dead);
            foreach(var tr in timeRifts) tr.Update(); timeRifts.RemoveAll(r=>r.ShouldRemove());
            foreach(var orb in energyOrbs) {orb.Update(Owner.Center);} energyOrbs.RemoveAll(o=>o.ShouldRemove());
        }

        private void UpdateGathering(){
            float p = stateTimer/(float)GatherDuration; effectAlpha = MathHelper.Clamp(p*1.3f,0f,1f);
            if (stateTimer==1){ InitializeTimeClones(); SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen,Owner.Center); }
            if (stateTimer % 12 ==0) runeCircles.Add(new RuneCircle(260,300,50,false,new Color(120,90,210), new Color(200,150,255)) );
            if (stateTimer % 18 ==0) timeRifts.Add(new TimeRift(Owner.Center + Main.rand.NextVector2Circular(360,220)));
            if (stateTimer>=GatherDuration){ currentState=SuperpositionState.Converging; stateTimer=0; }
        }
        private void UpdateConverging(){
            if (stateTimer % 10 ==0) runeCircles.Add(new RuneCircle(220,120,40,true,new Color(160,110,240), new Color(230,200,255)) );
            if (stateTimer>=ConvergeDuration){ currentState=SuperpositionState.Charging; stateTimer=0; attackDirection=(Main.MouseWorld-Owner.Center).SafeNormalize(Vector2.UnitX); }
        }
        private void UpdateCharging(){
            float p = stateTimer/(float)ChargeDuration; chargeIntensity = p; effectAlpha=1f;
            if (stateTimer==1){ SoundEngine.PlaySound(SoundID.Item72 with {Volume=1.1f},Owner.Center);}            
            if (stateTimer % 6 ==0) runeCircles.Add(new RuneCircle(140, 210, 32,false,new Color(180,130,255), new Color(255,255,255)));            
            if (stateTimer>=ChargeDuration){ currentState=SuperpositionState.Launching; stateTimer=0; LaunchSpiralAttack(); SoundEngine.PlaySound(SoundID.DD2_BetsyScream,Owner.Center);} }
        private void UpdateLaunching(){
            if (stateTimer % 24 ==0) runeCircles.Add(new RuneCircle(90,40,50,true,new Color(150,110,240), new Color(220,180,255)) );
            if (stateTimer>=LaunchDuration){ currentState=SuperpositionState.Exploding; stateTimer=0; }
        }
        private void UpdateExploding(){ float p = stateTimer/(float)ExplodeDuration; effectAlpha = 1f-p; if (stateTimer==1) SoundEngine.PlaySound(SoundID.Item14,Owner.Center); if (stateTimer<16 && stateTimer%2==0) runeCircles.Add(new RuneCircle(60,10,30,true,new Color(200,150,255), new Color(255,255,255)) ); if (stateTimer>=ExplodeDuration) Projectile.Kill(); }

        private void InitializeTimeClones(){
            timeClones = new List<TimeClone>();
            int cloneCount = 26; float outerRing = 420f;
            for(int i=0;i<cloneCount;i++){
                float edge = Main.rand.NextFloat(4f); Vector2 spawn;
                if(edge<1f) spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-600,600), -800);
                else if(edge<2f) spawn = Owner.Center + new Vector2(Main.rand.NextFloat(-600,600), 800);
                else if(edge<3f) spawn = Owner.Center + new Vector2(-800, Main.rand.NextFloat(-600,600));
                else spawn = Owner.Center + new Vector2(800, Main.rand.NextFloat(-600,600));
                timeClones.Add(new TimeClone(spawn, new PlayerSnapshot(Owner), outerRing));
            }
        }

        private void LaunchSpiralAttack(){
            if (!Owner.active) {
                return;
            }
            //待完善
        }

        public override bool PreDraw(ref Color lightColor) {
            foreach(var rc in runeCircles) rc.Draw(Owner.Center, effectAlpha);
            if (timeClones!=null) foreach(var c in timeClones) c.DrawTrail(effectAlpha);
            if (timeClones!=null){ foreach(var c in timeClones){ DrawTimeClone(c);} }
            return false;
        }

        private void DrawTimeClone(TimeClone clone){
            if (clone.Alpha < 0.05f) return;
            Player ghost = new Player(); ghost.ResetEffects(); ghost.CopyVisuals(Owner);
            ghost.position = clone.Position - Owner.Size*0.5f; ghost.direction = Owner.direction;
            ghost.bodyFrame = Owner.bodyFrame; ghost.legFrame = Owner.legFrame;
            Color ghostColor = new Color(170,130,255) * clone.Alpha * 0.9f;
            ghost.skinColor=ghostColor; ghost.shirtColor=ghostColor; ghost.underShirtColor=ghostColor; ghost.pantsColor=ghostColor; ghost.shoeColor=ghostColor; ghost.hairColor=ghostColor; ghost.eyeColor=ghostColor;
            try { Main.PlayerRenderer.DrawPlayer(Main.Camera, ghost, ghost.position, 0f, ghost.fullRotationOrigin);} catch {}
        }
    }

    #region 时空裂隙 (精简视觉占比)
    internal class TimeRift
    {
        public Vector2 Position; public float Life; public float MaxLife; public float Rotation; public float Scale;
        public TimeRift(Vector2 pos){ Position=pos; Life=0; MaxLife=Main.rand.NextFloat(50f,90f); Rotation=Main.rand.NextFloat(MathHelper.TwoPi); Scale=Main.rand.NextFloat(0.6f,1.3f);}        
        public void Update(){ Life++; Rotation += 0.04f; }
        public bool ShouldRemove()=> Life>=MaxLife;
        public void Draw(Vector2 c){} // 旧绘制弃用
    }
    #endregion

    #region 能量球 (保留简化)
    internal class EnergyOrb
    {
        public Vector2 Position; public Vector2 Velocity; public float Life; public float MaxLife; public float Scale;
        public EnergyOrb(Vector2 pos){ Position=pos; Velocity=Vector2.Zero; Life=0; MaxLife=70f; Scale=Main.rand.NextFloat(0.4f,0.9f);}        
        public void Update(Vector2 target){ Life++; Vector2 to = (target-Position).SafeNormalize(Vector2.Zero); Velocity = Vector2.Lerp(Velocity,to*14f,0.12f); Position+=Velocity; }
        public bool ShouldRemove()=> Life>=MaxLife;
    }
    #endregion
}
