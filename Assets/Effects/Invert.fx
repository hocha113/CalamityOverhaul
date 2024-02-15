sampler uImage0 : register(s0);
sampler uImage1 : register(s1);
sampler uImage2 : register(s2);
sampler uImage3 : register(s3);
float3 uColor;
float3 uSecondaryColor;
float2 uScreenResolution;
float2 uScreenPosition;
float2 uTargetPosition;
float2 uDirection;
float uOpacity;
float uTime;
float uIntensity;
float uProgress;
float2 uImageSize1;
float2 uImageSize2;
float2 uImageSize3;
float2 uImageOffset;
float uSaturation;
float4 uSourceRect;
float2 uZoom;
float4 uLegacyArmorSourceRect;
float2 uLegacyArmorSheetSize;
float4 uShaderSpecificData;

float4 Main(float2 coords : TEXCOORD0) : COLOR0
{
	float4 colour = tex2D(uImage0, coords);
	float2 comparePoint = (uTargetPosition - uScreenPosition) / uScreenResolution;
	float threshold = uOpacity;
	
	float lerp1 = (uOpacity - 0.5) * 2;
	if (lerp1 < 0)
		lerp1 = 0;
	float lerp2 = 1 - lerp1;

	float averageColour = (colour.r + colour.g + colour.b) / 3; //comes before inversion to remember grayscale of original

	if (threshold >= 1 || distance(coords, comparePoint) < threshold) // If the shader has fully faded in OR the pixel is close enough
	{
		colour = float4(colour.a - colour.r, colour.a - colour.g, colour.a - colour.b, colour.a); // Invert
	}

	colour = float4(averageColour * lerp1 + colour.r * lerp2, averageColour * lerp1 + colour.g * lerp2, averageColour * lerp1 + colour.b * lerp2, colour.a); //grayscale
	
	return colour;
}

technique Technique1
{
	pass Main
	{
		PixelShader = compile ps_2_0 Main();
	}
}