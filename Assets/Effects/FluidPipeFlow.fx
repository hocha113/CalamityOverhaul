//液体管道载液层:以管道能量贴图(uImage0)的 alpha 为导管遮罩。
//两种工作模式(uFlowMode 门控,无分支):
//  0=液体本体:亮度/不透明度随充盈度(顶点色 a),静止不动——断电/无液即静是状态语言;
//  1=流动液团:沿管移动的不对称液团(前缘陡后尾长,静帧也读得出方向),
//    顶点色 a=活跃度;方向由 C# 侧 SpriteEffects 翻转承担,快慢液分两批不同 uSpeed。
//顶点色 rgb=液色。世界空间绘制,AlphaBlend 预乘输出。
sampler uImage0 : register(s0);

float uTime;
float uAlpha;
float uFlowMode;   //0=本体 1=液团
float uSpeed;      //液团行进速度(u/秒),水性快浆蜜慢

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float4 tex = tex2D(uImage0, coords);
    float mask = tex.a;
    float vcA = vertexColor.a;
    float3 hue = tex.rgb * vertexColor.rgb;

    //----本体层:充盈度→亮度与实度,读作"管里有多少液"(低底数,残液不冒充半管)----
    float lvl = 0.40 + 0.60 * vcA;
    float bodyA = mask * saturate(0.10 + 0.90 * vcA);
    float3 bodyRgb = hue * lvl;

    //----液团层:不对称行进团,前缘陡(挤压)后尾长(拖拽)----
    float ph = frac(coords.x - uTime * uSpeed);
    float s = ph - 0.5;
    //前缘(+u 行进侧)系数 9.5,后尾 3.6
    float k = lerp(3.6, 9.5, step(0.0, s));
    float slug = exp2(-pow(s * k, 2.0) * 1.443);
    //错相小随团,加密流感
    float ph2 = frac(coords.x - uTime * uSpeed + 0.43);
    float s2 = ph2 - 0.5;
    float k2 = lerp(4.2, 10.5, step(0.0, s2));
    float slug2 = exp2(-pow(s2 * k2, 2.0) * 1.443) * 0.45;
    float flow = saturate(slug + slug2);
    //团峰向白提亮,读作水光
    float3 flowRgb = lerp(hue * 1.25, float3(0.92, 0.95, 1.0) * (hue * 0.5 + 0.5), 0.38 * flow);
    float flowA = mask * vcA * flow * 0.88;

    //----uFlowMode 门控合成(两层全算,权重乘混合)----
    float m = saturate(uFlowMode);
    float a = lerp(bodyA, flowA, m) * uAlpha;
    float3 rgb = lerp(bodyRgb, flowRgb, m);
    return float4(rgb * a, a);
}

technique Technique1
{
    pass FluidPipeFlowPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
