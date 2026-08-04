//提灯童子鬼灯笼，预乘 Alpha

float uTime;
float uOpacity;
float uIgnition;
float uExtinguish;
float uPulse;
float uSeed;

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

float BoxMask(float2 p, float2 halfSize, float feather)
{
    float2 d = abs(p) - halfSize;
    float dist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
    return 1.0 - smoothstep(-feather, feather, dist);
}

float4 PSLantern(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float aa = 0.035;

    float n0 = tex2D(noiseSamp,
        float2(coords.x * 2.6 + uSeed * 0.173, coords.y * 1.9 - uTime * 0.045)).r;
    float n1 = tex2D(noiseSamp,
        float2(coords.x * 5.4 - uTime * 0.075 + uSeed * 0.619,
            coords.y * 4.1 + uTime * 0.028)).r;
    float grain = n0 * 0.66 + n1 * 0.34;

    //纸罩轮廓
    float taper = 1.0 - smoothstep(0.05, 0.86, -p.y) * 0.14;
    float2 bodyP = float2(p.x / (0.635 * taper), p.y / 0.755);
    float bodyMetric = dot(bodyP, bodyP);
    float paper = 1.0 - smoothstep(1.0 - aa * 2.0, 1.0 + aa * 2.0, bodyMetric);

    float topCap = BoxMask(p - float2(0.0, -0.790), float2(0.285, 0.068), aa);
    float bottomCap = BoxMask(p - float2(0.0, 0.790), float2(0.255, 0.068), aa);
    float hanger = BoxMask(p - float2(0.0, -0.925), float2(0.105, 0.080), aa);
    float tassel = BoxMask(p - float2(0.0, 0.925), float2(0.040, 0.095), aa);
    float hardware = max(max(topCap, bottomCap), max(hanger, tassel));

    //焦纸熄灭
    float edgeDistance = saturate((bodyMetric - 0.48) * 1.72);
    float lowerBias = smoothstep(0.05, 0.90, p.y) * 0.18;
    float burnField = grain * 0.72 + edgeDistance * 0.34 + lowerBias;
    float extinction = 1.0 - smoothstep(0.74 - uExtinguish * 0.94,
        0.88 - uExtinguish * 0.94, burnField);
    float visiblePaper = paper * extinction;

    float innerPaper = 1.0 - smoothstep(0.72, 0.98, bodyMetric);
    float scorchedEdge = saturate(paper - innerPaper);

    //竹骨纸筋
    float horizontalRibs = 1.0 - smoothstep(0.020, 0.050,
        abs(frac((p.y + 0.74) * 3.15) - 0.5));
    horizontalRibs *= paper * step(abs(p.y), 0.70);
    float verticalRib = (1.0 - smoothstep(0.018, 0.050, abs(p.x))) * paper;
    float ribs = saturate(horizontalRibs * 0.64 + verticalRib * 0.54);

    float sphereShade = saturate(0.92 - abs(p.x) * 0.48 - bodyMetric * 0.10);
    float paperFiber = 0.82 + grain * 0.16 + n1 * 0.06;
    float3 paperDark = float3(0.105, 0.014, 0.012);
    float3 paperRed = float3(0.455, 0.048, 0.035);
    float3 paperWarm = float3(0.710, 0.105, 0.050);
    float3 paperColor = lerp(paperDark, paperRed, sphereShade * paperFiber);
    paperColor = lerp(paperColor, paperWarm, innerPaper * 0.22 * uIgnition);
    paperColor = lerp(paperColor, float3(0.028, 0.008, 0.006),
        saturate(scorchedEdge * 0.82 + ribs * 0.72));

    //暖红鬼火
    float pulseTighten = 1.0 - saturate(uPulse) * 0.34;
    float flicker = 0.88 + 0.12 * sin(uTime * 7.4 + uSeed * 5.7 + n0 * 2.2);
    float2 flameP = p - float2((n1 - 0.5) * 0.045, 0.16 + uPulse * 0.045);
    flameP.x /= 0.145 * pulseTighten;
    flameP.y /= 0.315;
    float flameShape = dot(flameP, flameP) + flameP.y * 0.18;
    float flame = (1.0 - smoothstep(0.62, 1.05, flameShape))
        * paper * uIgnition * (1.0 - uExtinguish) * flicker;
    float ember = (1.0 - smoothstep(0.10, 0.58, flameShape)) * flame;
    float3 flameColor = float3(0.710, 0.050, 0.016) * flame;
    flameColor += float3(0.980, 0.265, 0.045) * ember * 0.82;

    float hardwareMask = hardware * (1.0 - uExtinguish * grain * 0.42);
    float3 hardwareColor = lerp(float3(0.018, 0.007, 0.006),
        float3(0.125, 0.034, 0.018), n0 * 0.42 + 0.18);

    float3 color = paperColor * visiblePaper + flameColor;
    color = lerp(color, hardwareColor, hardwareMask);
    color *= lerp(float3(1.0, 1.0, 1.0), vertexColor.rgb, 0.32);

    float alpha = max(visiblePaper, hardwareMask);
    alpha = max(alpha, flame * 0.82);
    alpha *= saturate(uOpacity * vertexColor.a);
    color *= saturate(uOpacity * vertexColor.a);
    return float4(color, alpha);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PSLantern();
    }
}
