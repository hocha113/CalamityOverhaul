// ============================================================================
//CultistVeil.fx 仪式帷幕全屏后效
//向心捏聚色散 + 外域压暗 + 符环带(24θ整数谐波) + 元素染色 + 白闪 + 死亡去饱和
//直线算术 + 普通tex2D（FNA3D法则）；噪声采样走笛卡尔uv
// ============================================================================

sampler uImage0 : register(s0);
texture uNoise;
sampler2D noiseTex = sampler_state
{
    texture = <uNoise>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

float uTime;
float uIntensity;   //0~1 帷幕强度
float2 uCenter;     //归一化屏幕uv圆心
float uAspect;      //宽高比
float3 uTint;       //元素主色(0~1)
float uFlash;       //0~1 白闪
float uBreak;       //0~1 去饱和（死亡演出）
float uBandRadius;  //符环半径（屏高归一）

float4 VeilPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 d = (coords - uCenter) * float2(uAspect, 1.0);
    float r = length(d) + 1e-5;
    float2 dir = d / r;
    dir.x /= uAspect;

    //向心捏聚（近圆心处最强，随距离指数衰减）
    float pinch = uIntensity * 0.011 * exp(-r * 1.5);
    float2 off = dir * pinch;

    //轻色散采样
    float3 col;
    col.r = tex2D(uImage0, coords - off * 1.25).r;
    col.g = tex2D(uImage0, coords - off).g;
    col.b = tex2D(uImage0, coords - off * 0.78).b;

    //外域压暗（舞台追光感）
    float dark = smoothstep(uBandRadius * 0.85, uBandRadius * 2.4, r) * (0.44 * uIntensity);
    col *= 1.0 - dark;

    //死亡去饱和
    float lum = dot(col, float3(0.30, 0.59, 0.11));
    col = lerp(col, float3(lum, lum, lum), uBreak * 0.65);

    //符环带：细环 + 24θ流转符点 + 噪声闪烁（笛卡尔uv采样）
    float band = exp(-pow((r - uBandRadius) / (0.045 + uBandRadius * 0.06), 2.0));
    float theta = atan2(d.y, d.x);
    float ticks = pow(0.5 + 0.5 * sin(24.0 * theta + uTime * 0.9), 6.0);
    float n = tex2D(noiseTex, d * 0.35 + uTime * 0.015).r;
    float runeGlow = band * ticks * (0.45 + 0.55 * n) * uIntensity * 0.65;
    col += uTint * runeGlow;

    //心区呼吸微染
    col += uTint * (uIntensity * 0.045 * (0.6 + 0.4 * sin(uTime * 1.7)) * exp(-r * 2.2));

    //白闪（封顶防全盲）
    col = lerp(col, float3(1.0, 1.0, 1.0), saturate(uFlash) * 0.92);

    return float4(col, 1.0);
}

technique VeilTech
{
    pass VeilPass
    {
        PixelShader = compile ps_3_0 VeilPS();
    }
}
