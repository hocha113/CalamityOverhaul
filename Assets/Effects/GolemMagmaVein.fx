// ============================================================================
//GolemMagmaVein.fx 石巨人岩浆脉络 / 崩解侵蚀
//VeinTech：Additive 批，贴体发光脉络（采身体贴图 alpha 作蒙版）——
//  域扭曲单脊线成连贯裂缝网，涌动=以太阳宝石为源的外推亮波（能量从胸口泵向全身）
//CrumbleTech：AlphaBlend 预乘批，自上而下噪声侵蚀 + 蚀线炽边
//uFrame = (x,y,w,h) 帧区域归一，防串帧
//无动态分支，噪声全走贴图采样（s1）
// ============================================================================

sampler uImage0 : register(s0);   //身体贴图

float uTime;
float uGlow;       //脉络强度 0~1
float uCrumble;    //侵蚀进度 0~1
float4 uFrame;     //帧区域 (x, y, w, h)
float4 uColor;     //环境光色（Crumble 用）
// 噪声固定 s1：sampler_state 自动分配在 SpriteBatch 下必被 s0 覆写（曾靠 uImage0 占位侥幸落 s1）；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSampler : register(s1);

//帧内局部 uv（0~1）
float2 LocalUV(float2 coords)
{
    return (coords - uFrame.xy) / max(uFrame.zw, 1e-4);
}

//------------------------------------------------------------------
//岩浆脉络：域扭曲单脊线成连贯裂缝网，宝石为源的涌动波外推
//------------------------------------------------------------------
float4 VeinPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 body = tex2D(uImage0, coords);
    float2 local = LocalUV(coords);

    //与 CrumbleTech 同步的侵蚀遮罩：被吞没区域不再发脉络光
    float edgeNoiseK = tex2D(noiseSampler, float2(local.x * 3.4, local.x * 0.7 + uTime * 0.05)).r;
    float eatLine = uCrumble * 1.18 - 0.09 + (edgeNoiseK - 0.5) * 0.16;
    float keepMask = 1.0 - step(local.y, eatLine);

    //域扭曲单脊线：低频扭曲场让裂缝蜿蜒但不断线
    //（旧版双频脊线相加通带过窄=散点碎斑，判词"碎"的来源）
    float wx = tex2D(noiseSampler, local * 1.15 + float2(uTime * 0.012, 0.0)).r;
    float wy = tex2D(noiseSampler, local * 1.15 + float2(0.37, 0.61) - float2(0.0, uTime * 0.009)).r;
    float2 warp = float2(wx, wy) - 0.5;
    float n1 = tex2D(noiseSampler, local * 2.3 + warp * 0.42).r;
    float ridge = abs(n1 - 0.5);
    //缝体窄带+缝心白热双层：石壳大面积留黑，缝内有深度
    float crackBody = 1.0 - smoothstep(0.022, 0.085, ridge);
    float crackCore = 1.0 - smoothstep(0.0, 0.028, ridge);
    //第二八度只调制缝宽亮度，不参与拓扑（缝保持连贯）
    float n2 = tex2D(noiseSampler, local * 4.4 - float2(uTime * 0.02, 0.0)).r;
    float vein = (crackBody * 0.65 + crackCore * 0.55) * (0.62 + 0.38 * n2);

    //涌动主体：以太阳宝石为源的外推亮波，岩浆一股股泵向全身
    float2 gemVec = local - float2(0.5, 0.42);
    gemVec.y *= 1.35;   //补偿身体纵横比，波前近似同心
    float gemDist = length(gemVec);
    float wave = sin(gemDist * 11.5 - uTime * 4.0);
    float surge = 0.34 + 0.66 * smoothstep(0.12, 0.88, wave);

    //色阶：深红→橙→熔金（波峰推到熔金）
    float heat = vein * surge * uGlow;
    float3 deepRed = float3(0.55, 0.08, 0.02);
    float3 orange  = float3(1.00, 0.45, 0.08);
    float3 gold    = float3(1.00, 0.85, 0.40);
    float3 col = lerp(deepRed, orange, saturate(heat * 1.6));
    col = lerp(col, gold, saturate(heat * heat * 2.2));

    //宝石位常亮，与涌波同相呼吸（波从这里泵出）
    float gemPulse = 0.78 + 0.22 * sin(-uTime * 4.0);
    float gem = exp(-dot(local - float2(0.5, 0.42), local - float2(0.5, 0.42)) * 90.0) * uGlow * 0.8 * gemPulse;
    col += float3(1.0, 0.8, 0.35) * gem;

    float a = saturate((heat + gem) * body.a) * keepMask;
    return float4(col * vertexColor.rgb, a * vertexColor.a);
}

//------------------------------------------------------------------
//崩解侵蚀：蚀线自上而下推进，边缘噪声撕口 + 炽边
//------------------------------------------------------------------
float4 CrumblePS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 body = tex2D(uImage0, coords);
    float2 local = LocalUV(coords);

    //蚀线：噪声抖动的水平推进线
    float edgeNoise = tex2D(noiseSampler, float2(local.x * 3.4, local.x * 0.7 + uTime * 0.05)).r;
    float line1 = uCrumble * 1.18 - 0.09 + (edgeNoise - 0.5) * 0.16;
    //local.y < line1 的区域已被吞没
    float gone = step(local.y, line1);
    float keep = 1.0 - gone;

    //蚀线炽边：line1 附近向存留侧发光
    float edgeDist = local.y - line1;
    float ember = exp(-max(edgeDist, 0.0) * 26.0) * step(0.0, edgeDist);
    float emberFlick = 0.7 + 0.3 * sin(uTime * 21.0 + local.x * 30.0);

    //本体安放环境光
    float3 baseCol = body.rgb * uColor.rgb;

    //缝隙漏光（临近蚀线的整体升温）
    float preHeat = exp(-max(edgeDist, 0.0) * 7.0) * 0.4;
    float3 hotCol = float3(1.0, 0.55, 0.12);
    float3 col = lerp(baseCol, hotCol, saturate(preHeat));
    col += float3(1.0, 0.75, 0.3) * ember * emberFlick * 1.4;

    float a = body.a * keep;
    //预乘输出
    return float4(col * a, a) * vertexColor.a;
}

technique VeinTech
{
    pass VeinPass
    {
        PixelShader = compile ps_3_0 VeinPS();
    }
}

technique CrumbleTech
{
    pass CrumblePass
    {
        PixelShader = compile ps_3_0 CrumblePS();
    }
}
