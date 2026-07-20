// ============================================================================
//FishCloudPuff.fx 腾鱼驾雾积雨云体（单 quad 程序化多瓣积云）
//uv：0..1 覆盖整个 quad，云体占上段，基线以下为雨幕（virga）区
//全笛卡尔坐标，无极角，接缝协议天然合规
//
//结构：6 瓣椭圆 SDF 平滑并集（平底穹顶剪影）→ 双八度噪声撕裂边缘翻卷
//→ 顶光/底影垂直明暗 + 低频噪声絮理 → 基线下竖向拉长噪声雨幕
//uGrow 驱动聚拢成形/散逸：瓣心从四散收敛、蚀阈随成形回退
//uWind 内部剪切：云体逆风倾拉、尾侧蚀散加重
//预乘 alpha，配 BlendState.AlphaBlend，云为漫反射体，无加色无泛光
//ps_3_0
// ============================================================================

sampler uImage0 : register(s0);

texture uNoiseTex;
sampler noiseSamp = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float uTime;    //秒
float uSeed;    //实例随机相位
float uGrow;    //0-1.2 成形包络，0=散逸 1=完整，>1 乘云过冲
float uAlpha;   //整体不透明度
float uRain;    //雨幕强度 0-1
float2 uWind;   //风矢量（云局部空间，长度≈速度/极速）
float3 uTopCol; //顶部受光色（CPU 已乘环境光）
float3 uBotCol; //底部背光色（CPU 已乘环境光）

//6 瓣积云剪影：x/y/半径，y 负朝上，平底基线 y=+0.30
static const float3 kLobes[6] = {
    float3( 0.00, -0.04, 0.46),
    float3(-0.44,  0.07, 0.33),
    float3( 0.42,  0.06, 0.34),
    float3(-0.20, -0.24, 0.32),
    float3( 0.22, -0.21, 0.33),
    float3( 0.68,  0.18, 0.20)
};

float smin(float a, float b, float k)
{
    float h = saturate(0.5 + 0.5 * (b - a) / k);
    return lerp(b, a, h) - k * h * (1.0 - h);
}

float4 PixelShaderFunction(float4 vColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    //归一坐标：x ±1.1，y 上负下正，云心在 uv.y=0.36
    float2 p = float2((uv.x - 0.5) * 2.2, (uv.y - 0.36) * 2.6);

    float windLen = length(uWind);
    float2 windN = windLen > 0.001 ? uWind / windLen : float2(0.0, 0.0);

    //内部剪切：基线锚定，越往顶越滞后于风（云顶拖在运动身后），读作被风撕拉的形变
    float drag = saturate(0.30 - p.y);
    float2 ps = p + uWind * drag * 0.34;

    float scatter = saturate(1.0 - uGrow);

    //6 瓣平滑并集 SDF，散逸时瓣心外飘、半径收缩
    float d = 10.0;
    [unroll]
    for (int i = 0; i < 6; i++) {
        float fi = (float)i;
        float3 lb = kLobes[i];
        float2 sc = float2(sin(uSeed * 5.3 + fi * 2.4), cos(uSeed * 3.1 + fi * 1.7));
        float2 c = lb.xy + sc * scatter * 0.85;
        float r = lb.z * (0.30 + 0.70 * min(uGrow, 1.2))
            * (1.0 + 0.055 * sin(uTime * (0.7 + fi * 0.11) + uSeed * 7.0 + fi * 2.3));
        float di = length((ps - c) * float2(1.0, 1.38)) - r;
        d = smin(d, di, 0.17);
    }
    //平底：基线以下裁掉，噪声随后把切口打毛
    d = max(d, ps.y - 0.30);

    //边缘翻卷：双八度流噪声，第二八度慢旋转采样（刚性仿射，无缝）
    float ang = uTime * 0.04;
    float2 pr = float2(p.x * cos(ang) - p.y * sin(ang), p.x * sin(ang) + p.y * cos(ang));
    float n1 = tex2D(noiseSamp, p * 0.42 + float2(uTime * 0.020, -uTime * 0.012) + uSeed).r;
    float n2 = tex2D(noiseSamp, pr * 0.95 + float2(-uTime * 0.031, uTime * 0.017) + uSeed * 2.0).r;
    float e = n1 * 0.62 + n2 * 0.38;

    //尾侧蚀散：逆风一侧撕得更碎
    float tail = saturate(-dot(p, windN)) * windLen;

    float erode = (e - 0.5) * 0.34 + tail * 0.20 + scatter * 0.42;
    float cloudA = smoothstep(0.05, -0.10, d + erode) * 0.96;

    //絮理：低频噪声调制内部密度与明暗
    float m = tex2D(noiseSamp, p * 0.20 + uSeed * 3.0 + float2(uTime * 0.008, 0.0)).r;

    //顶光/底影：漫反射亮度结构，暗底/灰中/亮顶
    float sh = smoothstep(-0.52, 0.30, p.y);
    float3 col = lerp(uTopCol, uBotCol, sh);
    col *= 0.88 + 0.20 * m;
    //顶缘薄亮壳：仅上侧贴边一窄带，非泛光
    float rim = smoothstep(0.0, -0.35, p.y) * smoothstep(-0.18, 0.02, d + erode);
    col += uTopCol * rim * 0.16;
    //雨云蓄水：基部随雨幕加深
    col = lerp(col, uBotCol * 0.80, uRain * smoothstep(-0.05, 0.30, p.y) * 0.45);

    //雨幕 virga：基线下竖向拉长噪声纹，下移滚动
    float vN = tex2D(noiseSamp, float2(p.x * 1.35 + uSeed * 4.0, p.y * 0.16 - uTime * 0.42)).r;
    float streak = smoothstep(0.52, 0.80, vN);
    float vFade = smoothstep(0.26, 0.46, p.y) * (1.0 - smoothstep(0.62, 1.15, p.y));
    float vCenter = smoothstep(1.00, 0.40, abs(p.x));
    float virgaA = streak * vFade * vCenter * uRain * 0.40;
    float3 virgaCol = uBotCol * 0.72;

    //云体在前，雨幕垫底，预乘合成
    float a = cloudA + virgaA * (1.0 - cloudA);
    float3 rgb = col * cloudA + virgaCol * virgaA * (1.0 - cloudA);

    float k = uAlpha * vColor.a;
    return float4(rgb * k, a * k);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
