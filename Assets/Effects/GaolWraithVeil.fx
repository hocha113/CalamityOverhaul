// ============================================================================
//GaolWraithVeil.fx 深牢怨灵屏幕层（全屏后效，拷屏回写）
//狱压：战斗常驻的轻度四缘压暗 + 冷粉狱雾沿缘漂移（牢狱的空气变稠，
//与骷髅王的玩家中心黑暗领域刻意分野——这里压的是"屏幕的四壁"）。
//冲击环 ×2：径向位移 + 冷粉环辉（转阶段嘶吼 / 破土 / 死亡）。
//冷粉冲击帧：死亡火芯谢幕全场唯一一次——先冷粉满溢后灰烬冷却。
//隐袭雾拍 uMist：怨灵没入墙体时全屏一层薄雾脉动。
//直线算术 + 平铺 tex2D，无动态分支；噪声全笛卡尔（无极角，无接缝）。
//漂移用时间平移不用正弦呼吸（常驻舒适约定）。s1=PerlinNoise（值域过 nrm）
// ============================================================================

sampler uImage0 : register(s0);
// 噪声固定 s1：C# 侧在 pass.Apply 前显式 Textures[1]=PerlinNoise + LinearWrap
sampler noiseSamp : register(s1);

float uTime;
float uAspect;              //屏宽/屏高
float uDomain;              //狱压强度 0~1
float uMist;                //隐袭薄雾拍 0~1
float uFlash;               //冷粉冲击帧强度（死亡终爆一次）
float uFlashProgress;       //冲击帧进度 0~1
float4 ringData[2];         //xy=中心uv z=半径(屏高归一) w=强度
float3 uRingColor;          //环辉色（P2 偏白热）

static const float3 GAOL_PINK = float3(0.925, 0.455, 0.612);
static const float3 GAOL_DEEP = float3(0.463, 0.133, 0.259);
static const float3 MIST_COLD = float3(0.376, 0.455, 0.502);

//PerlinNoise.r 实测值域 0.227~0.776
float nrm(float x) {
    return saturate((x - 0.227) / 0.549);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 pc = float2(coords.x * uAspect, coords.y);

    //---- 冲击环位移场（先算合位移再采样，单次取屏）----
    float2 push = float2(0.0, 0.0);
    float ringGlow = 0.0;
    for (int i = 0; i < 2; i++) {
        float2 rc = float2(ringData[i].x * uAspect, ringData[i].y);
        float2 toPix = pc - rc;
        float rd = length(toPix);
        float band = exp(-abs(rd - ringData[i].z) * 30.0) * ringData[i].w;
        float2 dir = toPix / max(rd, 0.0012);
        push += dir * band * 0.010;
        ringGlow += band;
    }

    float4 src = tex2D(uImage0, coords + float2(push.x / uAspect, push.y));
    float3 color = src.rgb;
    float lum = dot(color, float3(0.299, 0.587, 0.114));

    //---- 狱压：四缘压暗 + 冷粉狱雾贴缘漂移 ----
    float2 cc = coords * 2.0 - 1.0;
    //方形视界（牢房的四壁），比圆形暗角更"铁窗"
    float wall = max(abs(cc.x), abs(cc.y));
    float press = smoothstep(0.52, 1.06, wall) * uDomain;
    float mistN = nrm(tex2D(noiseSamp, pc * 1.35 + float2(uTime * 0.016, -uTime * 0.011)).r);
    float mistN2 = nrm(tex2D(noiseSamp, pc * 3.1 + float2(-uTime * 0.009, -uTime * 0.021)).r);
    //压暗但不吞读：高亮元素穿透
    float punch = smoothstep(0.55, 0.95, lum) * 0.6;
    color *= 1.0 - press * 0.34 * (1.0 - punch);
    //缘雾：暗角里渗进冷粉灰的怨雾
    color += lerp(MIST_COLD, GAOL_DEEP, mistN) * press * (0.05 + mistN2 * 0.075);
    //整体轻度冷移（狱里没有暖光）
    color = lerp(color, float3(lum, lum, lum) * float3(0.86, 0.92, 1.0), uDomain * 0.10);

    //---- 隐袭雾拍：全屏薄雾脉动（雾里藏着要来的东西）----
    color += MIST_COLD * uMist * (0.05 + mistN * 0.09);
    color *= 1.0 - uMist * 0.06;

    //---- 冲击环辉光 ----
    color += uRingColor * ringGlow * 0.5;

    //---- 冷粉冲击帧（死亡火芯谢幕，一场一次）----
    float flashCurve = uFlash * pow(saturate(1.0 - uFlashProgress), 1.6);
    //先满溢：亮部烧向冷粉白；后冷却：灰烬去饱和
    float overNorm = smoothstep(0.18, 0.78, lum);
    float3 blaze = lerp(GAOL_DEEP * 0.7, float3(1.0, 0.88, 0.93), overNorm);
    float coolPhase = smoothstep(0.30, 0.85, uFlashProgress);
    float3 ashen = lerp(blaze, float3(lum, lum, lum) * float3(0.82, 0.80, 0.84), coolPhase);
    //暗角收束帧
    float frameDark = 1.0 - dot(cc, cc) * 0.24;
    color = lerp(color, ashen * frameDark, saturate(flashCurve));

    return float4(color, src.a);
}

technique Technique1 {
    pass GaolWraithVeilPass {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
