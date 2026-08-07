// ============================================================================
//OniLedgerPeek.fx 台账翻页待出:屏缘一角前幕常掀,缝里是邻屋
//三层纵深:缝内=更深一层的邻屋暗+自屏缘渗入的邻屋光+卷檐投进缝里的AO影;
//卷檐=前幕背面的圆筒明暗(外缘一线纸边,缝光亲吻内缘);檐外=缝光在前幕上的余晖。
//开缝宽的波形式与 OniLedgerPeek.cs 的 GapW 同式——命中/缝内提示物按它取样,改一处必改两处。
//uFlip=+1 缝在 quad 左缘(屏左),-1 缝在 quad 右缘(屏右);绑定噪声 s1;AlphaBlend 预乘输出
// ============================================================================

sampler uImage0 : register(s0);
sampler uNoise : register(s1);

float uTime;
float uAlpha;
float uLift;          //0 静息 ~ 1 悬停掀起
float uPress;         //0~1 点击弹亮
float uFlip;          //+1 缝在左 / -1 缝在右
float uStir;          //0~1 对面回声搅动(呼吸加深)
float uSeed;
float2 uResolution;
float3 uColInk;       //前幕/邻屋暗底
float3 uColPaper;     //幕背纸色
float3 uColAccent;    //邻屋主光(绯月红/烛暖)
float3 uColGlint;     //邻屋次光(鬼火青/金象嵌)
float3 uColHot;       //点击白热

#define PI 3.14159265

float vnoise(float2 uv) {
    return tex2D(uNoise, uv).r;
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float y01 = coords.y;
    float ex01 = uFlip > 0.0 ? coords.x : 1.0 - coords.x;
    float ex = ex01 * uResolution.x;

    //上下端融入夜色,幕角不作硬裁
    float endFade = smoothstep(0.0, 0.10, y01) * (1.0 - smoothstep(0.90, 1.0, y01));
    if (endFade <= 0.004) {
        return float4(0, 0, 0, 0);
    }

    //====开缝宽(与 C# GapW 同式):纵向波形边,中段最敞,呼吸/搅动====
    float wave = pow(abs(sin(PI * y01)), 1.35);
    float open01 = 0.30 + 0.70 * wave;
    float wob = 1.0 + (0.05 + uStir * 0.10) * sin(uTime * 0.8 + y01 * 5.0 + uSeed);
    float liftE = uLift * uLift * (3.0 - 2.0 * uLift);
    float gapW = lerp(uResolution.x * 0.16, uResolution.x * 0.52, liftE) * open01 * wob;
    float rollW = 7.0 + 5.0 * liftE;

    float grain = vnoise(float2(y01 * 3.0 + uSeed, ex01 * 2.0 + uSeed * 0.7));

    float3 C = float3(0.0, 0.0, 0.0);
    float A = 0.0;

    if (ex < gapW) {
        //====缝内:邻屋比本屋更深一层,光自屏缘渗入,卷檐把影投进缝里====
        float3 room = uColInk * 0.50;
        float spill = exp(-ex / (gapW * 0.55 + 5.0));
        float breath = 0.75 + 0.25 * sin(uTime * 0.9 + uSeed * 3.0);
        float shaft = 0.8 + 0.4 * vnoise(float2(y01 * 1.6 + uSeed, uTime * 0.03));
        room += uColAccent * (spill * 0.55 * breath * shaft);
        room += uColGlint * (spill * spill * 0.30 * (0.6 + 0.4 * sin(uTime * 1.7 + y01 * 9.0)));
        room *= 0.9 + grain * 0.2;
        float ao = exp(-(gapW - ex) / 6.0);
        room *= 1.0 - ao * 0.55;
        C = room;
        A = 0.93;
    }
    else if (ex < gapW + rollW) {
        //====卷檐:幕布背面拱起的圆筒,近缝承邻屋光,外缘一线纸边====
        float t = (ex - gapW) / rollW;
        float3 back = lerp(uColInk, uColPaper, 0.15);
        float shade = 0.38 + 0.72 * exp(-pow((t - 0.60) / 0.30, 2.0));
        shade *= 0.45 + 0.55 * smoothstep(0.0, 0.28, t);
        back *= shade * (0.92 + grain * 0.16);
        back += uColAccent * exp(-pow(t / 0.22, 2.0)) * 0.22;
        back = lerp(back, uColPaper * 0.55, smoothstep(0.90, 1.0, t));
        back += uColHot * (uPress * exp(-pow((t - 0.6) / 0.35, 2.0)) * 0.8);
        C = back;
        A = 0.97;
    }
    else {
        //====檐外:缝光在前幕上的余晖,几像素即没====
        float d = ex - gapW - rollW;
        float kiss = exp(-d / 9.0) * (0.16 + liftE * 0.14);
        C = uColAccent * kiss;
        A = kiss * 0.5;
    }

    return float4(C * A, A) * (uAlpha * endFade) * vertexColor;
}

technique Technique1
{
    pass OniLedgerPeekPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
