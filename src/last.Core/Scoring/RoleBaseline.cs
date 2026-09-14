namespace last.Core.Scoring;

public readonly record struct RoleBaseline(
    double DmgShare,
    double TankShare,
    double GoldShare,
    double AramKp,
    double SrKp,
    double VisionShare = 0.20);