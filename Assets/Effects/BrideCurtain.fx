//绯嫁「迎亲」喜堂帘面与冷喜天幕，预乘 Alpha
//TechRig: 世界锚定帘面四联画——墨底绸纹+上缘金压线衬绯线+轿帘双扇(uClose)+
//         冷烛五点(uCandle)+合卺帘缝(uSlit)；下缘噪声撕口，无整块矩形
//TechHall: 全屏冷墨红天幕暗角，强度 uIntensity；直线算术+平 tex2D，无分支

float uTime;
float uSeed;
float uFade;
float uClose;
float uCandle;
float uSlit;
float uAspect;
float uIntensity;

texture uNoiseTex;
sampler noiseSamp : register(s1) = sampler_state
{
    texture = <uNoiseTex>;
    magfilter = LINEAR;
    minfilter = LINEAR;
    mipfilter = LINEAR;
    AddressU = wrap;
    AddressV = wrap;
};

//单点冷烛：焰心细、焰舌上挑、外晕刻意压弱，光是结构不是光球
float3 CandleLight(float2 uv, float cx, float cy, float phase)
{
    float2 d = float2((uv.x - cx) * uAspect, uv.y - cy);
    float breath = 0.82 + 0.18 * sin(uTime * 2.3 + phase);
    float2 f = float2(d.x, d.y * 0.62);
    float core = exp2(-dot(f, f) * 26000.0);
    float body = exp2(-dot(f, f) * 5600.0);
    float up = max(0.0, -d.y);
    float below = max(0.0, d.y);
    float lick = exp2(-(d.x * d.x * 22000.0 + up * 80.0 + below * 3000.0));
    float halo = exp2(-dot(d, d) * 640.0);
    float3 c = float3(1.00, 0.87, 0.62) * core * 1.15
        + float3(0.90, 0.54, 0.24) * body * 0.50
        + float3(0.86, 0.40, 0.18) * lick * 0.22
        + float3(0.50, 0.15, 0.12) * halo * 0.16;
    return c * breath;
}

float4 PSRig(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 uv = coords;
    float2 p = uv - 0.5;
    float px = p.x * uAspect;

    float n0 = tex2D(noiseSamp, uv * float2(1.7, 2.3)
        + float2(uSeed * 0.31, uTime * 0.006)).r;
    float n1 = tex2D(noiseSamp, uv * float2(5.2, 6.8)
        + float2(uSeed * 0.77, -uTime * 0.004)).r;

    //====圆角堂形罩====
    float2 b = float2(0.40, 0.455);
    float2 q = abs(float2(px, p.y)) - b + 0.09;
    float sd = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - 0.09;
    float hall = 1.0 - smoothstep(-0.012, 0.02, sd);
    float hallSoft = 1.0 - smoothstep(-0.03, 0.10, sd);

    //====墨底纵深 + 绸纹====
    float3 back = lerp(float3(0.030, 0.012, 0.017), float3(0.155, 0.036, 0.052),
        pow(uv.y, 1.6));
    back *= 0.82 + 0.36 * n0;
    //衬幕浓度随帘合拢收放：开帘受人时堂内是空的，不把人蒙住
    float backA = 0.60 * (0.30 + 0.70 * uClose);

    //====上缘幔帐弧垂====
    float valanceEdge = 0.13 + 0.035 * sin(uv.x * 25.0 + uSeed * 9.0) + n1 * 0.02;
    float valance = 1.0 - smoothstep(valanceEdge - 0.015, valanceEdge + 0.015, uv.y);
    float3 valanceCol = lerp(float3(0.115, 0.026, 0.038), float3(0.235, 0.052, 0.068),
        0.5 + 0.5 * sin(uv.x * 60.0 + n0 * 4.0));

    //====上缘金压线,内衬一线绯红(换了场面仍是这家的规矩)====
    float rail = exp2(-abs(uv.y - 0.045) * 320.0);
    float thread = exp2(-abs(uv.y - 0.061) * 420.0);

    //====轿帘双扇====
    float fold = 0.5 + 0.5 * sin(uv.x * 44.0 + n0 * 5.0);
    float closeHalf = uClose * 0.5;
    float eL = closeHalf + 0.014 * sin(p.y * 19.0 + uTime * 1.5 + uSeed * 7.0);
    float eR = 1.0 - closeHalf + 0.014 * sin(p.y * 23.0 - uTime * 1.3 + uSeed * 5.0);
    float inL = 1.0 - smoothstep(eL - 0.014, eL + 0.004, uv.x);
    float inR = smoothstep(eR - 0.004, eR + 0.014, uv.x);
    float curtain = max(inL, inR);
    //下缘噪声撕口,绸破不齐
    float hem = smoothstep(0.86 - n1 * 0.08, 0.985 - n1 * 0.06, uv.y);
    curtain *= 1.0 - hem * 0.92;

    float3 cloth = lerp(float3(0.135, 0.030, 0.045), float3(0.300, 0.062, 0.082),
        fold * 0.62 + n0 * 0.38);
    cloth *= 0.88 + 0.24 * n1;
    //帘内缘金压线衬绯线
    float trimGoldL = exp2(-abs(uv.x - eL + 0.006) * 300.0);
    float trimRedL = exp2(-abs(uv.x - eL + 0.017) * 380.0);
    float trimGoldR = exp2(-abs(uv.x - eR - 0.006) * 300.0);
    float trimRedR = exp2(-abs(uv.x - eR - 0.017) * 380.0);
    float gateClose = step(0.02, uClose);
    float3 clothTrim = cloth;
    clothTrim = lerp(clothTrim, float3(0.72, 0.57, 0.24),
        saturate(trimGoldL * inL + trimGoldR * inR) * 0.8 * gateClose);
    clothTrim = lerp(clothTrim, float3(0.48, 0.09, 0.10),
        saturate(trimRedL * inL + trimRedR * inR) * 0.7 * gateClose);

    //====身体合成====
    float3 col = back;
    float a = backA;
    col = lerp(col, valanceCol, valance * 0.85);
    a = max(a, valance * 0.9);
    col = lerp(col, float3(0.60, 0.46, 0.17), rail * 0.75);
    col = lerp(col, float3(0.42, 0.08, 0.09), thread * 0.65);
    float ca = curtain * 0.97;
    col = lerp(col, clothTrim, ca);
    a = max(a, ca);
    a *= hall;

    //====光层: 冷烛五点 + 合卺帘缝====
    float3 light = float3(0.0, 0.0, 0.0);
    light += CandleLight(uv, 0.16, 0.745, 0.0);
    light += CandleLight(uv, 0.33, 0.775, 1.7);
    light += CandleLight(uv, 0.50, 0.785, 3.4);
    light += CandleLight(uv, 0.67, 0.775, 5.1);
    light += CandleLight(uv, 0.84, 0.745, 6.8);
    light *= uCandle;

    float slitBody = exp2(-abs(px) * 300.0);
    float slitSoft = exp2(-abs(px) * 60.0);
    float slitSpan = 1.0 - smoothstep(0.30, 0.46, abs(p.y));
    light += (float3(1.00, 0.90, 0.74) * slitBody * 1.2
        + float3(0.80, 0.22, 0.16) * slitSoft * 0.35) * slitSpan * uSlit;

    float vA = saturate(uFade * vertexColor.a);
    float outA = a * vA;
    float3 outC = col * outA + light * hallSoft * vA;
    return float4(outC, outA);
}

float4 PSHall(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = coords - 0.5;
    float vign = smoothstep(0.18, 0.85, length(p * float2(1.2, 1.0)));
    float n = tex2D(noiseSamp, coords * float2(3.1, 2.2)
        + float2(uTime * 0.004, uSeed * 0.13)).r;
    float3 col = lerp(float3(0.105, 0.022, 0.038), float3(0.040, 0.010, 0.018), vign)
        * (0.78 + 0.44 * n);
    float a = saturate(uIntensity) * (0.26 + 0.42 * vign) * vertexColor.a;
    return float4(col * a, a);
}

technique TechRig
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSRig();
    }
}

technique TechHall
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSHall();
    }
}
