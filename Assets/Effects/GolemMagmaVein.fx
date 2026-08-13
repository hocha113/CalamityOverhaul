// ============================================================================
//GolemMagmaVein.fx 石巨人岩浆脉络 / 崩解侵蚀
//VeinTech：Additive 批，贴体发光脉络（采身体贴图 alpha 作蒙版）
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
texture uNoise;
sampler noiseSampler = sampler_state
{
    Texture = <uNoise>;
    AddressU = Wrap;
    AddressV = Wrap;
    MagFilter = Linear;
    MinFilter = Linear;
};

//帧内局部 uv（0~1）
float2 LocalUV(float2 coords)
{
    return (coords - uFrame.xy) / max(uFrame.zw, 1e-4);
}

//------------------------------------------------------------------
//岩浆脉络：噪声脊线成缝，缝内熔金流动
//------------------------------------------------------------------
float4 VeinPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 body = tex2D(uImage0, coords);
    float2 local = LocalUV(coords);

    //与 CrumbleTech 同步的侵蚀遮罩：被吞没区域不再发脉络光
    float edgeNoiseK = tex2D(noiseSampler, float2(local.x * 3.4, local.x * 0.7 + uTime * 0.05)).r;
    float eatLine = uCrumble * 1.18 - 0.09 + (edgeNoiseK - 0.5) * 0.16;
    float keepMask = 1.0 - step(local.y, eatLine);

    //双频噪声脊线：|n-0.5| 越小越接近缝心
    float n1 = tex2D(noiseSampler, local * 2.6 + float2(0.0, uTime * 0.03)).r;
    float n2 = tex2D(noiseSampler, local * 5.2 - float2(uTime * 0.02, 0.0)).r;
    float ridge = abs(n1 - 0.5) * 1.6 + abs(n2 - 0.5) * 0.7;
    float vein = 1.0 - smoothstep(0.05, 0.16, ridge);

    //缝内流动：沿缝相位滚动的亮波
    float flow = sin(n1 * 14.0 + n2 * 9.0 - uTime * 3.2) * 0.5 + 0.5;
    flow = 0.55 + 0.45 * flow;

    //色阶：深红→橙→熔金
    float heat = vein * flow * uGlow;
    float3 deepRed = float3(0.55, 0.08, 0.02);
    float3 orange  = float3(1.00, 0.45, 0.08);
    float3 gold    = float3(1.00, 0.85, 0.40);
    float3 col = lerp(deepRed, orange, saturate(heat * 1.6));
    col = lerp(col, gold, saturate(heat * heat * 2.2));

    //宝石位常亮（胸口中带）
    float gem = exp(-dot(local - float2(0.5, 0.42), local - float2(0.5, 0.42)) * 90.0) * uGlow * 0.8;
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
