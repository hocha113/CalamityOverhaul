// ============================================================================
//PallbearerSeal.fx 落棺封殓：目标身后瞬现的焦黑棺影
//肩六边形棺材轮廓（分段宽度剖面 SDF）+ 焦黑板材 + 发光的血红板缝（缝隙即烙印）+
//深红沿缘流火 + 中缝合盖血光 + 噪声侵蚀的瞬现/碎散 + 合盖暖色白闪帧。
//quad 局部 uv 0..1，C# 端传 uSizePx 像素尺寸；世界几何由顶点承载。
//色彩纪律 v2：uColBody/uColBodyDark（焦黑棺木）+ uColBrand（血色）+ uColEmber（深红）；
//无青/绿/蓝；亮色仅合盖一帧的暖色过曝（uSlam 指数衰减）。
//极角审计：无 atan2/theta/phi 消费；全部为笛卡尔坐标、线性距离场与贴图采样，无缝隙风险。
//预乘 alpha 输出，配 BlendState.AlphaBlend（棺体是「暗」的，要能压住背景）
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //秒
float uSeed;      //每具棺随机相位
float uReveal;    //0..1 瞬现进度（噪声侵蚀反向扫入）
float uErode;     //0..1 碎散进度
float uClose;     //0..1 合盖进度（死寂段中缝收拢、血光增压）
float uSlam;      //合盖暖色白闪帧强度（1→指数衰减）
float2 uSizePx;   //quad 像素尺寸

float3 uColBody;      //焦黑棺木（板面）
float3 uColBodyDark;  //焦黑（深）
float3 uColBrand;     //血色（缝隙/压线/中缝）
float3 uColEmber;     //深红（沿缘流火）

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

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float noiseTex(float2 uv)
{
    return tex2D(noiseSamp, uv).r;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    //局部像素坐标，原点棺心，y 向下
    float2 p = (uv - 0.5) * uSizePx;
    float hw = uSizePx.x * 0.5 - 6.0;
    float hh = uSizePx.y * 0.5 - 6.0;

    //====== 肩六边形棺形：宽度随高度分段变化 ======
    float yt = p.y / hh; //-1 顶(棺首) .. +1 底(棺足)
    //头 0.56w → 肩(yt=-0.42) 1.0w → 足 0.40w
    float wHead = lerp(0.56, 1.0, smoothstep(-1.0, -0.42, yt));
    float wFoot = lerp(1.0, 0.40, smoothstep(-0.42, 1.0, yt));
    float halfWidth = min(wHead, wFoot) * hw;
    //近似 SDF：横向超出量与纵向超出量取大（px）
    float d = max(abs(p.x) - halfWidth, abs(p.y) - hh);

    //====== 瞬现/碎散的噪声侵蚀 ======
    float nA = noiseTex(uv * 2.6 + uSeed * 7.3);
    //瞬现：阈值自 1 扫向 0，噪声高处先成形；辅以自心向外的径向偏置
    float radial = saturate(length(p / float2(hw, hh)));
    float revealField = nA * 0.72 + (1.0 - radial) * 0.28;
    float appear = smoothstep(1.0 - uReveal - 0.14, 1.0 - uReveal, revealField);
    //碎散：反向侵蚀，前沿留一圈血色燃边
    float nB = noiseTex(uv * 3.1 + uSeed * 12.9 + float2(0.0, uTime * 0.015));
    float erodeEdge = uErode * 1.14;
    float dissolve = smoothstep(erodeEdge - 0.10, erodeEdge, nB);
    float emberBand = smoothstep(erodeEdge - 0.05, erodeEdge, nB) * (1.0 - smoothstep(erodeEdge, erodeEdge + 0.09, nB));

    //====== 轮廓 alpha：边缘毛化 ======
    float edgeFuzz = (nA - 0.5) * 3.0;
    float bodyMask = 1.0 - smoothstep(-1.5 + edgeFuzz, 1.5 + edgeFuzz, d);

    //====== 焦黑棺板：暗调木纹，边缘压得更黑 ======
    float grain = noiseTex(float2(p.x * 0.014, p.y * 0.0022) + uSeed * 3.7);
    float3 col = lerp(uColBodyDark, uColBody, grain * 0.8);
    col *= lerp(0.55, 1.0, saturate(-d / 26.0));

    //====== 板缝 = 发光的血红裂隙：焦黑棺体内透出的血光 ======
    float seamT = abs(frac(p.y / 54.0 + 0.5) - 0.5) * 2.0;
    float boardSeam = 1.0 - smoothstep(0.0, 0.09, seamT);
    //裂隙血光随噪声明灭（余烬呼吸），越靠棺心越亮
    float seamFlicker = 0.55 + 0.45 * noiseTex(float2(p.x * 0.006 - uTime * 0.08, uSeed * 9.1));
    float seamGlow = boardSeam * seamFlicker * saturate(-d / 14.0);
    col += uColBrand * seamGlow * (0.85 + uClose * 0.7);

    //====== 血红压线：沿轮廓内缩 12px 的烙印纹 ======
    float trimLine = 1.0 - smoothstep(0.8, 3.0, abs(d + 12.0));
    float trimPulse = 0.6 + 0.4 * noiseTex(float2(uv.y * 2.0 - uTime * 0.1, uSeed));
    col += uColBrand * trimLine * trimPulse * 0.8;

    //====== 中缝：两扇棺盖的合线，合盖时血光暴涨 ======
    float seamGlowW = lerp(5.0, 1.4, uClose); //收拢：缝隙变细
    float centerSeam = 1.0 - smoothstep(0.0, seamGlowW, abs(p.x));
    float seamHot = centerSeam * (0.25 + uClose * 1.15);
    col += uColBrand * seamHot * saturate(-d / 8.0);

    //====== 深红沿缘流火：轮廓等距带内的流动余烬 ======
    float rimBand = 1.0 - smoothstep(0.0, 8.0, abs(d + 3.0));
    float flow = noiseTex(float2(uv.y * 2.4 - uTime * 0.5, uv.x * 0.8 + uSeed * 5.1));
    float rim = rimBand * (0.35 + 0.65 * flow) * (0.6 + uClose * 0.4);
    col += uColEmber * rim * 1.1;

    //====== 碎散燃边 + 合盖暖色白闪（一帧即灭的过曝）======
    col += uColBrand * emberBand * 1.6;
    float slamMask = saturate(rimBand + centerSeam + boardSeam * 0.6 + 0.3);
    col = lerp(col, float3(1.02, 0.56, 0.30), uSlam * slamMask * 0.9);

    //====== 合成：棺影 92% 实体度（黑要压得住）======
    float a = bodyMask * appear * dissolve * 0.92;
    return float4(col * a, a) * input.Color;
}

technique Technique1
{
    pass P0
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
