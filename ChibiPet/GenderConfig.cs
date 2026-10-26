using System;

namespace ChibiPet;

public sealed class GenderConfig
{
    public bool Enabled { get; set; } = false;
    public double Size { get; set; } = 0.65;
    public double YOffset { get; set; } = 0.0;
    public double Depth { get; set; } = 1.0;
    public bool Physics { get; set; } = true;
    public double BounceIntensity { get; set; } = 1.0;
    public double Cleavage { get; set; } = 0.25;

    public static GenderConfig FromSettings(AppSettings s) => new()
    {
        Enabled = s.FemaleGenderEnabled,
        Size = s.BreastSize,
        YOffset = s.BreastYOffset,
        Depth = s.BreastDepth,
        Physics = s.BreastPhysics,
        BounceIntensity = s.BounceIntensity,
        Cleavage = s.Cleavage
    };
}
