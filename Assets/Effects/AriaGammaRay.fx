// ============================================================================
//AriaGammaRay.fx 寰宇咏叹调·伽马射线暴
//四顶点条带；UV.x 0=枪口→1=末端；UV.y 横截面；Additive
//细过曝白核+紫晕+切伦科夫蓝边+马赫环+相位流纹；命中端热球/自由端撕裂纺锤
// ============================================================================

float4x4 transformMatrix;
float uTime;
float fadeAlpha;     //整体透明度
float uOvershoot;    //开火首帧过冲白闪 0~1
float uHitWall;      //1=末端撞墙(热球) 0=自由端(纺锤尖)
float uLengthPx;     //命中点束长(px)
float uStripLenPx;   //条带几何总长(px)：撞墙时向墙内延伸容纳热球
float uHalfWidthPx;  //顶点条带半宽(px)
float seed;

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

//伽马色板：紫白核→亮紫→切伦科夫蓝
static const float3 ColCore = float3(0.949, 0.922, 1.0);
static const float3 ColViolet = float3(0.608, 0.420, 1.0);
static const float3 ColCheren = float3(0.220, 0.714, 1.0);
static const float3 ColDeep = float3(0.243, 0.125, 0.545);

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

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float along = input.TexCoords.x;             //0 枪口 → 1 末端
    float crossN = (input.TexCoords.y - 0.5) * 2.0; //-1~1 横截面
    float alongPx = along * uStripLenPx;
    float crossPx = crossN * uHalfWidthPx;

    //撞墙时束体止于命中点(热球独立),墙内不再画束身
    float bodyEnd = 1.0 - smoothstep(uLengthPx, uLengthPx + uHalfWidthPx * 0.6, alongPx) * uHitWall;

    //=========================================================
    //宽度包络：枪口漏斗收束(宽→细) + 末端处理
    //=========================================================
    //枪口段：能量从宽漏斗聚焦进细核(前4%)
    float funnel = lerp(1.8, 1.0, smoothstep(0.0, 0.045, along));

    //自由端：噪声撕裂的纺锤尖；撞墙端：保持全宽直至热球
    float tipTurb = tex2D(noiseSamp, float2(along * 3.0 - uTime * 2.6, crossN * 0.6 + seed + 0.5)).r - 0.5;
    float alongTip = along - tipTurb * 0.08 * (1.0 - uHitWall);
    float freeTaper = lerp(1.0, 0.22, smoothstep(0.82, 1.0, alongTip)) ;
    float tipFade = 1.0 - smoothstep(0.86, 1.0, alongTip) * (1.0 - uHitWall);
    float widthEnv = funnel * lerp(freeTaper, 1.0, uHitWall);

    //=========================================================
    //高频相位抖动：伽马束的核宽微颤(不摆轴,刚性感)
    //=========================================================
    float jitter = tex2D(noiseSamp, float2(along * 7.0 - uTime * 6.0, seed)).g;
    float coreW = 0.085 * widthEnv * (0.85 + jitter * 0.3);

    //=========================================================
    //马赫环 shock diamonds：沿束周期亮结,向末端奔流
    //=========================================================
    float diamondPhase = alongPx / 92.0 - uTime * 9.0;
    float diamonds = pow(saturate(sin(diamondPhase * 6.28318) * 0.5 + 0.5), 7.0);
    //亮结处核微胀
    coreW *= 1.0 + diamonds * 0.45;

    //=========================================================
    //层1：过曝白核
    //=========================================================
    float core = exp(-pow(crossN / max(coreW, 1e-4), 2.0));
    core *= 1.0 + diamonds * 1.1;

    //=========================================================
    //层2：紫晕(束身辉光)
    //=========================================================
    float glowW = 0.42 * widthEnv;
    float glow = exp(-pow(crossN / glowW, 2.0)) * 0.55;

    //相位流纹：辉光内的高速流动条纹
    float streak = tex2D(noiseSamp, float2(along * 5.5 - uTime * 4.2, crossN * 0.35 + seed)).b;
    glow *= 0.7 + streak * 0.6;

    //=========================================================
    //层3：切伦科夫蓝边(束周介质发光,包络钳位防条带边缘硬裁)
    //=========================================================
    float cherEnv = min(widthEnv, 1.0);
    float cherBand = smoothstep(0.30 * cherEnv, 0.55 * cherEnv, abs(crossN))
                   * (1.0 - smoothstep(0.62 * cherEnv, 0.95 * cherEnv, abs(crossN)));
    float cherWave = 0.65 + 0.35 * sin(alongPx / 46.0 - uTime * 6.0);
    float cheren = cherBand * cherWave * 0.5;

    //=========================================================
    //电离闪点：束身随机高能白斑
    //=========================================================
    float ion = tex2D(noiseSamp, float2(along * 11.0 + uTime * 2.0, seed * 3.0 + floor(uTime * 14.0) * 0.13)).r;
    ion = step(0.90, ion) * exp(-pow(crossN / (0.30 * widthEnv), 2.0)) * 1.4;

    //=========================================================
    //枪口麻花环：入口处一圈亮环(荷电聚焦)
    //=========================================================
    float muzzleRing = exp(-pow((alongPx - 10.0) / 14.0, 2.0))
                     * smoothstep(1.05, 0.55, abs(crossN / max(funnel * 0.33, 1e-3)));

    //=========================================================
    //命中端热球 + 反溅
    //=========================================================
    float ballR = max(uHalfWidthPx * 0.85, 26.0);
    float dEnd = sqrt(pow((uLengthPx - alongPx) / ballR, 2.0) + pow(crossPx / ballR, 2.0));
    float ball = exp(-dEnd * dEnd) * 2.2 * uHitWall;
    //反溅弧：命中点外一圈涟漪
    float splash = exp(-pow((dEnd - 1.6) / 0.5, 2.0)) * 0.6 * uHitWall
                 * (0.7 + 0.3 * sin(uTime * 12.0));

    //=========================================================
    //合成：束身受 bodyEnd 截断,热球/反溅独立存活
    //=========================================================
    float3 body = float3(0.0, 0.0, 0.0);
    body += ColViolet * glow;
    body += lerp(ColCheren, ColViolet, 0.3) * cheren;
    body += ColDeep * exp(-pow(crossN / (0.75 * cherEnv), 2.0)) * 0.22;
    body += ColCore * core * 1.35;
    body += ColCore * ion;
    body += lerp(ColCore, ColViolet, 0.4) * muzzleRing * 1.3;

    //开火过冲：全束白闪
    body += ColCore * uOvershoot * (core * 1.6 + glow * 1.2);

    float3 col = body * bodyEnd * tipFade;
    col += lerp(ColCore, ColCheren, 0.35) * ball;
    col += ColCheren * splash;

    col *= fadeAlpha;

    float a = saturate(max(col.r, max(col.g, col.b)));
    return float4(col, a) * input.Color;
}

technique Technique1
{
    pass GammaRayPass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
