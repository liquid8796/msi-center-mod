using MsiCenterMod.Models;
using MsiCenterMod.Services.Abstractions;

namespace MsiCenterMod.Services.Hardware.Aspects;

/// <summary>Áp Windows power mode overlay (nếu scenario có yêu cầu).</summary>
public sealed class PowerOverlayAspect(IPowerOverlayService powerOverlay) : IScenarioAspect
{
    public string DisplayName => "Power mode Windows";

    public int Order => 30;

    public Task ApplyAsync(ScenarioProfile profile, CancellationToken ct)
    {
        if (profile.PowerOverlay != PowerOverlayMode.None && !powerOverlay.Apply(profile.PowerOverlay))
        {
            throw new InvalidOperationException("Windows từ chối thay đổi power mode overlay.");
        }

        return Task.CompletedTask;
    }
}
