// Impact haptics for JungleBooze (IosHaptics.cs). Styles: 0 light, 1 medium, 2 heavy.
#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *jbGenerators[3];

static UIImpactFeedbackGenerator *JBGenerator(int style)
{
    if (style < 0 || style > 2) { style = 0; }
    if (jbGenerators[style] == nil)
    {
        UIImpactFeedbackStyle s = style == 0 ? UIImpactFeedbackStyleLight : (style == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy);
        jbGenerators[style] = [[UIImpactFeedbackGenerator alloc] initWithStyle:s];
    }
    return jbGenerators[style];
}

extern "C" void JBHaptics_Impact(int style)
{
    // Unity's player loop runs on the main thread on iOS, so UIKit can be called directly (no extra latency).
    UIImpactFeedbackGenerator *g = JBGenerator(style);
    [g impactOccurred];
    [g prepare];
}

extern "C" int JBHaptics_Supported(void)
{
    return 1;
}
