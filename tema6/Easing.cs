namespace Egc;

/// <summary>Funcție de easing: primește progresul t ∈ [0, 1] și întoarce progresul "modelat".</summary>
public delegate float EasingFunction(float t);

/// <summary>
/// NOU: funcțiile de easing (secțiunea 3). Toate pornesc din 0 și ajung în 1, deci animația
/// începe și se termină în aceleași puncte; diferă doar viteza pe drum.
/// </summary>
public static class Easing
{
    /// <summary>Interpolarea liniară: a pentru t = 0, b pentru t = 1, mijlocul pentru t = 0.5.</summary>
    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>Viteză constantă.</summary>
    public static float Linear(float t) => t;

    /// <summary>Pornește încet și accelerează. Exemplu complet.</summary>
    public static float EaseIn(float t) => t * t;

    /// <summary>Pornește repede și frânează la sosire.</summary>
    public static float EaseOut(float t)
    {
        // TODO 6.2 (a): 1 - (1 - t)²   (EaseIn "întoarsă")
        return 1f - MathF.Pow(1f - t, 3f) ;   // provizoriu
    }

    /// <summary>Pornește încet, accelerează, apoi frânează la sosire.</summary>
    public static float EaseInOut(float t)
    {
        // TODO 6.2 (a): t² · (3 - 2t)
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;// provizoriu
    }

    /// <summary>Toate funcțiile, cu nume (pentru scena laboratorului și pentru meniuri).</summary>
    public static readonly (string Name, EasingFunction Function)[] All =
    {
        ("Linear", Linear),
        ("EaseIn", EaseIn),
        ("EaseOut", EaseOut),
        ("EaseInOut", EaseInOut),
    };
}
